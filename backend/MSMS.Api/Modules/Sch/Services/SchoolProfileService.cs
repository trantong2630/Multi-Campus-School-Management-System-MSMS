using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;

namespace MSMS.Api.Modules.Sch.Services;

/// <summary>
/// SCH-07: thông tin nhận diện của trường (tên, địa chỉ, liên hệ, logo). Đúng một dòng; chưa lưu lần nào thì
/// các trường trả về null và giao diện dùng tên mặc định. Hiệu trưởng và School Admin sửa được.
/// </summary>
public sealed class SchoolProfileService(MsmsDbContext db, CurrentUser user, IClock clock, IWebHostEnvironment env)
{
    private const int ProfileId = 1;
    private const long MaxLogoBytes = 2 * 1024 * 1024;   // cùng giới hạn ảnh với bảng tin (BR-27)

    private string LogoDirectory => Path.Combine(env.ContentRootPath, "App_Data", "uploads", "school");

    public async Task<SchoolProfileDto> GetAsync()
    {
        var p = await db.SchoolProfile.AsNoTracking().FirstOrDefaultAsync(x => x.SchoolProfileId == ProfileId);
        return p is null ? new SchoolProfileDto(null, null, null, null, null, false, null) : ToDto(p);
    }

    public async Task<SchoolProfileDto> UpdateAsync(SchoolProfileRequest r)
    {
        var name = Guard.Required(r.Name, "Tên trường", 200);
        var email = Guard.Optional(r.Email, "Email", 150);
        if (email is not null && !Guard.IsEmail(email)) throw ApiException.BadRequest("Email chưa đúng định dạng. Kiểm tra lại địa chỉ email.");
        var website = Guard.Optional(r.Website, "Website", 200);
        if (website is not null && !(Uri.TryCreate(website, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
            throw ApiException.BadRequest("Website phải bắt đầu bằng http:// hoặc https://.");

        var p = await db.SchoolProfile.FirstOrDefaultAsync(x => x.SchoolProfileId == ProfileId);
        if (p is null)
        {
            p = new SchoolProfile { SchoolProfileId = ProfileId };
            db.SchoolProfile.Add(p);
        }
        p.Name = name;
        p.Address = Guard.Optional(r.Address, "Địa chỉ", 255);
        p.PhoneNumber = Guard.Optional(r.PhoneNumber, "Số điện thoại", 20);
        p.Email = email;
        p.Website = website;
        Stamp(p);
        await db.SaveChangesAsync();
        return ToDto(p);
    }

    /// <summary>Chỉ nhận JPG hoặc PNG, tối đa 2 MB. Kiểm theo nội dung file chứ không tin đuôi file hay Content-Type.</summary>
    public async Task<SchoolProfileDto> SetLogoAsync(IFormFile file)
    {
        if (file.Length == 0) throw ApiException.BadRequest("File rỗng. Chọn lại ảnh logo.");
        if (file.Length > MaxLogoBytes) throw ApiException.BadRequest("Logo tối đa 2 MB. Chọn ảnh nhỏ hơn.");

        var p = await db.SchoolProfile.FirstOrDefaultAsync(x => x.SchoolProfileId == ProfileId)
            ?? throw ApiException.BadRequest("Nhập tên trường và lưu trước, sau đó mới tải logo.");

        await using var input = file.OpenReadStream();
        var header = new byte[8];
        var read = await input.ReadAsync(header);
        var extension = DetectImageExtension(header.AsSpan(0, read))
            ?? throw ApiException.BadRequest("Logo chỉ nhận ảnh JPG hoặc PNG. Chọn lại ảnh đúng định dạng.");
        input.Position = 0;

        Directory.CreateDirectory(LogoDirectory);
        var fileName = $"logo-{Guid.NewGuid():N}{extension}";
        await using (var output = File.Create(Path.Combine(LogoDirectory, fileName)))
            await input.CopyToAsync(output);

        var oldFile = p.LogoPath;
        p.LogoPath = fileName;
        Stamp(p);
        await db.SaveChangesAsync();
        DeleteLogoFile(oldFile);
        return ToDto(p);
    }

    public async Task<SchoolProfileDto> RemoveLogoAsync()
    {
        var p = await db.SchoolProfile.FirstOrDefaultAsync(x => x.SchoolProfileId == ProfileId);
        if (p?.LogoPath is null) return await GetAsync();
        var oldFile = p.LogoPath;
        p.LogoPath = null;
        Stamp(p);
        await db.SaveChangesAsync();
        DeleteLogoFile(oldFile);
        return ToDto(p);
    }

    /// <summary>Đường dẫn file logo và loại nội dung; null nếu trường chưa có logo.</summary>
    public async Task<(string Path, string ContentType)?> FindLogoAsync()
    {
        var name = await db.SchoolProfile.AsNoTracking().Where(x => x.SchoolProfileId == ProfileId).Select(x => x.LogoPath).FirstOrDefaultAsync();
        if (name is null) return null;
        var path = Path.Combine(LogoDirectory, Path.GetFileName(name));   // GetFileName chặn đường dẫn thoát khỏi thư mục
        if (!File.Exists(path)) return null;
        return (path, path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg");
    }

    private void Stamp(SchoolProfile p)
    {
        p.UpdatedByPersonId = user.PersonId;
        p.UpdatedAt = clock.Now;
    }

    private void DeleteLogoFile(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return;
        try { File.Delete(Path.Combine(LogoDirectory, Path.GetFileName(fileName))); }
        catch (IOException) { /* file mồ côi không ảnh hưởng nghiệp vụ */ }
    }

    private static string? DetectImageExtension(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return ".png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        return null;
    }

    private static SchoolProfileDto ToDto(SchoolProfile p) => new(
        p.Name, p.Address, p.PhoneNumber, p.Email, p.Website, p.LogoPath is not null, p.UpdatedAt?.Ticks);
}
