using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace MSMS.Api.Common.Storage;

/// <summary>
/// Lưu ảnh trên Cloudinary, public_id = msms/&lt;folder&gt;/&lt;tên file không đuôi&gt;. DB vẫn chỉ giữ tên file.
/// API đọc ảnh lấy từ Cloudinary rồi trả lại cho trình duyệt, nên quyền xem ảnh vẫn do API kiểm như trước.
/// </summary>
public sealed class CloudinaryImageStorage(Cloudinary cloudinary, IHttpClientFactory http, ILogger<CloudinaryImageStorage> log) : IImageStorage
{
    public const string HttpClientName = "cloudinary";

    private static string PublicId(string folder, string fileName) => $"msms/{folder}/{Path.GetFileNameWithoutExtension(fileName)}";

    public async Task<string> SaveAsync(string folder, string prefix, byte[] bytes, string extension)
    {
        var name = $"{prefix}-{Guid.NewGuid():N}{extension}";
        using var ms = new MemoryStream(bytes);
        var result = await cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(name, ms),
            PublicId = PublicId(folder, name),
            AssetFolder = $"msms/{folder}",   // tài khoản dùng dynamic folders: thư mục là thuộc tính riêng, không suy ra từ public_id
            Overwrite = false,
        });
        if (result.Error is not null)
            throw new InvalidOperationException($"Cloudinary không lưu được ảnh: {result.Error.Message}");
        return name;
    }

    public async Task DeleteAsync(string folder, string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return;
        try
        {
            var result = await cloudinary.DestroyAsync(new DeletionParams(PublicId(folder, fileName)));
            if (result.Error is not null) log.LogWarning("Cloudinary không xóa được {File}: {Error}", fileName, result.Error.Message);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Cloudinary không xóa được {File}", fileName);   // file mồ côi không ảnh hưởng nghiệp vụ
        }
    }

    public async Task<StoredImage?> OpenAsync(string folder, string fileName)
    {
        var url = cloudinary.Api.UrlImgUp.Secure(true)
            .Format(Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant())
            .BuildUrl(PublicId(folder, fileName));
        var response = await http.CreateClient(HttpClientName).GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            return null;
        }
        return new StoredImage(await response.Content.ReadAsStreamAsync(), ImageContentTypes.FromFileName(fileName));
    }
}
