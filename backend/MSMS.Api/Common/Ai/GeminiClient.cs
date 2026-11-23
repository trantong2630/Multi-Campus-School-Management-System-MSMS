using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace MSMS.Api.Common.Ai;

/// <summary>Gọi Gemini bằng HttpClient + REST (Conventions §1). Thử lại một lần khi bị giới hạn lượt gọi hoặc máy chủ AI quá tải.</summary>
public sealed class GeminiClient(HttpClient http, IOptions<AiOptions> options, ILogger<GeminiClient> logger) : IAiClient
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    public Task<string> GenerateJsonAsync(string systemInstruction, string prompt, CancellationToken ct = default) =>
        GenerateJsonAsync(systemInstruction, prompt, [], ct);

    public async Task<string> GenerateJsonAsync(string systemInstruction, string prompt, IReadOnlyList<AiImage> images, CancellationToken ct = default)
    {
        var o = options.Value;
        // Ảnh đứng trước lời nhắc để AI đọc trang rồi mới nhận yêu cầu.
        var parts = new List<object>();
        foreach (var image in images) parts.Add(new { inline_data = new { mime_type = image.MimeType, data = Convert.ToBase64String(image.Data) } });
        parts.Add(new { text = prompt });
        var body = JsonSerializer.Serialize(new
        {
            systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
            contents = new[] { new { role = "user", parts } },
            generationConfig = new { responseMimeType = "application/json", temperature = 0.4, maxOutputTokens = o.MaxOutputTokens },
        });
        var json = await SendAsync(o.Model, body, ct);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var text = TextOf(FirstContent(doc.RootElement));
            if (text.Length == 0) throw new AiException("AI không trả về nội dung. Chạy lại sau.");
            return text;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new AiException("AI trả về dữ liệu không đọc được. Chạy lại sau.", ex);
        }
    }

    public async Task<AiChatReply> ChatAsync(string systemInstruction, IReadOnlyList<AiChatMessage> messages, IReadOnlyList<AiTool> tools, bool allowTools,
        CancellationToken ct = default)
    {
        var o = options.Value;
        var body = JsonSerializer.Serialize(new
        {
            systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
            contents = messages.Select(ToContent).ToList(),
            tools = tools.Count == 0 ? null : new[]
            {
                new { functionDeclarations = tools.Select(t => new { name = t.Name, description = t.Description, parameters = t.Parameters }).ToList() },
            },
            toolConfig = tools.Count == 0 ? null : new { functionCallingConfig = new { mode = allowTools ? "AUTO" : "NONE" } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = o.MaxOutputTokens },
        }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        var json = await SendAsync(o.EffectiveChatModel, body, ct);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var content = FirstContent(doc.RootElement);
            var calls = new List<AiToolCall>();
            foreach (var part in content.GetProperty("parts").EnumerateArray())
                if (part.TryGetProperty("functionCall", out var call))
                    calls.Add(new AiToolCall(call.GetProperty("name").GetString() ?? "",
                        call.TryGetProperty("args", out var args) ? args.Clone() : JsonDocument.Parse("{}").RootElement.Clone(),
                        call.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null));
            if (calls.Count > 0) return new AiChatReply(null, calls, content.GetRawText());
            var text = TextOf(content);
            if (text.Length == 0) throw new AiException("AI không trả về nội dung. Hỏi lại sau.");
            return new AiChatReply(text, [], null);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new AiException("AI trả về dữ liệu không đọc được. Hỏi lại sau.", ex);
        }
    }

    private static object ToContent(AiChatMessage m) => m switch
    {
        AiUserText u => new { role = "user", parts = new object[] { new { text = u.Text } } },
        AiModelText t => new { role = "model", parts = new object[] { new { text = t.Text } } },
        // Gửi lại đúng nội dung model đã trả (kể cả chữ ký suy nghĩ), nếu không Gemini từ chối lượt gọi tool.
        AiModelToolCalls c when c.ProviderState is not null => JsonNode.Parse(c.ProviderState)!,
        AiModelToolCalls c => new { role = "model", parts = c.Calls.Select(x => (object)new { functionCall = new { name = x.Name, args = x.Args } }).ToArray() },
        AiToolResults r => new { role = "user", parts = r.Results.Select(x => (object)new { functionResponse = new { id = x.Id, name = x.Name, response = x.Result } }).ToArray() },
        _ => throw new ArgumentOutOfRangeException(nameof(m)),
    };

    /// <summary>Gửi một yêu cầu generateContent, thử lại một lần khi quá tải hoặc mất kết nối. Trả về thân phản hồi JSON.</summary>
    private async Task<string> SendAsync(string model, string body, CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiKey) || string.IsNullOrWhiteSpace(model))
            throw new AiException("Chưa cấu hình AI. Liên hệ quản trị để cấu hình khóa và model.");
        var url = $"{o.BaseUrl.TrimEnd('/')}/models/{model}:generateContent";

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
                if (response.IsSuccessStatusCode) return text;

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

    private static JsonElement FirstContent(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            throw new AiException("AI không trả về nội dung. Chạy lại sau.");
        var candidate = candidates[0];
        if (!candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out _))
            throw new AiException("AI không trả về nội dung. Chạy lại sau.");
        return content;
    }

    /// <summary>Gộp các đoạn văn bản, bỏ đoạn suy nghĩ nếu model trả kèm.</summary>
    private static string TextOf(JsonElement content)
    {
        var sb = new StringBuilder();
        foreach (var part in content.GetProperty("parts").EnumerateArray())
        {
            if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True) continue;
            if (part.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String) sb.Append(t.GetString());
        }
        return sb.ToString();
    }

    private static string Truncate(string s) => s.Length <= 400 ? s : s[..400];
}
