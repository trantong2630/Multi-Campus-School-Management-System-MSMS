using System.Text.Json;
using System.Text.Json.Nodes;

namespace MSMS.Api.Common.Ai;

/// <summary>Lỗi khi gọi AI: AI hỏng, hết hạn mức, hết giờ hoặc chưa cấu hình. Thông báo hiển thị được cho giáo viên.</summary>
public sealed class AiException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Cổng duy nhất gọi AI (mục 3.1). Chỉ trả về văn bản JSON do AI sinh; Backend luôn kiểm lại nội dung trước khi lưu,
/// AI không ghi trực tiếp vào dữ liệu nghiệp vụ.
/// </summary>
public interface IAiClient
{
    Task<string> GenerateJsonAsync(string systemInstruction, string prompt, CancellationToken ct = default) =>
        GenerateJsonAsync(systemInstruction, prompt, [], ct);

    /// <summary>Như trên, kèm ảnh để AI đọc (ví dụ các trang sách giáo khoa) ngay trong yêu cầu; ảnh không được lưu lại ở phía AI.</summary>
    Task<string> GenerateJsonAsync(string systemInstruction, string prompt, IReadOnlyList<AiImage> images, CancellationToken ct = default);

    /// <summary>
    /// Một lượt hội thoại nhiều bước có khai báo tool (model chat, <see cref="AiOptions.ChatModel"/>). AI trả về hoặc các yêu cầu gọi tool, hoặc câu trả lời bằng chữ.
    /// <paramref name="allowTools"/> = false buộc AI trả lời bằng chữ (vòng cuối). AI không tự chạy tool; Backend chạy rồi gửi kết quả ở lượt sau.
    /// </summary>
    Task<AiChatReply> ChatAsync(string systemInstruction, IReadOnlyList<AiChatMessage> messages, IReadOnlyList<AiTool> tools, bool allowTools, CancellationToken ct = default);
}

/// <summary>Một ảnh gửi kèm yêu cầu AI.</summary>
public sealed record AiImage(string MimeType, byte[] Data);

/// <summary>Khai báo một tool cho AI: tên, mô tả và lược đồ tham số (JSON Schema rút gọn).</summary>
public sealed record AiTool(string Name, string Description, JsonObject Parameters);

/// <summary>Một yêu cầu gọi tool của AI; Id (nếu nhà cung cấp cấp) phải gửi lại cùng kết quả.</summary>
public sealed record AiToolCall(string Name, JsonElement Args, string? Id = null);

/// <summary>Kết quả một tool gửi lại cho AI; Result luôn là một đối tượng JSON, Id khớp với lời gọi.</summary>
public sealed record AiToolResult(string Name, JsonObject Result, string? Id = null);

/// <summary>Các loại tin trong hội thoại gửi cho AI.</summary>
public abstract record AiChatMessage;

public sealed record AiUserText(string Text) : AiChatMessage;

public sealed record AiModelText(string Text) : AiChatMessage;

/// <summary>Lượt AI yêu cầu gọi tool. ProviderState giữ nguyên nội dung nhà cung cấp trả về để gửi lại đúng như cũ (Gemini cần chữ ký suy nghĩ đi kèm).</summary>
public sealed record AiModelToolCalls(IReadOnlyList<AiToolCall> Calls, string? ProviderState) : AiChatMessage;

public sealed record AiToolResults(IReadOnlyList<AiToolResult> Results) : AiChatMessage;

/// <summary>Kết quả một lượt chat: có Calls thì Backend chạy tool rồi hỏi tiếp; không có thì Text là câu trả lời.</summary>
public sealed record AiChatReply(string? Text, IReadOnlyList<AiToolCall> Calls, string? ProviderState);
