using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Text;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Dtos;

namespace MSMS.Api.Modules.Lrn.Services;

public static class QuestionStatuses
{
    public const string Draft = "Draft";
    public const string Ready = "Ready";
}

/// <summary>
/// LRN-01: ngân hàng câu hỏi dùng chung toàn trường (BR-20). Mọi Teacher xem và dùng câu Sẵn dùng; chỉ người tạo sửa;
/// câu đã nằm trong một bài tập thì không sửa được nữa, chỉ còn ẩn khỏi ngân hàng. Câu Nháp chỉ người tạo thấy.
/// </summary>
public sealed class QuestionService(MsmsDbContext db, CurrentUser user, IClock clock)
{
    /// <summary>Môn thuộc tổ chuyên môn của giáo viên: chỉ những môn này được soạn câu hỏi (kể cả nhờ AI soạn).</summary>
    public async Task<IReadOnlyList<SubjectOptionDto>> MySubjectsAsync()
    {
        var departmentId = await db.StaffProfile.AsNoTracking().Where(s => s.StaffId == user.PersonId).Select(s => s.DepartmentId).FirstOrDefaultAsync();
        if (departmentId is null) return [];
        return await db.Subject.AsNoTracking().Where(s => s.IsActive && s.DepartmentId == departmentId)
            .OrderBy(s => s.Name).Select(s => new SubjectOptionDto(s.SubjectId, s.Name)).ToListAsync();
    }

    public async Task EnsureOwnSubjectAsync(int subjectId)
    {
        if ((await MySubjectsAsync()).All(s => s.SubjectId != subjectId))
            throw ApiException.Forbidden("Chỉ soạn được câu hỏi cho môn thuộc tổ chuyên môn của mình. Chọn môn khác.");
    }

    public async Task<PagedResult<QuestionItemDto>> ListAsync(
        int? subjectId, byte? gradeLevel, string? topic, string? creator, string? status, bool mine, bool includeHidden, string? search, int page, int pageSize)
    {
        var me = user.PersonId;
        // Câu bị ẩn chỉ người tạo thấy và chỉ khi chọn xem câu đã ẩn (khi đó chỉ trả câu đã ẩn); câu Nháp chỉ người tạo thấy.
        var q = db.Question.AsNoTracking().Where(x => x.IsHidden
            ? includeHidden && x.CreatedByPersonId == me
            : x.Status == QuestionStatuses.Ready || x.CreatedByPersonId == me);

        if (includeHidden) q = q.Where(x => x.IsHidden);
        if (subjectId is not null) q = q.Where(x => x.SubjectId == subjectId);
        if (gradeLevel is not null) q = q.Where(x => x.GradeLevel == gradeLevel);
        if (mine) q = q.Where(x => x.CreatedByPersonId == me);
        if (status is QuestionStatuses.Draft or QuestionStatuses.Ready) q = q.Where(x => x.Status == status);
        var topicText = TextNormalizer.Normalize(topic);
        if (topicText.Length > 0) q = q.Where(x => x.Topic != null && x.Topic.Contains(topicText));
        var creatorText = TextNormalizer.Normalize(creator);
        if (creatorText.Length > 0) q = q.Where(x => x.CreatedByPerson.FullName.Contains(creatorText));
        var searchText = TextNormalizer.Normalize(search);
        if (searchText.Length > 0) q = q.Where(x => x.Content.Contains(searchText));

        return await q
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.QuestionId)
            .Select(x => new QuestionItemDto(
                x.QuestionId, x.SubjectId, x.Subject.Name, x.GradeLevel, x.Topic, x.Content, x.Status, x.IsAiGenerated, x.IsHidden,
                x.QuestionOption.Count(o => o.IsCorrect) > 1,
                x.CreatedByPersonId, x.CreatedByPerson.FullName, x.CreatedByPersonId == me,
                x.AssignmentQuestion.Any(), x.CreatedAt))
            .ToPagedAsync(page, pageSize);
    }

    public async Task<QuestionDetailDto> GetAsync(int id)
    {
        var q = await LoadVisibleAsync(id);
        return await ToDetailAsync(q);
    }

    public async Task<QuestionDetailDto> CreateAsync(SaveQuestionDto dto)
    {
        var input = await ValidateAsync(dto);
        var q = new Question
        {
            SubjectId = input.SubjectId, GradeLevel = input.GradeLevel, Topic = input.Topic, Content = input.Content,
            Explanation = input.Explanation, Status = QuestionStatuses.Ready, IsAiGenerated = false, IsHidden = false,
            CreatedByPersonId = user.PersonId, CreatedAt = clock.Now,
        };
        foreach (var o in input.Options) q.QuestionOption.Add(new QuestionOption { OptionLabel = o.Label, Content = o.Content, IsCorrect = o.IsCorrect });
        db.Question.Add(q);
        await db.SaveChangesAsync();
        return await ToDetailAsync(await LoadVisibleAsync(q.QuestionId));
    }

    /// <summary>Sửa câu của mình, kể cả sửa nháp AI (4.6.6). Hàng câu hỏi bị khóa trước khi kiểm để không bị giao vào bài giữa chừng.</summary>
    public async Task<QuestionDetailDto> UpdateAsync(int id, SaveQuestionDto dto)
    {
        var input = await ValidateAsync(dto);
        await using var tx = await db.Database.BeginTransactionAsync();
        var q = await db.Question.Include(x => x.QuestionOption).FirstOrDefaultAsync(x => x.QuestionId == id)
            ?? throw ApiException.NotFound("Không tìm thấy câu hỏi.");
        EnsureOwner(q, "sửa");
        await LockQuestionsAsync([id]);
        if (await IsLockedAsync(id))
            throw ApiException.BadRequest("Câu hỏi đã nằm trong một bài tập nên không sửa được. Chỉ còn ẩn khỏi ngân hàng, hoặc soạn câu mới.");

        q.SubjectId = input.SubjectId;
        q.GradeLevel = input.GradeLevel;
        q.Topic = input.Topic;
        q.Content = input.Content;
        q.Explanation = input.Explanation;
        q.UpdatedAt = clock.Now;
        foreach (var o in input.Options)
        {
            var existing = q.QuestionOption.First(x => x.OptionLabel == o.Label);
            existing.Content = o.Content;
            existing.IsCorrect = o.IsCorrect;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return await ToDetailAsync(await LoadVisibleAsync(id));
    }

    /// <summary>Ẩn khỏi ngân hàng (4.6.4): không ảnh hưởng bài cũ, không đưa vào bài mới được nữa.</summary>
    public async Task HideAsync(int id)
    {
        var q = await db.Question.FirstOrDefaultAsync(x => x.QuestionId == id) ?? throw ApiException.NotFound("Không tìm thấy câu hỏi.");
        EnsureOwner(q, "ẩn");
        if (q.Status == QuestionStatuses.Draft)
            throw ApiException.BadRequest("Câu nháp không cần ẩn. Xóa nháp nếu không dùng.");
        if (q.IsHidden) return;
        q.IsHidden = true;
        q.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
    }

    /// <summary>Bỏ ẩn (4.6.4): câu trở lại ngân hàng chung, người khác đưa vào bài mới được như trước.</summary>
    public async Task UnhideAsync(int id)
    {
        var q = await db.Question.FirstOrDefaultAsync(x => x.QuestionId == id) ?? throw ApiException.NotFound("Không tìm thấy câu hỏi.");
        EnsureOwner(q, "bỏ ẩn");
        if (!q.IsHidden) return;
        q.IsHidden = false;
        q.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
    }

    /// <summary>Xác nhận nháp AI thành câu Sẵn dùng (4.6.7). Chỉ câu Sẵn dùng mới đưa vào bài tập được.</summary>
    public async Task ConfirmAsync(int id)
    {
        var q = await db.Question.Include(x => x.QuestionOption).FirstOrDefaultAsync(x => x.QuestionId == id)
            ?? throw ApiException.NotFound("Không tìm thấy câu hỏi.");
        EnsureOwner(q, "xác nhận");
        if (q.Status != QuestionStatuses.Draft) throw ApiException.BadRequest("Câu hỏi đã ở trạng thái Sẵn dùng.");
        var error = QuizRules.CheckOptions(q.QuestionOption.OrderBy(o => o.OptionLabel)
            .Select(o => new OptionInput(o.OptionLabel, o.Content, o.IsCorrect)).ToList());
        if (error is not null) throw ApiException.BadRequest($"{error} Sửa nháp rồi xác nhận lại.");
        var rows = await db.Question.Where(x => x.QuestionId == id && x.Status == QuestionStatuses.Draft)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, QuestionStatuses.Ready).SetProperty(x => x.UpdatedAt, clock.Now));
        if (rows == 0) throw ApiException.Conflict("Câu hỏi vừa được thay đổi. Tải lại danh sách rồi thử lại.");
    }

    /// <summary>Xóa nháp (4.6.8). Chỉ xóa câu Nháp của mình; câu Sẵn dùng chỉ ẩn được.</summary>
    public async Task DeleteDraftAsync(int id)
    {
        var q = await db.Question.AsNoTracking().FirstOrDefaultAsync(x => x.QuestionId == id) ?? throw ApiException.NotFound("Không tìm thấy câu hỏi.");
        EnsureOwner(q, "xóa");
        if (q.Status != QuestionStatuses.Draft)
            throw ApiException.BadRequest("Chỉ xóa được câu Nháp. Câu Sẵn dùng chỉ ẩn khỏi ngân hàng.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.QuestionOption.Where(o => o.QuestionId == id).ExecuteDeleteAsync();
        var rows = await db.Question.Where(x => x.QuestionId == id && x.Status == QuestionStatuses.Draft).ExecuteDeleteAsync();
        if (rows == 0) throw ApiException.Conflict("Câu hỏi vừa được thay đổi. Tải lại danh sách rồi thử lại.");
        await tx.CommitAsync();
    }

    // ---- dùng chung với các service LRN khác ----

    /// <summary>Câu đã nằm trong một bài tập chưa. Bài giao ngay nên có mặt trong bất kỳ bài nào là bị khóa sửa.</summary>
    public Task<bool> IsLockedAsync(int questionId) =>
        db.AssignmentQuestion.AnyAsync(aq => aq.QuestionId == questionId);

    /// <summary>
    /// Cập nhật rỗng để giữ khóa hàng của các câu hỏi tới hết transaction: giao bài đang đưa câu này vào bài sẽ phải chờ
    /// (và ngược lại), nên không có chuyện câu bị sửa sau khi đã nằm trong bài.
    /// </summary>
    public Task LockQuestionsAsync(IReadOnlyCollection<int> questionIds) =>
        db.Question.Where(q => questionIds.Contains(q.QuestionId))
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.IsHidden, q => q.IsHidden));

    public static string? CheckBook(string? textbook) => textbook is "Kết nối tri thức với cuộc sống" or "Chân trời sáng tạo" or "Cánh diều"
        ? textbook : null;

    private void EnsureOwner(Question q, string action)
    {
        if (q.CreatedByPersonId != user.PersonId)
            throw ApiException.Forbidden($"Chỉ người tạo câu hỏi mới {action} được. Soạn câu mới nếu cần phiên bản khác.");
    }

    private async Task<Question> LoadVisibleAsync(int id)
    {
        var q = await db.Question.AsNoTracking()
            .Include(x => x.Subject).Include(x => x.CreatedByPerson).Include(x => x.QuestionOption)
            .FirstOrDefaultAsync(x => x.QuestionId == id);
        var visible = q is not null && (q.CreatedByPersonId == user.PersonId || (q.Status == QuestionStatuses.Ready && !q.IsHidden));
        return visible ? q! : throw ApiException.NotFound("Không tìm thấy câu hỏi.");
    }

    internal async Task<QuestionDetailDto> ToDetailAsync(Question q) =>
        ToDetail(q, user.PersonId, await IsLockedAsync(q.QuestionId));

    internal static QuestionDetailDto ToDetail(Question q, int me, bool locked) => new(
        q.QuestionId, q.SubjectId, q.Subject.Name, q.GradeLevel, q.Topic, q.Content, q.Explanation, q.Status, q.IsAiGenerated, q.IsHidden,
        q.QuestionOption.Count(o => o.IsCorrect) > 1, q.CreatedByPersonId, q.CreatedByPerson.FullName, q.CreatedByPersonId == me, locked, q.CreatedAt,
        q.QuestionOption.OrderBy(o => o.OptionLabel).Select(o => new OptionDto(o.OptionId, o.OptionLabel, o.Content, o.IsCorrect)).ToList());

    private sealed record Valid(int SubjectId, byte GradeLevel, string Topic, string Content, string? Explanation, List<OptionInput> Options);

    private async Task<Valid> ValidateAsync(SaveQuestionDto dto)
    {
        if (!await db.Subject.AnyAsync(s => s.SubjectId == dto.SubjectId && s.IsActive))
            throw ApiException.BadRequest("Môn học không tồn tại hoặc đã ngừng hoạt động. Chọn môn khác.");
        await EnsureOwnSubjectAsync(dto.SubjectId);
        if (dto.GradeLevel is < QuizRules.MinGrade or > QuizRules.MaxGrade) throw ApiException.BadRequest("Khối phải từ 6 đến 9. Chọn lại khối.");
        var topic = Guard.Required(dto.Topic, "Chủ đề", 150);
        var content = Guard.Required(dto.Content, "Nội dung câu hỏi", QuizRules.ContentMaxLength);
        var explanation = Guard.Optional(dto.Explanation, "Giải thích", QuizRules.ContentMaxLength);
        var options = (dto.Options ?? []).Select(o => new OptionInput(o.Label, TextNormalizer.Normalize(o.Content), o.IsCorrect)).ToList();
        var error = QuizRules.CheckOptions(options);
        if (error is not null) throw ApiException.BadRequest(error);
        return new Valid(dto.SubjectId, dto.GradeLevel, topic, content, explanation, options);
    }
}
