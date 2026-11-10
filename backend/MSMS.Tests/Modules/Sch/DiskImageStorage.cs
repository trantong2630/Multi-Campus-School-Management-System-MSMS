using MSMS.Api.Common.Storage;

namespace MSMS.Tests.Modules.Sch;

/// <summary>Bản lưu ảnh vào thư mục tạm, chỉ dùng trong test để khỏi gọi Cloudinary thật.</summary>
public sealed class DiskImageStorage(string root) : IImageStorage
{
    private string Dir(string folder) => Path.Combine(root, "App_Data", "uploads", folder);

    public async Task<string> SaveAsync(string folder, string prefix, byte[] bytes, string extension)
    {
        Directory.CreateDirectory(Dir(folder));
        var name = $"{prefix}-{Guid.NewGuid():N}{extension}";
        await File.WriteAllBytesAsync(Path.Combine(Dir(folder), name), bytes);
        return name;
    }

    public Task DeleteAsync(string folder, string? fileName)
    {
        if (!string.IsNullOrEmpty(fileName)) File.Delete(Path.Combine(Dir(folder), Path.GetFileName(fileName)));
        return Task.CompletedTask;
    }

    public Task<StoredImage?> OpenAsync(string folder, string fileName)
    {
        var path = Path.Combine(Dir(folder), Path.GetFileName(fileName));   // GetFileName chặn đường dẫn thoát khỏi thư mục
        StoredImage? image = File.Exists(path)
            ? new StoredImage(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), ImageContentTypes.FromFileName(path))
            : null;
        return Task.FromResult(image);
    }
}
