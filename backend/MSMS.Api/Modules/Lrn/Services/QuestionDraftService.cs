using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Ai;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Text;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Dtos;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>
/// LRN-01 (4.6.5): AI sinh nháp câu hỏi. AI chỉ nhận môn, khối, chủ đề, bộ sách và số câu; kết quả là câu Nháp gắn cờ AI
/// của giáo viên đang gọi. Backend loại câu không đủ 4 lựa chọn hoặc ngoài 1–3 đáp án đúng. AI hỏng thì giáo viên soạn thủ công,
/// không ảnh hưởng luồng nào khác (mục 3.1, BR-20).
/// </summary>
public sealed class QuestionDraftService(MsmsDbContext db, IAiClient ai, CurrentUser user, IClock clock, ILogger<QuestionDraftService> logger)
{
    public const int MaxCount = 10;

    private const string SystemInstruction =
        "Bạn là giáo viên Việt Nam giàu kinh nghiệm, soạn câu hỏi trắc nghiệm cho học sinh phổ thông. " +
        "Chỉ trả về JSON hợp lệ đúng cấu trúc yêu cầu, không thêm lời dẫn, không dùng markdown. " +
        "Viết bằng tiếng Việt, đúng kiến thức, bám sát chương trình và sách giáo khoa được nêu.";

    public async Task<GenerateQuestionsResultDto> GenerateAsync(GenerateQuestionsDto dto, CancellationToken ct)
    {
        var subject = await db.Subject.AsNoTracking().FirstOrDefaultAsync(s => s.SubjectId == dto.SubjectId && s.IsActive)
            ?? throw ApiException.BadRequest("Môn học không tồn tại hoặc đã ngừng hoạt động. Chọn môn khác.");
        if (dto.GradeLevel is < QuizRules.MinGrade or > QuizRules.MaxGrade) throw ApiException.BadRequest("Khối phải từ 6 đến 9. Chọn lại khối.");
        var topic = Guard.Required(dto.Topic, "Chủ đề", 150);
        var textbook = QuestionService.CheckBook(TextNormalizer.Normalize(dto.Textbook))
            ?? throw ApiException.BadRequest("Chọn một trong ba bộ sách: Kết nối tri thức với cuộc sống, Chân trời sáng tạo, Cánh diều.");
        if (dto.Count < 1 || dto.Count > MaxCount) throw ApiException.BadRequest($"Số câu mỗi lần sinh từ 1 đến {MaxCount}.");

        var prompt =
            $"Soạn {dto.Count} câu hỏi trắc nghiệm.\n" +
            $"- Môn: {subject.Name}\n- Khối: {dto.GradeLevel}\n- Chủ đề: {topic}\n- Bộ sách: {textbook}\n" +
            "Mỗi câu có đúng 4 lựa chọn A, B, C, D và từ 1 đến 3 đáp án đúng; câu có nhiều đáp án đúng phải nêu rõ trong nội dung câu hỏi. " +
            "Các lựa chọn sai phải hợp lý, không trùng nhau. Giải thích ngắn gọn vì sao đáp án đúng là đúng.\n" +
            "Trả về JSON dạng: {\"questions\":[{\"content\":\"...\",\"explanation\":\"...\",\"options\":[" +
            "{\"label\":\"A\",\"content\":\"...\",\"isCorrect\":false},{\"label\":\"B\",\"content\":\"...\",\"isCorrect\":true}," +
            "{\"label\":\"C\",\"content\":\"...\",\"isCorrect\":false},{\"label\":\"D\",\"content\":\"...\",\"isCorrect\":false}]}]}";

        string raw;
        try { raw = await ai.GenerateJsonAsync(SystemInstruction, prompt, ct); }
        catch (AiException ex) { throw ApiException.BadRequest($"{ex.Message} Soạn câu hỏi thủ công trong lúc chờ."); }

        var (valid, discarded) = Parse(raw);
        if (valid.Count == 0)
            throw ApiException.BadRequest("AI không trả về câu hỏi hợp lệ. Thử lại, hoặc soạn câu hỏi thủ công.");
        if (valid.Count > dto.Count) { discarded += valid.Count - dto.Count; valid = valid.Take(dto.Count).ToList(); }

        var now = clock.Now;
        var created = new List<Question>();
        foreach (var v in valid)
        {
            var q = new Question
            {
                SubjectId = subject.SubjectId, GradeLevel = dto.GradeLevel, Topic = topic, Content = v.Content, Explanation = v.Explanation,
                Status = QuestionStatuses.Draft, IsAiGenerated = true, IsHidden = false, CreatedByPersonId = user.PersonId, CreatedAt = now,
            };
            foreach (var o in v.Options) q.QuestionOption.Add(new QuestionOption { OptionLabel = o.Label, Content = o.Content, IsCorrect = o.IsCorrect });
            created.Add(q);
        }
        db.Question.AddRange(created);
        await db.SaveChangesAsync();

        var ids = created.Select(c => c.QuestionId).ToList();
        var saved = await db.Question.AsNoTracking()
            .Include(x => x.Subject).Include(x => x.CreatedByPerson).Include(x => x.QuestionOption)
            .Where(x => ids.Contains(x.QuestionId)).OrderBy(x => x.QuestionId).ToListAsync();
        return new GenerateQuestionsResultDto(saved.Select(q => QuestionService.ToDetail(q, user.PersonId, false)).ToList(), discarded);
    }

    private sealed record Candidate(string Content, string? Explanation, List<OptionInput> Options);

    /// <summary>Đọc kết quả AI, giữ lại câu hợp lệ theo BR-20 và đếm số câu bị loại.</summary>
    private (List<Candidate> Valid, int Discarded) Parse(string raw)
    {
        var valid = new List<Candidate>();
        var discarded = 0;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("questions", out var arr) || arr.ValueKind != JsonValueKind.Array) return (valid, 0);
            foreach (var item in arr.EnumerateArray())
            {
                var candidate = ToCandidate(item);
                if (candidate is null) discarded++; else valid.Add(candidate);
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "AI trả JSON không hợp lệ khi sinh câu hỏi");
        }
        return (valid, discarded);
    }

    private static Candidate? ToCandidate(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;
        var content = TextNormalizer.Normalize(Str(item, "content"));
        if (content.Length == 0 || content.Length > QuizRules.ContentMaxLength) return null;
        var explanation = TextNormalizer.Normalize(Str(item, "explanation"));
        if (explanation.Length > QuizRules.ContentMaxLength) return null;
        if (!item.TryGetProperty("options", out var opts) || opts.ValueKind != JsonValueKind.Array) return null;

        var options = new List<OptionInput>();
        var index = 0;
        foreach (var o in opts.EnumerateArray())
        {
            if (index >= QuizRules.OptionLabels.Length || o.ValueKind != JsonValueKind.Object) return null;
            var isCorrect = o.TryGetProperty("isCorrect", out var c) && c.ValueKind == JsonValueKind.True;
            // Nhãn lấy theo thứ tự, không tin nhãn AI ghi: Backend luôn lưu A, B, C, D.
            options.Add(new OptionInput(QuizRules.OptionLabels[index++], TextNormalizer.Normalize(Str(o, "content")), isCorrect));
        }
        if (QuizRules.CheckOptions(options) is not null) return null;
        return new Candidate(content, explanation.Length == 0 ? null : explanation, options);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
