using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>
/// LRN-02 (phía giáo viên): giao bài cho lớp (BR-21). Teacher đang phụ trách chuyên môn ở (lớp, môn) chọn câu Sẵn dùng cùng môn
/// từ ngân hàng. Bài giao ngay: lưu danh sách được giao, báo học sinh; từ đó không sửa, không xóa, không gia hạn.
/// </summary>
public sealed class AssignmentService(
    MsmsDbContext db, CurrentUser user, IClock clock, LrnAccess access, QuestionService questions,
    SubjectResponsibilityService responsibility, NotificationSender notifier)
{
    /// <summary>Các (lớp, môn) người gọi đang phụ trách, dùng để chọn lớp khi giao bài.</summary>
    public async Task<IReadOnlyList<ClassSubjectOptionDto>> MyClassSubjectsAsync(int? subjectId)
    {
        var pairs = await responsibility.ForTeacherAsync(user.PersonId);
        return pairs.Where(p => subjectId is null || p.SubjectId == subjectId)
            .OrderBy(p => p.SubjectName, StringComparer.CurrentCultureIgnoreCase).ThenBy(p => p.GradeLevel).ThenBy(p => p.ClassName)
            .Select(p => new ClassSubjectOptionDto(p.ClassId, p.ClassName, p.GradeLevel, p.SubjectId, p.SubjectName)).ToList();
    }

    /// <summary>Bài của các (lớp, môn) người gọi đang phụ trách, mới nhất trước.</summary>
    public async Task<IReadOnlyList<AssignmentItemDto>> ListAsync()
    {
        var pairs = await responsibility.ForTeacherAsync(user.PersonId);
        if (pairs.Count == 0) return [];
        var classIds = pairs.Select(p => p.ClassId).Distinct().ToList();
        var subjectIds = pairs.Select(p => p.SubjectId).Distinct().ToList();
        var allowed = pairs.Select(p => (p.ClassId, p.SubjectId)).ToHashSet();

        var rows = await ProjectItems(db.Assignment.AsNoTracking().Where(a => classIds.Contains(a.ClassId) && subjectIds.Contains(a.SubjectId))).ToListAsync();
        var now = clock.Now;
        return rows.Where(r => allowed.Contains((r.ClassId, r.SubjectId)))
            .OrderByDescending(r => r.DueAt).ThenByDescending(r => r.AssignmentId)
            .Select(r => ToDto(r, now)).ToList();
    }

    /// <summary>Chi tiết một bài cho người xem được kết quả: giáo viên phụ trách hoặc tổ trưởng của môn.</summary>
    public async Task<AssignmentDetailDto> GetAsync(int id)
    {
        var a = await access.LoadForViewAsync(id);
        var row = (await ProjectItems(db.Assignment.AsNoTracking().Where(x => x.AssignmentId == id)).ToListAsync()).Single();
        var lines = await db.AssignmentQuestion.AsNoTracking().Include(x => x.Question).ThenInclude(q => q.QuestionOption)
            .Where(x => x.AssignmentId == id).OrderBy(x => x.OrderNo).ToListAsync();
        return new AssignmentDetailDto(ToDto(row, clock.Now), a.Instruction, lines.Sum(x => x.Points), ToLines(lines));
    }

    /// <summary>
    /// Giao bài (4.6.9) trong một transaction: khóa hàng các câu hỏi để không bị sửa giữa chừng, tạo bài, danh sách câu hỏi,
    /// danh sách được giao (học sinh có ghi danh hiệu lực ở lớp hôm nay) và thông báo cho học sinh.
    /// </summary>
    public async Task<AssignmentDetailDto> CreateAsync(CreateAssignmentDto dto)
    {
        var cls = await db.Class.AsNoTracking().FirstOrDefaultAsync(c => c.ClassId == dto.ClassId && c.IsActive)
            ?? throw ApiException.BadRequest("Lớp không tồn tại hoặc đã ngừng hoạt động. Chọn lớp khác.");
        var subject = await db.Subject.AsNoTracking().FirstOrDefaultAsync(s => s.SubjectId == dto.SubjectId && s.IsActive)
            ?? throw ApiException.BadRequest("Môn học không tồn tại hoặc đã ngừng hoạt động. Chọn môn khác.");
        if (!await access.IsResponsibleAsync(cls.ClassId, subject.SubjectId))
            throw ApiException.Forbidden($"Chỉ giáo viên đang phụ trách chuyên môn {subject.Name} ở lớp {cls.Name} mới giao bài được. Liên hệ hiệu phó kiểm tra phụ trách chuyên môn.");

        var now = clock.Now;
        var (openAt, dueAt) = ValidateTimes(dto.OpenAt, dto.DueAt, now);
        var duration = ValidateDuration(dto.DurationMinutes);
        var title = Guard.Required(dto.Title, "Tên bài tập", 200);
        var instruction = Guard.Optional(dto.Instruction, "Hướng dẫn", 500);
        var items = ValidateItems(dto.Items);

        var roster = await db.Enrollment.AsNoTracking().RosterAt(cls.ClassId, clock.Today).Select(e => e.StudentId).Distinct().ToListAsync();
        if (roster.Count == 0)
            throw ApiException.BadRequest("Lớp chưa có học sinh đang học. Ghi danh học sinh vào lớp trước khi giao bài.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var ids = items.Select(i => i.QuestionId).ToList();
        await questions.LockQuestionsAsync(ids);
        var found = await db.Question.AsNoTracking().Where(q => ids.Contains(q.QuestionId))
            .Select(q => new { q.QuestionId, q.SubjectId, q.Status, q.IsHidden }).ToListAsync();
        if (found.Count != ids.Count)
            throw ApiException.BadRequest("Có câu hỏi không còn tồn tại. Tải lại ngân hàng câu hỏi rồi chọn lại.");
        if (found.Any(q => q.SubjectId != subject.SubjectId))
            throw ApiException.BadRequest($"Bài tập môn {subject.Name} chỉ gồm câu hỏi cùng môn. Bỏ các câu thuộc môn khác.");
        if (found.Any(q => q.Status != QuestionStatuses.Ready || q.IsHidden))
            throw ApiException.BadRequest("Có câu hỏi chưa Sẵn dùng hoặc đã bị ẩn khỏi ngân hàng. Bỏ các câu đó rồi giao lại.");

        var a = new Assignment
        {
            SubjectId = subject.SubjectId, ClassId = cls.ClassId, Title = title, Instruction = instruction,
            OpenAt = openAt, DueAt = dueAt, DurationMinutes = duration, AssignedByPersonId = user.PersonId, CreatedAt = now,
        };
        var order = (short)0;
        foreach (var item in items)
            a.AssignmentQuestion.Add(new AssignmentQuestion { QuestionId = item.QuestionId, OrderNo = ++order, Points = item.Points });
        foreach (var studentId in roster)
            a.AssignmentRecipient.Add(new AssignmentRecipient { StudentId = studentId });
        db.Assignment.Add(a);
        await db.SaveChangesAsync();

        notifier.Add(roster, NotificationTypes.AssignmentAssigned, Truncate($"Bài tập mới: {a.Title}", 200),
            $"Môn {subject.Name}, hạn nộp {a.DueAt:HH:mm dd/MM/yyyy}.", "Assignment", a.AssignmentId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        db.ChangeTracker.Clear();
        return await GetAsync(a.AssignmentId);
    }

    // ---- dùng chung ----

    public static IReadOnlyList<AssignmentQuestionDto> ToLines(IEnumerable<AssignmentQuestion> lines) => lines.OrderBy(l => l.OrderNo)
        .Select(l => new AssignmentQuestionDto(l.AssignmentQuestionId, l.QuestionId, l.OrderNo, l.Points, l.Question.Content, l.Question.Explanation,
            l.Question.QuestionOption.Count(o => o.IsCorrect) > 1,
            l.Question.QuestionOption.OrderBy(o => o.OptionLabel).Select(o => new OptionDto(o.OptionId, o.OptionLabel, o.Content, o.IsCorrect)).ToList()))
        .ToList();

    public static IReadOnlyList<QuizQuestionDto> ToQuiz(IEnumerable<AssignmentQuestion> lines) => lines.OrderBy(l => l.OrderNo)
        .Select(l => new QuizQuestionDto(l.AssignmentQuestionId, l.OrderNo, l.Points, l.Question.Content,
            l.Question.QuestionOption.Count(o => o.IsCorrect) > 1, l.Question.QuestionOption.Count(o => o.IsCorrect),
            l.Question.QuestionOption.OrderBy(o => o.OptionLabel).Select(o => new QuizOptionDto(o.OptionId, o.OptionLabel, o.Content)).ToList()))
        .ToList();

    internal sealed record Row(
        int AssignmentId, string Title, int ClassId, string ClassName, int SubjectId, string SubjectName, DateTime OpenAt, DateTime DueAt,
        short? DurationMinutes, int QuestionCount, int RecipientCount, int SubmittedCount, int AssignedById, string AssignedByName);

    internal static IQueryable<Row> ProjectItems(IQueryable<Assignment> q) => q.Select(a => new Row(
        a.AssignmentId, a.Title, a.ClassId, a.Class.Name, a.SubjectId, a.Subject.Name, a.OpenAt, a.DueAt, a.DurationMinutes,
        a.AssignmentQuestion.Count, a.AssignmentRecipient.Count, a.Submission.Count(s => s.SubmittedAt != null),
        a.AssignedByPersonId, a.AssignedByPerson.FullName));

    private AssignmentItemDto ToDto(Row r, DateTime now) => new(
        r.AssignmentId, r.Title, r.ClassId, r.ClassName, r.SubjectId, r.SubjectName, r.OpenAt, r.DueAt, r.DurationMinutes, r.QuestionCount,
        r.RecipientCount, r.SubmittedCount, AssignmentRules.IsClosed(r.DueAt, now), r.AssignedByName, r.AssignedById == user.PersonId);

    /// <summary>Thời điểm mở mặc định là ngay; hạn nộp phải lớn hơn hiện tại và lớn hơn thời điểm mở. Lưu chính xác tới giây.</summary>
    private static (DateTime OpenAt, DateTime DueAt) ValidateTimes(DateTime? open, DateTime? due, DateTime now)
    {
        if (due is null) throw ApiException.BadRequest("Chọn hạn nộp bài.");
        var openAt = Floor(open ?? now);
        var dueAt = Floor(due.Value);
        if (dueAt <= now) throw ApiException.BadRequest("Hạn nộp phải lớn hơn thời điểm hiện tại. Chọn hạn muộn hơn.");
        if (dueAt <= openAt) throw ApiException.BadRequest("Hạn nộp phải sau thời điểm mở bài. Chọn hạn muộn hơn.");
        return (openAt, dueAt);
    }

    private static short? ValidateDuration(short? minutes)
    {
        if (minutes is null) return null;
        if (minutes < 1 || minutes > AssignmentRules.MaxDurationMinutes)
            throw ApiException.BadRequest($"Thời lượng làm bài từ 1 đến {AssignmentRules.MaxDurationMinutes} phút. Để trống nếu không giới hạn.");
        return minutes;
    }

    private static List<AssignmentLineDto> ValidateItems(List<AssignmentLineDto>? items)
    {
        var list = items ?? [];
        if (list.Count == 0) throw ApiException.BadRequest("Chọn ít nhất một câu hỏi cho bài tập.");
        if (list.Count > QuizRules.MaxQuestionsPerAssignment)
            throw ApiException.BadRequest($"Một bài tối đa {QuizRules.MaxQuestionsPerAssignment} câu hỏi.");
        if (list.Select(i => i.QuestionId).Distinct().Count() != list.Count)
            throw ApiException.BadRequest("Mỗi câu hỏi chỉ xuất hiện một lần trong bài.");
        if (list.Any(i => i.Points <= 0 || i.Points > QuizRules.MaxPoints || decimal.Round(i.Points, 2) != i.Points))
            throw ApiException.BadRequest($"Điểm mỗi câu lớn hơn 0 và tối đa {QuizRules.MaxPoints:0}, tối đa 2 chữ số thập phân.");
        return list;
    }

    private static DateTime Floor(DateTime v) => new(v.Ticks - v.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Unspecified);
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
