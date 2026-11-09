namespace MSMS.Api.Common.Storage;

/// <summary>Ảnh đã lưu, sẵn sàng trả về cho trình duyệt. Người gọi chịu trách nhiệm đóng <see cref="Content"/>.</summary>
public sealed record StoredImage(Stream Content, string ContentType);

/// <summary>Tên thư mục lưu ảnh theo từng loại (BR-27).</summary>
public static class ImageFolders
{
    public const string Posts = "posts";
    public const string Avatars = "avatars";
    public const string School = "school";
    public const string Textbooks = "textbooks";
}

/// <summary>
/// Nơi lưu ảnh của hệ thống: ảnh đại diện, logo trường, ảnh bài đăng. Chỉ nhận ảnh đã kiểm (JPG/PNG, đúng dung lượng).
/// DB chỉ giữ tên file do <see cref="SaveAsync"/> trả về; ảnh thật nằm trên Cloudinary.
/// </summary>
public interface IImageStorage
{
    /// <summary>Lưu ảnh, trả về tên file (kèm đuôi) để ghi vào DB.</summary>
    Task<string> SaveAsync(string folder, string prefix, byte[] bytes, string extension);

    /// <summary>Xóa ảnh; không có ảnh hoặc xóa lỗi thì bỏ qua vì file mồ côi không ảnh hưởng nghiệp vụ.</summary>
    Task DeleteAsync(string folder, string? fileName);

    /// <summary>Mở ảnh để trả về; null nếu ảnh không còn.</summary>
    Task<StoredImage?> OpenAsync(string folder, string fileName);
}

public static class ImageContentTypes
{
    public static string FromFileName(string fileName) =>
        fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
}
