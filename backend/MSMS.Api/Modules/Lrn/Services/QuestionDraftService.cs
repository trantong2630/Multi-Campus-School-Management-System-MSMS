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
/// LRN-01 (4.6.5): AI sinh nháp câu hỏi từ các trang của bài đã chọn trong sách giáo khoa (ảnh trang gửi kèm yêu cầu); kết quả là câu Nháp gắn cờ AI
/// của giáo viên đang gọi. Backend loại câu không đủ 4 lựa chọn hoặc ngoài 1–3 đáp án đúng. AI hỏng thì giáo viên soạn thủ công,
/// không ảnh hưởng luồng nào khác (mục 3.1, BR-20).
/// </summary>
public sealed class QuestionDraftService(MsmsDbContext db, IAiClient ai, QuestionService questions, TextbookService textbooks, CurrentUser user, IClock clock, ILogger<QuestionDraftService> logger)
{
    public const int MaxCount = 10;

    private const string SystemInstruction =
        "Bạn là giáo viên Việt Nam giàu kinh nghiệm, soạn câu hỏi trắc nghiệm cho học sinh phổ thông. " +
        "Chỉ trả về JSON hợp lệ đúng cấu trúc yêu cầu, không thêm lời dẫn, không dùng markdown. " +
        "Viết bằng tiếng Việt. Các ảnh đính kèm là những trang của sách giáo khoa \"Kết nối tri thức với cuộc sống\" (Nhà xuất bản Giáo dục Việt Nam), " +
        "bộ sách được sử dụng thống nhất toàn quốc theo Quyết định 3588/QĐ-BGDĐT ngày 26/12/2025, áp dụng từ năm học 2026–2027. " +
        "Chỉ soạn câu hỏi từ nội dung có trong các trang đó: dùng đúng thuật ngữ, kí hiệu, cách diễn đạt và mức độ kiến thức của sách; " +
        "không đưa kiến thức ngoài các trang, không bịa dữ kiện hay số liệu, không hỏi về hình vẽ hay trang trí mà chữ không mô tả được. " +
        "Mỗi câu hỏi phải tự đứng được: học sinh chỉ thấy câu hỏi và các lựa chọn, không có sách trước mặt. " +
        "Không nhắc đến sách, trang, hình, bảng, phần \"Hoạt động\", \"Thực hành\", \"Luyện tập\" hay số thứ tự bước trong sách (ví dụ không viết \"theo sách giáo khoa\", \"ở Hình 1.2\", \"trong Thực hành 1\"); " +
        "nếu cần dữ kiện thì chép dữ kiện đó thẳng vào câu hỏi. " +
        "Viết toán bằng chữ thường, không dùng LaTeX hay ký hiệu $ và dấu gạch chéo ngược: viết \"hình chữ nhật ABCD\", \"AB = 5 cm\", \"6 cm²\", \"x²\", \"3/4\", \"√9\"; " +
        "dùng các ký hiệu Unicode như ², ³, ×, ÷, ≠, ≤, ≥, ∈, ∉, ⊥, ∥, ∠, √ khi cần.";

    public async Task<GenerateQuestionsResultDto> GenerateAsync(GenerateQuestionsDto dto, CancellationToken ct)
    {
        var subject = await db.Subject.AsNoTracking().FirstOrDefaultAsync(s => s.SubjectId == dto.SubjectId && s.IsActive)
            ?? throw ApiException.BadRequest("Môn học không tồn tại hoặc đã ngừng hoạt động. Chọn môn khác.");
        await questions.EnsureOwnSubjectAsync(subject.SubjectId);
        if (dto.GradeLevel is < QuizRules.MinGrade or > QuizRules.MaxGrade) throw ApiException.BadRequest("Khối phải từ 6 đến 9. Chọn lại khối.");
        var note = Guard.Optional(dto.Note, "Ghi chú", 150);
        var levels = ValidateLevels(dto.Levels);
        var total = levels.Sum(l => l.Count);
        var lesson = await textbooks.GetLessonAsync(dto.LessonId, subject.SubjectId, dto.GradeLevel);
        var images = await textbooks.LoadLessonImagesAsync(lesson, ct);

        var prompt =
            $"Soạn {total} câu hỏi trắc nghiệm từ {images.Count} trang sách đính kèm (từ trang {lesson.StartPage} đến trang {lesson.EndPage}).\n" +
            $"- Môn: {subject.Name}\n- Khối: {dto.GradeLevel}\n- Bài: {lesson.Title} ({lesson.Chapter})\n" +
            (note is null ? "" : $"- Gợi ý thu hẹp trong bài: {note}\n") +
            $"- Số câu theo mức độ nhận thức: {string.Join("; ", levels.Select(l => $"{l.Count} câu {Difficulties.Label(l.Difficulty)}"))}\n" +
            "Mức độ: Nhận biết là nhớ lại, nhận ra kiến thức đã học; Thông hiểu là giải thích, so sánh, diễn đạt lại bằng lời của mình; " +
            "Vận dụng là dùng kiến thức vào tình huống hoặc bài toán quen thuộc; Vận dụng cao là tình huống mới, nhiều bước hoặc phải kết hợp nhiều kiến thức. " +
            "Ghi mức đúng với thực tế của từng câu, không nâng mức cho đủ số lượng: nếu bài không có nội dung đủ khó cho mức nào thì soạn ít câu ở mức đó hơn và vẫn ghi đúng mức của câu. " +
            "Mỗi câu ghi đúng một trong các giá trị difficulty: \"Recognize\" (Nhận biết), \"Understand\" (Thông hiểu), \"Apply\" (Vận dụng), \"HighApply\" (Vận dụng cao), " +
            "đúng số câu đã yêu cầu cho từng mức.\n" +
            "Mỗi câu có đúng 4 lựa chọn A, B, C, D và từ 1 đến 3 đáp án đúng; không ghi gợi ý số đáp án như \"(Chọn 1 hoặc 2 đáp án)\" vào nội dung câu hỏi vì giao diện tự báo. " +
            "Các lựa chọn sai phải hợp lý, không trùng nhau. Giải thích ngắn gọn vì sao đáp án đúng là đúng.\n" +
            "Trả về JSON dạng: {\"questions\":[{\"content\":\"...\",\"difficulty\":\"Understand\",\"explanation\":\"...\",\"options\":[" +
            "{\"label\":\"A\",\"content\":\"...\",\"isCorrect\":false},{\"label\":\"B\",\"content\":\"...\",\"isCorrect\":true}," +
            "{\"label\":\"C\",\"content\":\"...\",\"isCorrect\":false},{\"label\":\"D\",\"content\":\"...\",\"isCorrect\":false}]}]}";

        string raw;
        try { raw = await ai.GenerateJsonAsync(SystemInstruction, prompt, images, ct); }
        catch (AiException ex) { throw ApiException.BadRequest($"{ex.Message} Soạn câu hỏi thủ công trong lúc chờ."); }

        var (valid, discarded) = Parse(raw, logger);
        if (valid.Count == 0)
            throw ApiException.BadRequest("AI không trả về câu hỏi hợp lệ. Thử lại, hoặc soạn câu hỏi thủ công.");
        if (valid.Count > total) { discarded += valid.Count - total; valid = valid.Take(total).ToList(); }
        var difficulties = AssignDifficulties(valid, levels);

        var now = clock.Now;
        var created = new List<Question>();
        for (var i = 0; i < valid.Count; i++)
        {
            var v = valid[i];
            var q = new Question
            {
                SubjectId = subject.SubjectId, GradeLevel = dto.GradeLevel, Topic = Truncate(lesson.Title, 150), LessonId = lesson.LessonId, Content = v.Content, Explanation = v.Explanation, Difficulty = difficulties[i],
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
            .Include(x => x.Lesson).ThenInclude(l => l!.Book)
            .Where(x => ids.Contains(x.QuestionId)).OrderBy(x => x.QuestionId).ToListAsync();
        return new GenerateQuestionsResultDto(saved.Select(q => QuestionService.ToDetail(q, user.PersonId, false)).ToList(), discarded);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static List<DifficultyCountDto> ValidateLevels(List<DifficultyCountDto>? levels)
    {
        var list = (levels ?? []).Where(l => l.Count != 0).ToList();
        if (list.Any(l => !Difficulties.IsValid(l.Difficulty) || l.Count < 0))
            throw ApiException.BadRequest("Số câu theo mức độ không hợp lệ. Nhập lại số câu cho từng mức.");
        if (list.Select(l => l.Difficulty).Distinct().Count() != list.Count)
            throw ApiException.BadRequest("Mỗi mức độ chỉ ghi số câu một lần.");
        var total = list.Sum(l => l.Count);
        if (total < 1 || total > MaxCount) throw ApiException.BadRequest($"Số câu mỗi lần sinh từ 1 đến {MaxCount}. Chỉnh số câu theo mức độ.");
        return list.OrderBy(l => Difficulties.Rank(l.Difficulty)).ToList();
    }

    /// <summary>
    /// Độ khó của từng câu AI trả về. Mức AI ghi hợp lệ thì giữ nguyên, kể cả khi lệch số câu đã yêu cầu cho mức đó: nhãn phải đúng với câu,
    /// không ép câu cơ bản thành Vận dụng cao chỉ để đủ số lượng (bài ít nội dung khó thì có thể ra ít câu khó hơn đã yêu cầu). Chỉ câu AI ghi
    /// thiếu hoặc sai mới lấy mức còn thiếu trong số đã yêu cầu, để mọi nháp đều có độ khó. Giáo viên đổi được lúc duyệt nháp.
    /// </summary>
    internal static List<string> AssignDifficulties(IReadOnlyList<Candidate> candidates, IReadOnlyList<DifficultyCountDto> levels)
    {
        var remaining = levels.ToDictionary(l => l.Difficulty, l => l.Count);
        var result = new string?[candidates.Count];
        for (var i = 0; i < candidates.Count; i++)
        {
            var d = candidates[i].Difficulty;
            if (d is null || !Difficulties.IsValid(d)) continue;
            result[i] = d;
            if (remaining.TryGetValue(d, out var left) && left > 0) remaining[d] = left - 1;
        }
        for (var i = 0; i < result.Length; i++)
        {
            if (result[i] is not null) continue;
            var pick = remaining.Where(r => r.Value > 0).Select(r => r.Key).OrderBy(Difficulties.Rank).FirstOrDefault() ?? levels[0].Difficulty;
            result[i] = pick;
            if (remaining.TryGetValue(pick, out var left) && left > 0) remaining[pick] = left - 1;
        }
        return result.Select(r => r!).ToList();
    }

    internal sealed record Candidate(string Content, string? Explanation, List<OptionInput> Options, string? Difficulty = null);

    /// <summary>Đọc kết quả AI, giữ lại câu hợp lệ theo BR-20 và đếm số câu bị loại.</summary>
    internal static (List<Candidate> Valid, int Discarded) Parse(string raw, ILogger logger)
    {
        var valid = new List<Candidate>();
        var discarded = 0;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            // AI có lúc trả thẳng một mảng thay vì {"questions":[...]}; cả hai đều đọc được, dạng khác thì coi như không có câu nào.
            var root = doc.RootElement;
            var arr = root.ValueKind == JsonValueKind.Array ? root
                : root.ValueKind == JsonValueKind.Object && root.TryGetProperty("questions", out var q) ? q : default;
            if (arr.ValueKind != JsonValueKind.Array) return (valid, 0);
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
        var difficulty = Str(item, "difficulty");
        return new Candidate(content, explanation.Length == 0 ? null : explanation, options, Difficulties.IsValid(difficulty) ? difficulty : null);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
