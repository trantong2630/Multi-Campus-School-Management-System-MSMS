using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Dtos;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>
/// LRN-02 (phía học sinh): mở bài, Bắt đầu (bài có thời lượng), tự lưu nháp, nộp một lượt (BR-21).
/// Mọi mốc thời gian lấy từ đồng hồ máy chủ. Học sinh phải nằm trong danh sách được giao lúc giao bài;
/// nộp bài không kiểm lại ghi danh nên chuyển lớp giữa chừng không làm mất bài.
/// </summary>
public sealed class AttemptService(MsmsDbContext db, CurrentUser user, IClock clock, AttemptGrader grader)
{
    public static class States
    {
        public const string NotOpen = "NotOpen";
        public const string Open = "Open";
        public const string InProgress = "InProgress";
        public const string Submitted = "Submitted";
        /// <summary>Quá hạn thực tế mà chưa nộp: hiển thị "Không nộp", không phải 0 điểm.</summary>
        public const string NotSubmitted = "NotSubmitted";
    }

    public async Task<IReadOnlyList<MyAssignmentItemDto>> ListMineAsync()
    {
        var me = user.PersonId;
        await grader.FinalizeExpiredAsync(null, me);
        var rows = await db.AssignmentRecipient.AsNoTracking()
            .Where(r => r.StudentId == me)
            .Select(r => new
            {
                r.AssignmentId, r.Assignment.Title, r.Assignment.SubjectId, SubjectName = r.Assignment.Subject.Name,
                ClassName = r.Assignment.Class.Name, r.Assignment.OpenAt, r.Assignment.DueAt, r.Assignment.DurationMinutes,
                QuestionCount = r.Assignment.AssignmentQuestion.Count,
                Sub = r.Assignment.Submission.Where(s => s.StudentId == me).Select(s => new { s.StartedAt, s.SubmittedAt, s.Score, s.CorrectCount }).FirstOrDefault(),
            }).ToListAsync();
        var now = clock.Now;
        return rows.Select(r => new MyAssignmentItemDto(
                r.AssignmentId, r.Title, r.SubjectId, r.SubjectName, r.ClassName, r.OpenAt, r.DueAt, r.DurationMinutes, r.QuestionCount,
                StateOf(r.Sub?.StartedAt, r.Sub?.SubmittedAt, r.OpenAt, r.DueAt, r.DurationMinutes, now), r.Sub?.SubmittedAt, r.Sub?.Score, r.Sub?.CorrectCount))
            .OrderBy(r => r.DueAt).ThenBy(r => r.AssignmentId).ToList();
    }

    /// <summary>Mở bài để làm. Bài có thời lượng mà chưa bấm Bắt đầu thì chưa trả câu hỏi.</summary>
    public async Task<AttemptDto> OpenAsync(int assignmentId)
    {
        await grader.FinalizeExpiredAsync(assignmentId, user.PersonId);
        var a = await LoadForWorkAsync(assignmentId);
        var submission = await FindSubmissionAsync(assignmentId);
        EnsureCanWork(a, submission, clock.Now);
        return await BuildAttemptAsync(a, submission);
    }

    /// <summary>Bắt đầu làm bài có thời lượng: tạo bài làm với giờ bắt đầu của Backend. Gọi lại thì giữ giờ bắt đầu cũ.</summary>
    public async Task<AttemptDto> StartAsync(int assignmentId)
    {
        await grader.FinalizeExpiredAsync(assignmentId, user.PersonId);
        var a = await LoadForWorkAsync(assignmentId);
        var now = clock.Now;
        var submission = await FindSubmissionAsync(assignmentId);
        EnsureCanWork(a, submission, now);
        if (submission is null) await EnsureSubmissionAsync(assignmentId, now);
        submission = await FindSubmissionAsync(assignmentId);
        return await BuildAttemptAsync(a, submission);
    }

    /// <summary>Tự lưu nháp: thay toàn bộ nháp bằng trạng thái hiện tại. Từ chối sau hạn thực tế và sau khi đã nộp.</summary>
    public async Task<DateTime> SaveDraftAsync(int assignmentId, SaveAnswersDto dto)
    {
        var a = await LoadForWorkAsync(assignmentId);
        var now = clock.Now;
        var existing = await FindSubmissionAsync(assignmentId);
        EnsureCanWork(a, existing, now);
        EnsureStarted(a, existing);
        var lines = await grader.LoadLinesAsync(assignmentId);
        var answers = ValidateAnswers(dto, lines);

        await using var tx = await db.Database.BeginTransactionAsync();
        var submissionId = await EnsureSubmissionAsync(assignmentId, now);
        // Cập nhật có điều kiện giữ khóa hàng tới hết transaction: lần nộp đồng thời phải chờ, và nếu đã nộp thì cập nhật này không khớp hàng nào.
        var rows = await db.Submission.Where(s => s.SubmissionId == submissionId && s.SubmittedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSavedAt, now));
        if (rows == 0) throw ApiException.Conflict("Bài đã nộp nên không lưu thêm được.");
        await grader.ReplaceAnswersAsync(submissionId, answers);
        await tx.CommitAsync();
        return now;
    }

    /// <summary>Nộp bài một lượt và chấm ngay. Bài có thời lượng ghi giờ nộp không muộn hơn hạn thực tế.</summary>
    public async Task<SubmitResultDto> SubmitAsync(int assignmentId, SaveAnswersDto dto)
    {
        var a = await LoadForWorkAsync(assignmentId);
        var now = clock.Now;
        var existing = await FindSubmissionAsync(assignmentId);
        EnsureCanWork(a, existing, now);
        EnsureStarted(a, existing);
        var lines = await grader.LoadLinesAsync(assignmentId);
        var answers = ValidateAnswers(dto, lines);

        var submissionId = await EnsureSubmissionAsync(assignmentId, now);
        var deadline = AssignmentRules.EffectiveDeadline(a.DueAt, a.DurationMinutes, existing?.StartedAt ?? now);
        var submittedAt = now < deadline ? now : deadline;
        if (!await grader.FinalizeAsync(submissionId, assignmentId, submittedAt, answers))
            throw ApiException.Conflict("Bài đã nộp trước đó. Mỗi bài chỉ nộp một lần.");
        return new SubmitResultDto(submittedAt);
    }

    // ---- kiểm tra ----

    /// <summary>Tải bài mà học sinh nằm trong danh sách được giao; học sinh ngoài danh sách không mở được bài.</summary>
    private async Task<Assignment> LoadForWorkAsync(int assignmentId)
    {
        var a = await db.Assignment.AsNoTracking().Include(x => x.Subject).Include(x => x.Class)
            .FirstOrDefaultAsync(x => x.AssignmentId == assignmentId && x.AssignmentRecipient.Any(r => r.StudentId == user.PersonId));
        return a ?? throw ApiException.NotFound("Không tìm thấy bài tập trong danh sách được giao của tài khoản này.");
    }

    private Task<Submission?> FindSubmissionAsync(int assignmentId) =>
        db.Submission.AsNoTracking().FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == user.PersonId);

    private static void EnsureCanWork(Assignment a, Submission? submission, DateTime now)
    {
        if (submission?.SubmittedAt is not null)
            throw ApiException.BadRequest("Đã nộp bài này, mỗi bài chỉ nộp một lần.");
        if (now < a.OpenAt)
            throw ApiException.BadRequest($"Bài chưa mở. Mở lúc {a.OpenAt:HH:mm dd/MM/yyyy}.");
        var deadline = AssignmentRules.EffectiveDeadline(a.DueAt, a.DurationMinutes, submission?.StartedAt);
        if (AssignmentRules.IsPastDeadline(deadline, now))
            throw ApiException.BadRequest(a.DurationMinutes is not null && submission is not null
                ? "Đã hết giờ làm bài. Bài đã đóng, không làm hay lưu được nữa."
                : "Đã quá hạn nộp. Bài đã đóng, không làm hay lưu được nữa.");
    }

    /// <summary>Bài có thời lượng phải bấm Bắt đầu trước, để giờ bắt đầu do Backend ghi chứ không do máy học sinh.</summary>
    private static void EnsureStarted(Assignment a, Submission? submission)
    {
        if (a.DurationMinutes is not null && submission is null)
            throw ApiException.BadRequest("Bài có giới hạn thời gian. Bấm Bắt đầu rồi mới làm bài.");
    }

    private async Task<AttemptDto> BuildAttemptAsync(Assignment a, Submission? submission)
    {
        var now = clock.Now;
        var timed = a.DurationMinutes is not null;
        var started = !timed || submission is not null;
        var lines = await grader.LoadLinesAsync(a.AssignmentId);
        var saved = submission is null ? [] : await db.SubmissionAnswer.AsNoTracking()
            .Where(x => x.SubmissionId == submission.SubmissionId)
            .Select(x => new AnswerInputDto(x.AssignmentQuestionId, x.SubmissionAnswerOption.Select(o => o.OptionId).ToList())).ToListAsync();
        return new AttemptDto(
            a.AssignmentId, a.Title, a.Instruction, a.SubjectId, a.Subject.Name, a.Class.Name, a.OpenAt, a.DueAt, a.DurationMinutes,
            started, submission?.StartedAt, AssignmentRules.EffectiveDeadline(a.DueAt, a.DurationMinutes, submission?.StartedAt), now,
            lines.Count, lines.Sum(l => l.Points), submission?.LastSavedAt,
            started ? AssignmentService.ToQuiz(lines) : [], saved);
    }

    /// <summary>Mọi câu trả lời phải thuộc bài và lựa chọn phải thuộc đúng câu đó; câu một đáp án chỉ chọn một lựa chọn.</summary>
    private static Dictionary<int, List<int>> ValidateAnswers(SaveAnswersDto dto, List<AssignmentQuestion> lines)
    {
        var result = new Dictionary<int, List<int>>();
        foreach (var answer in dto.Answers ?? [])
        {
            var line = lines.FirstOrDefault(l => l.AssignmentQuestionId == answer.AssignmentQuestionId)
                ?? throw ApiException.BadRequest("Có câu trả lời không thuộc bài. Tải lại trang rồi làm tiếp.");
            if (result.ContainsKey(line.AssignmentQuestionId)) throw ApiException.BadRequest("Mỗi câu chỉ gửi một lần. Tải lại trang rồi làm tiếp.");
            var optionIds = (answer.OptionIds ?? []).Distinct().ToList();
            if (optionIds.Any(id => line.Question.QuestionOption.All(o => o.OptionId != id)))
                throw ApiException.BadRequest("Có lựa chọn không thuộc câu hỏi. Tải lại trang rồi làm tiếp.");
            var multiple = line.Question.QuestionOption.Count(o => o.IsCorrect) > 1;
            if (!multiple && optionIds.Count > 1)
                throw ApiException.BadRequest($"Câu {line.OrderNo} chỉ chọn một đáp án.");
            result[line.AssignmentQuestionId] = optionIds;
        }
        return result;
    }

    /// <summary>Tạo bản ghi bài làm nếu chưa có; hai thẻ trình duyệt cùng tạo thì lấy bản ghi đã có.</summary>
    private async Task<long> EnsureSubmissionAsync(int assignmentId, DateTime now)
    {
        var id = await db.Submission.Where(s => s.AssignmentId == assignmentId && s.StudentId == user.PersonId)
            .Select(s => (long?)s.SubmissionId).FirstOrDefaultAsync();
        if (id is not null) return id.Value;
        var created = new Submission { AssignmentId = assignmentId, StudentId = user.PersonId, StartedAt = now, LastSavedAt = now };
        db.Submission.Add(created);
        try
        {
            await db.SaveChangesAsync();
            return created.SubmissionId;
        }
        catch (DbUpdateException)
        {
            db.Entry(created).State = EntityState.Detached;
            return await db.Submission.Where(s => s.AssignmentId == assignmentId && s.StudentId == user.PersonId)
                .Select(s => s.SubmissionId).FirstAsync();
        }
    }

    public static string StateOf(DateTime? startedAt, DateTime? submittedAt, DateTime openAt, DateTime dueAt, short? durationMinutes, DateTime now)
    {
        if (submittedAt is not null) return States.Submitted;
        var deadline = AssignmentRules.EffectiveDeadline(dueAt, durationMinutes, startedAt);
        if (AssignmentRules.IsPastDeadline(deadline, now)) return States.NotSubmitted;
        if (now < openAt) return States.NotOpen;
        return startedAt is not null ? States.InProgress : States.Open;
    }
}
