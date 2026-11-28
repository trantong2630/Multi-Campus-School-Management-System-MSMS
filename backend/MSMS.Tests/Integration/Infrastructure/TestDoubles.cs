using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using MSMS.Api.Common.Ai;
using MSMS.Api.Common.Email;
using MSMS.Api.Common.Storage;
using MSMS.Api.Common.Time;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Tests.Integration.Infrastructure;

/// <summary>Đồng hồ chỉnh được: test đặt "bây giờ" theo giờ Việt Nam để kiểm các quy tắc phụ thuộc ngày giờ (IClock).</summary>
public sealed class TestClock : IClock
{
    private static readonly TimeZoneInfo Vn = ResolveVn();
    private DateTime? fixedNow;

    public DateTime Now => fixedNow ?? TimeZoneInfo.ConvertTime(DateTime.UtcNow, Vn);
    public DateOnly Today => DateOnly.FromDateTime(Now);

    public void Set(DateTime vietnamNow) => fixedNow = vietnamNow;
    public void Reset() => fixedNow = null;

    private static TimeZoneInfo ResolveVn()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "VN", "VN");
    }
}

/// <summary>Ghi lại thư thay vì gửi qua SMTP.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    public readonly ConcurrentQueue<(string To, string Subject, string Body)> Sent = new();
    public Task<bool> SendAsync(string recipient, string subject, string body, CancellationToken ct = default)
    {
        Sent.Enqueue((recipient, subject, body));
        return Task.FromResult(true);
    }
}

/// <summary>Lưu ảnh trong bộ nhớ, không gọi Cloudinary.</summary>
public sealed class MemoryImageStorage : IImageStorage
{
    private readonly ConcurrentDictionary<string, byte[]> files = new();
    public Task<string> SaveAsync(string folder, string prefix, byte[] bytes, string extension)
    {
        var name = $"{prefix}-{Guid.NewGuid():N}{extension}";
        files[$"{folder}/{name}"] = bytes;
        return Task.FromResult(name);
    }
    /// <summary>Đặt sẵn một ảnh với đúng tên file (để test sách giáo khoa nạp sẵn).</summary>
    public void Put(string folder, string fileName, byte[] bytes) => files[$"{folder}/{fileName}"] = bytes;
    public Task DeleteAsync(string folder, string? fileName)
    {
        if (fileName != null) files.TryRemove($"{folder}/{fileName}", out _);
        return Task.CompletedTask;
    }
    public Task<StoredImage?> OpenAsync(string folder, string fileName) =>
        Task.FromResult<StoredImage?>(files.TryGetValue($"{folder}/{fileName}", out var b)
            ? new StoredImage(new MemoryStream(b), ImageContentTypes.FromFileName(fileName)) : null);
}

/// <summary>AI giả: ghi lại yêu cầu và trả về nội dung cố định; đặt <see cref="Fail"/> để mô phỏng AI hỏng.</summary>
public sealed class StubAiClient : IAiClient
{
    public readonly ConcurrentQueue<string> Prompts = new();
    public bool Fail { get; set; }
    public string JsonReply { get; set; } = "{}";

    public Task<string> GenerateJsonAsync(string systemInstruction, string prompt, IReadOnlyList<AiImage> images, CancellationToken ct = default)
    {
        Prompts.Enqueue(prompt);
        if (Fail) throw new AiException("AI tạm thời không phản hồi.");
        return Task.FromResult(JsonReply);
    }

    /// <summary>Kịch bản trả lời của AI: nhận số lượt gọi tool đã xong và hội thoại tới giờ; trả null để dùng câu trả lời cố định.</summary>
    public Func<int, IReadOnlyList<AiChatMessage>, AiChatReply?>? ChatScript { get; set; }
    /// <summary>Mỗi lần AI được hỏi: bản chụp toàn bộ hội thoại gửi lên (để kiểm tra những gì AI nhìn thấy).</summary>
    public readonly ConcurrentQueue<List<AiChatMessage>> ChatLog = new();
    public int ToolRoundsAllowedSeen;

    public Task<AiChatReply> ChatAsync(string systemInstruction, IReadOnlyList<AiChatMessage> messages, IReadOnlyList<AiTool> tools, bool allowTools, CancellationToken ct = default)
    {
        ChatLog.Enqueue(messages.ToList());
        foreach (var m in messages) Prompts.Enqueue(m.ToString() ?? "");
        if (Fail) throw new AiException("AI tạm thời không phản hồi.");
        if (ChatScript?.Invoke(messages.OfType<AiToolResults>().Count(), messages) is { } scripted && (allowTools || scripted.Calls.Count == 0)) return Task.FromResult(scripted);
        return Task.FromResult(new AiChatReply("Đây là câu trả lời giả của AI.", [], null));
    }
}

/// <summary>Ghi lại mã OTP thay vì gửi email; test lấy mã để đi tiếp luồng quên mật khẩu.</summary>
public sealed class RecordingOtpSender : IOtpEmailSender
{
    public readonly ConcurrentQueue<(string To, string Code, DateTime ExpiresAt)> Sent = new();
    public Task<bool> SendAsync(string recipient, string code, DateTime expiresAt, CancellationToken ct = default)
    {
        Sent.Enqueue((recipient, code, expiresAt));
        return Task.FromResult(true);
    }
    public string? LastCodeTo(string recipient) => Sent.Where(s => s.To == recipient).Select(s => s.Code).LastOrDefault();
    public int CountTo(string recipient) => Sent.Count(s => s.To == recipient);
}
