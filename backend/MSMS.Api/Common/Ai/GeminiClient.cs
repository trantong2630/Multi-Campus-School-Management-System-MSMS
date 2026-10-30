using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MSMS.Api.Common.Ai;

/// <summary>Gọi Gemini bằng HttpClient + REST (Conventions §1). Thử lại một lần khi bị giới hạn lượt gọi hoặc máy chủ AI quá tải.</summary>
public sealed class GeminiClient(HttpClient http, IOptions<AiOptions> options, ILogger<GeminiClient> logger) : IAiClient
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public async Task<string> GenerateJsonAsync(string systemInstruction, string prompt, CancellationToken ct = default)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiKey) || string.IsNullOrWhiteSpace(o.Model))
            throw new AiException("Chưa cấu hình AI. Liên hệ quản trị để cấu hình khóa và model.");

        var body = JsonSerializer.Serialize(new
        {
            systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json", temperature = 0.4, maxOutputTokens = o.MaxOutputTokens },
        });
        var url = $"{o.BaseUrl.TrimEnd('/')}/models/{o.Model}:generateContent";

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("x-goog-api-key", o.ApiKey);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSeconds));
            try
            {
                using var response = await http.SendAsync(request, timeout.Token);
                var text = await response.Content.ReadAsStringAsync(timeout.Token);
                if (response.IsSuccessStatusCode) return ExtractText(text);

                var retryable = response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable;
                logger.LogWarning("AI trả {Status} (lần {Attempt}): {Body}", (int)response.StatusCode, attempt, Truncate(text));
                if (retryable && attempt == 1) { await Task.Delay(RetryDelay, ct); continue; }
                var keyRejected = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || text.Contains("API_KEY_INVALID", StringComparison.Ordinal);
                throw new AiException(keyRejected ? "AI từ chối khóa truy cập. Liên hệ quản trị kiểm tra cấu hình." : response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests => "AI đang hết hạn mức gọi. Chờ một lúc rồi chạy lại.",
                    HttpStatusCode.ServiceUnavailable => "AI đang quá tải. Chờ một lúc rồi chạy lại.",
                    _ => $"AI trả lỗi ({(int)response.StatusCode}). Chạy lại sau, nếu vẫn lỗi thì liên hệ quản trị.",
                });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new AiException("AI phản hồi quá lâu. Chạy lại sau.");
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Không kết nối được AI (lần {Attempt})", attempt);
                if (attempt == 1) { await Task.Delay(RetryDelay, ct); continue; }
                throw new AiException("Không kết nối được AI. Kiểm tra mạng rồi chạy lại.", ex);
            }
        }
    }

    /// <summary>Gộp các đoạn văn bản của ứng viên đầu tiên, bỏ đoạn suy nghĩ nếu model trả kèm.</summary>
    private static string ExtractText(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var candidate = doc.RootElement.GetProperty("candidates")[0];
            var sb = new StringBuilder();
            foreach (var part in candidate.GetProperty("content").GetProperty("parts").EnumerateArray())
            {
                if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True) continue;
                if (part.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String) sb.Append(t.GetString());
            }
            if (sb.Length == 0) throw new AiException("AI không trả về nội dung. Chạy lại sau.");
            return sb.ToString();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new AiException("AI trả về dữ liệu không đọc được. Chạy lại sau.", ex);
        }
    }

    private static string Truncate(string s) => s.Length <= 400 ? s : s[..400];
}
