using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using MSMS.Api.Common.Ai;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Modules.Adm.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Adm.Ai;

/// <summary>
/// ADM-01 MSMS AI cho Hiệu trưởng, Hiệu phó: hỏi đáp số liệu chuyên cần, bài tập rèn luyện, giáo viên vắng, tải dạy.
/// Chỉ đọc; không lưu hội thoại. AI chỉ thấy số tổng hợp và mã giả HS#, GV#; tên được đổi lại ở Backend trước khi trả về.
/// </summary>
public sealed class AdmAiChatService(MsmsDbContext db, CurrentUser user, ScopeResolver scopeResolver, IClock clock, IAiClient ai,
    IOptions<AiOptions> options, SubjectResponsibilityService responsibility, ILogger<AdmAiChatService> logger)
{
    public const int MaxQuestionLength = 1000;
    private const int MaxUserTurnLength = 2000, MaxAiTurnLength = 4000, MaxCharts = 2;

    public async Task<AiChatResponse> AskAsync(AiChatRequest request, CancellationToken ct = default)
    {
        var question = request.Question?.Trim() ?? "";
        if (question.Length == 0) throw ApiException.BadRequest("Nhập câu hỏi cho MSMS AI.");
        if (question.Length > MaxQuestionLength) throw ApiException.BadRequest($"Câu hỏi dài quá {MaxQuestionLength:N0} ký tự. Rút gọn rồi hỏi lại.");

        var o = options.Value;
        var scope = await AiScope.LoadAsync(db, user, scopeResolver, clock);
        var directory = await PersonDirectory.LoadAsync(db, scope);
        var tools = new AiTools(db, scope, responsibility, clock);

        var messages = new List<AiChatMessage>();
        foreach (var turn in (request.History ?? []).Where(t => !string.IsNullOrWhiteSpace(t.Text))
                     .TakeLast(Math.Max(0, o.ChatHistoryTurns) * 2).SkipWhile(t => t.Role != "user"))
            messages.Add(turn.Role == "user"
                ? new AiUserText(directory.Mask(Cut(turn.Text, MaxUserTurnLength)))
                : new AiModelText(Cut(turn.Text, MaxAiTurnLength)));
        messages.Add(new AiUserText(directory.Mask(question)));

        var system = SystemPrompt(scope);
        var declarations = AiTools.Declarations();
        var results = new List<JsonObject>();
        var charts = new List<AiChartDto>();
        var steps = new List<string>();
        string? answer = null;
        try
        {
            for (var round = 0; answer is null; round++)
            {
                var allowTools = round < Math.Max(0, o.ChatMaxToolRounds);
                var reply = await ai.ChatAsync(system, messages, declarations, allowTools, ct);
                if (reply.Calls.Count == 0) { answer = reply.Text; break; }
                if (!allowTools) break;

                messages.Add(new AiModelToolCalls(reply.Calls, reply.ProviderState));
                var outputs = new List<AiToolResult>();
                foreach (var call in reply.Calls)
                {
                    var label = AiTools.StepLabel(call.Name);
                    if (call.Name != AiTools.ShowChart && !steps.Contains(label)) steps.Add(label);
                    outputs.Add(new AiToolResult(call.Name, call.Name == AiTools.ShowChart
                        ? Chart(call.Args, results, charts)
                        : await RunToolAsync(tools, call, results), call.Id));
                }
                messages.Add(new AiToolResults(outputs));
            }
        }
        catch (AiException ex)
        {
            throw ApiException.BadRequest(ex.Message);
        }
        if (string.IsNullOrWhiteSpace(answer))
            throw ApiException.BadRequest("MSMS AI chưa tổng hợp được câu trả lời. Hỏi gọn hơn hoặc hỏi lại sau.");

        logger.LogInformation("MSMS AI trả lời người {PersonId} sau {Tools} lần gọi tool", user.PersonId, results.Count);
        answer = answer.Trim();
        return new AiChatResponse(directory.Unmask(answer), answer,
            charts.Select(c => c with { Rows = c.Rows.Select(r => r with { Label = directory.Unmask(r.Label) }).ToList() }).ToList(), steps);
    }

    /// <summary>Gợi ý người trong phạm vi cho ô chọn @ (tối đa 10).</summary>
    public async Task<List<AiPersonDto>> PeopleAsync(string? query)
    {
        var scope = await AiScope.LoadAsync(db, user, scopeResolver, clock);
        var directory = await PersonDirectory.LoadAsync(db, scope);
        return directory.Search(query ?? "", 10).Select(p => new AiPersonDto(p.Code, p.Name, p.Detail)).ToList();
    }

    private async Task<JsonObject> RunToolAsync(AiTools tools, AiToolCall call, List<JsonObject> results)
    {
        try
        {
            var result = await tools.RunAsync(call.Name, call.Args);
            results.Add(result);
            // result_id đứng đầu để AI dễ thấy khi gọi show_chart.
            var withId = new JsonObject { ["result_id"] = results.Count };
            foreach (var kv in result.ToList()) withId[kv.Key] = kv.Value?.DeepClone();
            return withId;
        }
        catch (ApiException ex)
        {
            return new JsonObject { ["error"] = ex.Message };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Tool {Tool} của MSMS AI lỗi", call.Name);
            return new JsonObject { ["error"] = "Không tính được số liệu này. Thử hỏi theo cách khác." };
        }
    }

    // ---------------------------------------------------------------- Biểu đồ

    private static readonly HashSet<string> LowerIsBetter =
    [
        "unexcused_rate", "absent_excused", "absent_unexcused", "students_absent", "absent_sessions", "absent_days", "absent_teachers",
        "affected_sessions", "substituted", "cancelled", "pending", "streak_days",
    ];

    /// <summary>Dựng biểu đồ từ đúng các dòng của một kết quả tool; AI chỉ chọn kết quả và chỉ số, không tự đưa số.</summary>
    private static JsonObject Chart(JsonElement args, List<JsonObject> results, List<AiChartDto> charts)
    {
        if (charts.Count >= MaxCharts) return new JsonObject { ["error"] = $"Mỗi câu trả lời tối đa {MaxCharts} biểu đồ." };
        var id = AiArgs.Int(args, "result_id");
        var metric = AiArgs.Str(args, "metric") ?? "";
        var title = AiArgs.Str(args, "title")?.Trim();
        if (id is null || id < 1 || id > results.Count) return new JsonObject { ["error"] = "Không có kết quả với result_id này." };
        var result = results[id.Value - 1];
        if (result["rows"] is not JsonArray rows || rows.Count == 0) return new JsonObject { ["error"] = "Kết quả này không có dòng nào để vẽ." };

        var points = new List<AiChartRowDto>();
        foreach (var row in rows.OfType<JsonObject>())
        {
            var label = (string?)row["label"] ?? "";
            var node = row[metric];
            if (node is not null && node.GetValueKind() != JsonValueKind.Number) return new JsonObject { ["error"] = $"\"{metric}\" không phải trường số." };
            // Số nguyên (số tiết, số lượt) và số thực (tỷ lệ) đều đọc qua chuỗi JSON; ép thẳng JsonValue<int> sang double sẽ lỗi.
            points.Add(new AiChartRowDto(label, node is null ? null : double.Parse(node.ToJsonString(), CultureInfo.InvariantCulture)));
        }
        if (points.All(p => p.Value is null)) return new JsonObject { ["error"] = $"Các dòng không có số \"{metric}\"." };

        var percent = metric.EndsWith("_rate", StringComparison.Ordinal) || metric == "rate";
        double? threshold = metric is "rate" or "attendance_rate" or "class_rate" ? 90 : metric == "submission_rate" ? 50 : null;
        double? max = percent ? 100 : metric.Contains("avg_score", StringComparison.Ordinal) ? 10 : null;
        var kind = (string?)result["group_by"] == "week" ? "line" : "bar";
        charts.Add(new AiChartDto(string.IsNullOrEmpty(title) ? metric : title, kind, percent ? "%" : "", threshold,
            !LowerIsBetter.Contains(metric), max, points));
        return new JsonObject { ["ok"] = true, ["chart"] = charts.Count };
    }

    // ---------------------------------------------------------------- Lời nhắc

    private static string SystemPrompt(AiScope scope)
    {
        var vi = new CultureInfo("vi-VN");
        var sb = new StringBuilder();
        sb.AppendLine($"Bạn là MSMS AI, trợ lý số liệu của {(scope.IsPrincipal ? "Hiệu trưởng" : "Hiệu phó")} một trường THCS Việt Nam.");
        sb.Append($"Hôm nay là {scope.Today.ToString("dddd, dd/MM/yyyy", vi)}");
        if (scope.WeekNo is { } week) sb.Append($", tuần {week} của năm học {scope.Term.YearName}");
        if (scope.Term.SemesterName is { } sem) sb.Append($", {sem} (bắt đầu {scope.Term.SemesterStart:dd/MM/yyyy})");
        sb.AppendLine(".");
        sb.AppendLine($"Phạm vi được xem: {(scope.IsPrincipal ? "toàn trường" : string.Join(", ", scope.Campuses.Select(c => c.Name)))}. Mọi tool đã tự giới hạn theo phạm vi này.");
        sb.AppendLine("""

            Quy tắc:
            1. Chỉ dùng số do tool trả về; không bịa số, không tự cộng trừ lại khi tool đã có sẵn. Không có tool phù hợp hoặc dữ liệu trống thì nói thẳng là chưa có số liệu.
               Trường số để trống nghĩa là không đủ dữ liệu để tính hay để so: không nhắc tới, không tự tính thay (ví dụ không có department_average thì không so với trung bình tổ).
               Chỉ so sánh với mốc (trung bình tổ, kỳ trước, ngưỡng) khi câu hỏi cần hoặc khi chênh lệch đáng chú ý; nói rõ mốc tính trên bao nhiêu người và trong phạm vi nào nếu tool có trả.
               Có same_as_last_not_shown thì nói rõ còn bấy nhiêu người cùng số với dòng cuối mà không hiện; cùng rank là đồng hạng. Tool trả error thì nói lại đúng ý lỗi đó cho thầy cô, không đoán số.
            2. Dòng đầu tiên là phạm vi và kỳ của số liệu, đứng riêng một dòng, đúng dạng "Phạm vi: Toàn trường · 4 tuần qua (27/10–23/11/2026)". Câu từ chối hoặc hỏi lại thì không cần dòng này.
            3. Hệ thống không có dữ liệu nguyên nhân (lý do vắng, hoàn cảnh). Không đoán nguyên nhân; có thể gợi ý thầy cô nên xem thêm gì.
            4. Học sinh và giáo viên chỉ nhắc bằng mã HS#số, GV#số đúng như tool trả về, viết nguyên mã, không đoán tên. Câu hỏi có "HS#1 hoặc HS#2" nghĩa là trùng tên: hỏi lại người dùng muốn xem ai.
            5. Ngưỡng cảnh báo: chuyên cần dưới 90%, tỷ lệ nộp bài dưới 50%. Điểm bài tập thang 10.
            6. Khi so sánh hoặc xếp hạng từ 3 dòng trở lên, hay xem xu hướng theo tuần, luôn gọi show_chart để vẽ từ kết quả tool; biểu đồ đã có số của mọi dòng nên chữ chỉ nêu điều đáng chú ý. Tối đa 2 biểu đồ.
            7. Không hỏi lại khi câu hỏi đã đủ rõ; thiếu kỳ thì dùng mặc định 4 tuần qua. Câu hỏi nối tiếp ("còn khối 9 thì sao?") hiểu theo các lượt trước.
            8. Chỉ trả lời về chuyên cần, bài tập rèn luyện, giáo viên vắng, tải dạy của trường. Câu hỏi khác thì từ chối nhẹ nhàng và nói mình làm được gì.
            9. Trả lời tiếng Việt, xưng "mình", gọi người hỏi là "thầy cô". Sau dòng phạm vi, viết theo thứ tự (phần nào không có thì bỏ):
               - Một câu kết luận nêu điều đáng chú ý nhất, in đậm ý chính bằng **...**.
               - Các số chính, mỗi số một dòng đúng dạng "- Tên chỉ số: **số** ghi chú ngắn", từ 2 đến 4 dòng, ví dụ "- Tỷ lệ nộp: **18,2%** dưới ngưỡng 50%". Tên chỉ số ngắn, không chứa dấu hai chấm.
                 Chỉ dùng số chung của cả phạm vi do tool trả (dòng tổng, summary, total_students...), không lặp lại số của một người hay một nhóm đã có trong danh sách hoặc biểu đồ.
                 Tên chỉ số phải đúng nghĩa của trường số: max là "cao nhất", min là "thấp nhất", average là "trung bình"; ghi chú không khẳng định điều tool không trả.
               - Danh sách người hoặc nhóm (xếp hạng, so sánh) là danh sách đánh số "1. ", mỗi dòng một người hoặc nhóm kèm số chính in đậm.
                 Đã có biểu đồ thì chỉ nêu tối đa 3 dòng đáng chú ý nhất kèm lý do ngắn; không có biểu đồ thì nêu tối đa 10 dòng.
               - Điều nên xem thêm hay giới hạn của dữ liệu ở dòng cuối, bắt đầu bằng "Gợi ý: " hoặc "Lưu ý: ".
               Mỗi đoạn văn tối đa 2 câu. Không dùng tiêu đề #; bảng markdown chỉ khi thật cần và không quá 10 dòng.

            Định nghĩa: chuyên cần = (có mặt + muộn) / lượt đã điểm danh; vắng có phép là vắng có đơn nghỉ được duyệt; tiết bị ảnh hưởng = tiết của giáo viên vắng (dạy thay, hủy, chờ xử lý) và tiết bị hủy, không tính tiết hủy vì ngày nghỉ; tỷ lệ nộp chỉ tính bài đã hết hạn.

            Danh mục (dùng id khi gọi tool):
            """);
        sb.AppendLine("Phân hiệu: " + string.Join("; ", scope.Campuses.Select(c => $"{c.Id} = {c.Name}")));
        foreach (var grade in scope.Classes.GroupBy(c => c.Grade))
            sb.AppendLine($"Khối {grade.Key}: " + string.Join("; ", grade.Select(c =>
                scope.Campuses.Count > 1 ? $"{c.Id} = {c.Name} ({c.CampusName}, {c.Students} HS)" : $"{c.Id} = {c.Name} ({c.Students} HS)")));
        sb.AppendLine("Tổ: " + string.Join("; ", scope.Departments.Select(d => $"{d.Id} = {d.Name}")));
        var departments = scope.Departments.ToDictionary(d => d.Id, d => d.Name);
        sb.AppendLine("Môn: " + string.Join("; ", scope.Subjects.Select(s => $"{s.Id} = {s.Name}{(departments.TryGetValue(s.DepartmentId, out var d) ? $" (tổ {d})" : "")}")));
        return sb.ToString();
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}
