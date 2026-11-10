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
}

/// <summary>Một ảnh gửi kèm yêu cầu AI.</summary>
public sealed record AiImage(string MimeType, byte[] Data);
