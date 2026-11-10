using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Storage;

namespace MSMS.Api.Modules.Com.Services;

/// <summary>
/// Lưu file ảnh bài đăng (BR-27): JPG hoặc PNG, mỗi ảnh tối đa 2 MB, kiểm theo nội dung file chứ không tin đuôi file.
/// Nơi lưu thật (thư mục máy hay Cloudinary) do IImageStorage quyết định; DB chỉ giữ tên file.
/// </summary>
public sealed class PostImageStorage(IImageStorage storage)
{
    public const long MaxBytes = 2 * 1024 * 1024;

    /// <summary>Đọc và kiểm một ảnh; trả về nội dung và đuôi file chuẩn.</summary>
    public async Task<(byte[] Bytes, string Extension)> ReadAsync(IFormFile file)
    {
        if (file.Length == 0) throw ApiException.BadRequest("Có ảnh bị rỗng. Chọn lại ảnh.");
        if (file.Length > MaxBytes) throw ApiException.BadRequest($"Ảnh \"{file.FileName}\" lớn hơn 2 MB. Chọn ảnh nhỏ hơn.");
        await using var input = file.OpenReadStream();
        using var ms = new MemoryStream((int)file.Length);
        await input.CopyToAsync(ms);
        var bytes = ms.ToArray();
        var ext = DetectExtension(bytes)
            ?? throw ApiException.BadRequest($"Ảnh \"{file.FileName}\" không phải JPG hoặc PNG. Chọn lại ảnh đúng định dạng.");
        return (bytes, ext);
    }

    public Task<string> SaveAsync(byte[] bytes, string extension) =>
        storage.SaveAsync(ImageFolders.Posts, "post", bytes, extension);

    public Task DeleteAsync(string? fileName) => storage.DeleteAsync(ImageFolders.Posts, fileName);

    public async Task DeleteAllAsync(IEnumerable<string> fileNames)
    {
        foreach (var f in fileNames) await DeleteAsync(f);
    }

    public Task<StoredImage?> OpenAsync(string fileName) => storage.OpenAsync(ImageFolders.Posts, fileName);

    private static string? DetectExtension(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return ".png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        return null;
    }
}
