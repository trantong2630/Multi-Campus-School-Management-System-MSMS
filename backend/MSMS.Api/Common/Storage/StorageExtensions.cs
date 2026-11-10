using CloudinaryDotNet;

namespace MSMS.Api.Common.Storage;

public static class StorageExtensions
{
    /// <summary>
    /// Ảnh lưu trên Cloudinary. Khóa Cloudinary:CloudName, ApiKey, ApiSecret nằm trong user-secrets hoặc biến môi trường,
    /// không ghi vào appsettings.json; thiếu khóa thì API không khởi động.
    /// </summary>
    public static IServiceCollection AddImageStorage(this IServiceCollection services, IConfiguration config)
    {
        var cloud = config["Cloudinary:CloudName"];
        var key = config["Cloudinary:ApiKey"];
        var secret = config["Cloudinary:ApiSecret"];
        if (string.IsNullOrWhiteSpace(cloud) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Thiếu cấu hình Cloudinary:CloudName, Cloudinary:ApiKey hoặc Cloudinary:ApiSecret. Đặt bằng dotnet user-secrets (xem MSMS_Dev_Conventions.md).");
        services.AddSingleton(new Cloudinary(new Account(cloud, key, secret)) { Api = { Secure = true } });
        services.AddHttpClient(CloudinaryImageStorage.HttpClientName);
        services.AddSingleton<IImageStorage, CloudinaryImageStorage>();
        return services;
    }
}
