using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Ai;
using MSMS.Api.Common.Text;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Dtos;

namespace MSMS.Api.Modules.Lrn.Services;

public static class AnalysisStatuses
{
    public const string Pending = "Pending";
    public const string Done = "Done";
    public const string Failed = "Failed";
}

/// <summary>
/// Đẩy việc phân tích đi sau khi bài nộp đã chốt, để học sinh thấy điểm ngay mà không chờ AI (BR-23). Chạy nền trong tiến trình,
/// mỗi việc một scope riêng. Tiến trình dừng giữa chừng thì dòng phân tích kẹt ở Pending; phía đọc coi là Lỗi sau
/// <see cref="AnalysisService.StaleAfter"/> (xem <see cref="ResultService"/>).
/// </summary>
public sealed class AnalysisJob(IServiceScopeFactory scopes, IHostApplicationLifetime lifetime, ILogger<AnalysisJob> logger)
{
    public void Enqueue(long submissionId) => _ = Task.Run(async () =>
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AnalysisService>().RunAsync(submissionId, lifetime.ApplicationStopping);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Phân tích AI của bài nộp {SubmissionId} lỗi ngoài dự kiến", submissionId);
        }
    });
}

/// <summary>
/// LRN-04: MSMS AI phân tích một bài nộp (BR-23). Chỉ nhận nội dung câu hỏi, các lựa chọn, đáp án đúng, lựa chọn của học sinh và điểm,
/// tuyệt đối không có tên, mã học sinh, lớp hay trường (mục 3.1). Kết quả được kiểm lại rồi mới lưu; AI lỗi thì để Lỗi,
/// không thử lại, không ảnh hưởng điểm.
/// </summary>
public sealed class AnalysisService(MsmsDbContext db, IAiClient ai, IClock clock, ILogger<AnalysisService> logger)
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);

    // Nhiều học sinh nộp cùng lúc không gọi AI ồ ạt: quá hạn mức của nhà cung cấp chỉ làm phân tích Lỗi, không làm hỏng việc khác.
    private static readonly SemaphoreSlim Gate = new(3, 3);

    private const int MaxSummary = 300, MaxItem = 160, MaxWhy = 240, MaxItems = 4, MaxMistakes = 8;

    private const string SystemInstruction =
        "Bạn là MSMS AI, trợ lý học tập thân thiện của một trường THCS Việt Nam. Bạn phân tích một bài trắc nghiệm rèn luyện của học sinh. " +
        "Chỉ trả về JSON hợp lệ đúng cấu trúc yêu cầu, không thêm lời dẫn, không dùng markdown. Viết bằng tiếng Việt, xưng hô với học sinh là \"bạn\", " +
        "giọng khích lệ, cụ thể, ngắn gọn. Không bịa kiến thức ngoài các câu hỏi đã cho.";

    public async Task RunAsync(long submissionId, CancellationToken ct)
    {
        string? failure = null;
        string? content = null;
        try
        {
            var prompt = await BuildPromptAsync(submissionId, ct);
            if (prompt is null) return;
            string raw;
            await Gate.WaitAsync(ct);
            try { raw = await ai.GenerateJsonAsync(SystemInstruction, prompt.Text, ct); }
            finally { Gate.Release(); }
            content = Parse(raw, prompt.WrongNumbers, out failure);
        }
        catch (AiException ex)
        {
            failure = ex.Message;
            logger.LogWarning(ex, "AI lỗi khi phân tích bài nộp {SubmissionId}", submissionId);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            failure = "Phân tích gặp lỗi ngoài dự kiến.";
            logger.LogError(ex, "Phân tích bài nộp {SubmissionId} lỗi", submissionId);
        }

        var status = content is null ? AnalysisStatuses.Failed : AnalysisStatuses.Done;
        var error = content is null ? Truncate(failure ?? "AI trả kết quả không hợp lệ.", 500) : null;
        var now = clock.Now;
        await db.AiFeedback.Where(f => f.SubmissionId == submissionId && f.Status == AnalysisStatuses.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Status, status).SetProperty(f => f.AiContent, content)
                .SetProperty(f => f.ErrorMessage, error).SetProperty(f => f.GeneratedAt, now), CancellationToken.None);
    }

    /// <summary>Phân tích để hiển thị: đọc nội dung đã lưu. Kẹt ở Pending quá lâu thì <see cref="ResultService"/> đã đổi thành Lỗi.</summary>
    public static AnalysisDto ToDto(string status, string? content)
    {
        if (status != AnalysisStatuses.Done || content is null) return new AnalysisDto(status, null, [], [], [], []);
        try
        {
            var c = JsonSerializer.Deserialize<Stored>(content, Json);
            if (c is null) return new AnalysisDto(AnalysisStatuses.Failed, null, [], [], [], []);
            return new AnalysisDto(AnalysisStatuses.Done, c.Summary, c.Strengths ?? [], c.Weaknesses ?? [],
                (c.Mistakes ?? []).Select(m => new AnalysisMistakeDto(m.QuestionNo, m.Why)).ToList(), c.Suggestions ?? []);
        }
        catch (JsonException)
        {
            return new AnalysisDto(AnalysisStatuses.Failed, null, [], [], [], []);
        }
    }

    // ---- dựng dữ liệu gửi AI ----

    private sealed record PromptData(string Text, HashSet<int> WrongNumbers);

    private async Task<PromptData?> BuildPromptAsync(long submissionId, CancellationToken ct)
    {
        var sub = await db.Submission.AsNoTracking().Where(s => s.SubmissionId == submissionId && s.SubmittedAt != null)
            .Select(s => new { s.AssignmentId, s.Score, s.CorrectCount, Subject = s.Assignment.Subject.Name, Grade = s.Assignment.Class.GradeLevel })
            .FirstOrDefaultAsync(ct);
        if (sub is null) return null;

        var lines = await db.AssignmentQuestion.AsNoTracking().Include(x => x.Question).ThenInclude(q => q.QuestionOption)
            .Where(x => x.AssignmentId == sub.AssignmentId).OrderBy(x => x.OrderNo).ToListAsync(ct);
        var answers = await db.SubmissionAnswer.AsNoTracking().Where(x => x.SubmissionId == submissionId)
            .Select(x => new { x.AssignmentQuestionId, x.IsCorrect, Selected = x.SubmissionAnswerOption.Select(o => o.OptionId).ToList() })
            .ToListAsync(ct);

        var wrong = new HashSet<int>();
        var sb = new StringBuilder();
        sb.AppendLine($"Môn {sub.Subject}, lớp {sub.Grade}. Kết quả: {sub.CorrectCount}/{lines.Count} câu đúng, điểm {sub.Score:0.##}/10.");
        sb.AppendLine("Chi tiết từng câu:");
        foreach (var line in lines)
        {
            var ans = answers.FirstOrDefault(a => a.AssignmentQuestionId == line.AssignmentQuestionId);
            var selected = ans?.Selected ?? [];
            var isCorrect = ans?.IsCorrect == true;
            if (!isCorrect) wrong.Add(line.OrderNo);
            var options = line.Question.QuestionOption.OrderBy(o => o.OptionLabel).ToList();
            sb.AppendLine($"Câu {line.OrderNo} ({(isCorrect ? "đúng" : "sai")}): {line.Question.Content}");
            foreach (var o in options) sb.AppendLine($"  {o.OptionLabel}. {o.Content}");
            sb.AppendLine($"  Đáp án đúng: {string.Join(", ", options.Where(o => o.IsCorrect).Select(o => o.OptionLabel))}.");
            var chosen = options.Where(o => selected.Contains(o.OptionId)).Select(o => o.OptionLabel).ToList();
            sb.AppendLine($"  Học sinh chọn: {(chosen.Count == 0 ? "bỏ trống" : string.Join(", ", chosen))}.");
            if (!string.IsNullOrWhiteSpace(line.Question.Explanation)) sb.AppendLine($"  Gợi ý giải thích của giáo viên: {line.Question.Explanation}");
        }
        sb.AppendLine();
        sb.AppendLine("Hãy phân tích bài làm này và trả về JSON dạng:");
        sb.AppendLine("{\"summary\":\"nhận xét chung 1-2 câu\",\"strengths\":[\"điểm mạnh\"],\"weaknesses\":[\"điểm yếu\"]," +
                      "\"mistakes\":[{\"questionNo\":3,\"why\":\"vì sao lựa chọn của bạn chưa đúng và cách nhớ đúng\"}],\"suggestions\":[\"kiến thức cần ôn tiếp\"]}");
        sb.AppendLine($"Mỗi danh sách tối đa {MaxItems} mục; mistakes chỉ gồm các câu sai (tối đa {MaxMistakes}), questionNo là số câu như trên. " +
                      "Nếu làm đúng hết thì strengths nêu điểm mạnh, weaknesses và mistakes để rỗng, suggestions gợi ý hướng nâng cao.");
        return new PromptData(sb.ToString(), wrong);
    }

    // ---- kiểm kết quả AI ----

    private sealed record Mistake(int QuestionNo, string Why);
    private sealed record Stored(string? Summary, List<string>? Strengths, List<string>? Weaknesses, List<Mistake>? Mistakes, List<string>? Suggestions);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Đọc kết quả AI; chuỗi dài quá thì cắt, mục lạ (câu không sai, số câu không có) thì bỏ. Rỗng hoàn toàn là Lỗi.</summary>
    private static string? Parse(string raw, HashSet<int> wrongNumbers, out string? failure)
    {
        failure = null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { failure = "AI trả kết quả không đúng cấu trúc."; return null; }

            var summary = Cut(Str(root, "summary"), MaxSummary);
            var strengths = List(root, "strengths");
            var weaknesses = List(root, "weaknesses");
            var suggestions = List(root, "suggestions");
            var mistakes = new List<Mistake>();
            if (root.TryGetProperty("mistakes", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in arr.EnumerateArray())
                {
                    if (m.ValueKind != JsonValueKind.Object || !m.TryGetProperty("questionNo", out var no) || !no.TryGetInt32(out var number)) continue;
                    var why = Cut(Str(m, "why"), MaxWhy);
                    if (why.Length == 0 || !wrongNumbers.Contains(number) || mistakes.Any(x => x.QuestionNo == number)) continue;
                    mistakes.Add(new Mistake(number, why));
                    if (mistakes.Count == MaxMistakes) break;
                }
            }
            if (summary.Length == 0 && strengths.Count == 0 && weaknesses.Count == 0 && mistakes.Count == 0 && suggestions.Count == 0)
            {
                failure = "AI trả kết quả rỗng.";
                return null;
            }
            return JsonSerializer.Serialize(new Stored(summary.Length == 0 ? null : summary, strengths, weaknesses,
                mistakes.OrderBy(m => m.QuestionNo).ToList(), suggestions), Json);
        }
        catch (JsonException)
        {
            failure = "AI trả kết quả không phải JSON hợp lệ.";
            return null;
        }
    }

    private static string Str(JsonElement e, string name) =>
        TextNormalizer.Normalize(e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null);

    private static List<string> List(JsonElement e, string name)
    {
        var result = new List<string>();
        if (!e.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var text = Cut(TextNormalizer.Normalize(item.GetString()), MaxItem);
            if (text.Length > 0) result.Add(text);
            if (result.Count == MaxItems) break;
        }
        return result;
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..(max - 1)].TrimEnd() + "…";
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
