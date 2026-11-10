using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Storage;
using MSMS.Api.Common.Text;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Dtos;

namespace MSMS.Api.Modules.Iam.Services;

/// <summary>
/// IAM-04: hồ sơ cá nhân của chính người đăng nhập — xem (4.1.27, 4.1.30), sửa trường cá nhân được phép (4.1.28),
/// tải/đổi ảnh đại diện (4.1.29). Mọi thao tác chỉ chạm tới hồ sơ của tài khoản trong token, không nhận id từ client.
/// Ảnh lưu qua IImageStorage cùng với ảnh bảng tin và logo trường, DB chỉ giữ tên file (BR-27).
/// </summary>
public sealed class ProfileService(MsmsDbContext db, CurrentUser user, IClock clock, IImageStorage storage)
{
    private const long MaxAvatarBytes = 2 * 1024 * 1024;   // cùng giới hạn với ảnh bảng tin và logo trường (BR-27)
    private const string ParentType = "Parent";

    /// <summary>4.1.27, 4.1.30: hồ sơ, trách nhiệm đang giữ và trạng thái tài khoản của chính mình.</summary>
    public async Task<OwnProfileDto> GetAsync()
    {
        var account = await db.Account.AsNoTracking().Include(a => a.Person).FirstAsync(a => a.AccountId == user.AccountId);
        return await ToDtoAsync(account);
    }

    /// <summary>
    /// 4.1.28: sửa số điện thoại, email, địa chỉ. Email là trường thông tin bình thường, không có luồng xác nhận (BR-04).
    /// Họ tên, ngày sinh, giới tính, số định danh do School Admin quản lý ở danh bạ (PPL), không tự sửa ở đây.
    /// </summary>
    public async Task<OwnProfileDto> UpdateAsync(UpdateProfileRequest r)
    {
        var email = Guard.Optional(r.Email, "Email", 150);
        if (email is not null && !Guard.IsEmail(email))
            throw ApiException.BadRequest("Email chưa đúng định dạng. Kiểm tra lại địa chỉ email.");
        var phone = Guard.Optional(r.PhoneNumber, "Số điện thoại", 20);
        var address = Guard.Optional(r.Address, "Địa chỉ", 255);

        var account = await LoadTrackedAsync();
        var person = account.Person;

        // BR-10: số điện thoại là khóa nhận diện phụ huynh khi import, đổi tự do sẽ làm import sau tạo hồ sơ trùng.
        if (person.PersonType == ParentType && (phone ?? "") != TextNormalizer.Normalize(person.PhoneNumber))
            throw ApiException.BadRequest("Số điện thoại là khóa nhận diện của phụ huynh nên không tự sửa được. Liên hệ nhà trường để đổi.");

        if (person.PersonType != ParentType) person.PhoneNumber = phone;
        person.Email = email;
        person.Address = address;
        person.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
        return await ToDtoAsync(account);
    }

    /// <summary>
    /// 4.1.29: tải ảnh đại diện JPG hoặc PNG, tối đa 2 MB, kiểm theo nội dung file chứ không tin đuôi file (BR-27).
    /// Đổi ảnh thì file cũ bị xóa sau khi lưu DB thành công.
    /// </summary>
    public async Task<OwnProfileDto> SetAvatarAsync(IFormFile file)
    {
        if (file.Length == 0) throw ApiException.BadRequest("File rỗng. Chọn lại ảnh đại diện.");
        if (file.Length > MaxAvatarBytes) throw ApiException.BadRequest("Ảnh đại diện tối đa 2 MB. Chọn ảnh nhỏ hơn.");

        await using var input = file.OpenReadStream();
        using var ms = new MemoryStream((int)file.Length);
        await input.CopyToAsync(ms);
        var bytes = ms.ToArray();
        var extension = DetectImageExtension(bytes)
            ?? throw ApiException.BadRequest("Ảnh đại diện chỉ nhận JPG hoặc PNG. Chọn lại ảnh đúng định dạng.");

        var account = await LoadTrackedAsync();
        var person = account.Person;

        var fileName = await storage.SaveAsync(ImageFolders.Avatars, "avatar", bytes, extension);

        var oldFile = person.AvatarPath;
        person.AvatarPath = fileName;
        person.UpdatedAt = clock.Now;
        try
        {
            await db.SaveChangesAsync();
        }
        catch
        {
            await DeleteAvatarFileAsync(fileName);   // DB không lưu được thì không để lại file mồ côi
            throw;
        }
        await DeleteAvatarFileAsync(oldFile);
        return await ToDtoAsync(account);
    }

    /// <summary>4.1.29: gỡ ảnh đại diện, xóa luôn file; giao diện quay về chữ cái đầu tên.</summary>
    public async Task<OwnProfileDto> RemoveAvatarAsync()
    {
        var account = await LoadTrackedAsync();
        var person = account.Person;
        var oldFile = person.AvatarPath;
        if (oldFile is not null)
        {
            person.AvatarPath = null;
            person.UpdatedAt = clock.Now;
            await db.SaveChangesAsync();
            await DeleteAvatarFileAsync(oldFile);
        }
        return await ToDtoAsync(account);
    }

    /// <summary>Ảnh đại diện; null nếu người đó chưa có ảnh hoặc ảnh đã mất.</summary>
    public async Task<StoredImage?> FindAvatarAsync(int personId)
    {
        var name = await db.Person.AsNoTracking()
            .Where(p => p.PersonId == personId)
            .Select(p => p.AvatarPath)
            .FirstOrDefaultAsync();
        return name is null ? null : await storage.OpenAsync(ImageFolders.Avatars, name);
    }

    private Task<Account> LoadTrackedAsync() =>
        db.Account.Include(a => a.Person).FirstAsync(a => a.AccountId == user.AccountId);

    /// <summary>
    /// 4.1.27: trách nhiệm đang hiệu lực TẠI NGÀY HIỆN TẠI, không lấy bản mới nhất (BR-09) — cùng quy tắc với
    /// AccountScopeResolver (4.1.3). Hiệu phó: phụ trách phân hiệu; Giáo viên: tổ trưởng, chủ nhiệm.
    /// </summary>
    private async Task<IReadOnlyList<ResponsibilityDto>> ResolveResponsibilitiesAsync(int personId, string accountType)
    {
        var today = clock.Today;
        var result = new List<ResponsibilityDto>();

        if (accountType == AccountTypes.VicePrincipal)
        {
            var campuses = await db.CampusLeadership.AsNoTracking()
                .Where(EffectivePeriod.CurrentResponsibility<CampusLeadership>(today))
                .Where(x => x.StaffId == personId)
                .OrderBy(x => x.Campus.Name)
                .Select(x => new { x.Campus.Name, x.StartDate })
                .ToListAsync();
            result.AddRange(campuses.Select(c => new ResponsibilityDto("Phụ trách phân hiệu", c.Name, Iso(c.StartDate))));
        }
        else if (accountType == AccountTypes.Teacher)
        {
            var departments = await db.DepartmentHead.AsNoTracking()
                .Where(EffectivePeriod.CurrentResponsibility<DepartmentHead>(today))
                .Where(x => x.StaffId == personId)
                .OrderBy(x => x.Department.Name)
                .Select(x => new { x.Department.Name, x.StartDate })
                .ToListAsync();
            result.AddRange(departments.Select(d => new ResponsibilityDto("Tổ trưởng", d.Name, Iso(d.StartDate))));

            var classes = await db.HomeroomAssignment.AsNoTracking()
                .Where(EffectivePeriod.CurrentResponsibility<HomeroomAssignment>(today))
                .Where(x => x.StaffId == personId)
                .OrderBy(x => x.Class.Name)
                .Select(x => new { x.Class.Name, x.StartDate })
                .ToListAsync();
            result.AddRange(classes.Select(c => new ResponsibilityDto("Chủ nhiệm", c.Name, Iso(c.StartDate))));
        }

        return result;
    }

    private async Task<OwnProfileDto> ToDtoAsync(Account a)
    {
        var p = a.Person;
        var responsibilities = await ResolveResponsibilitiesAsync(a.PersonId, a.AccountType);
        return new OwnProfileDto(
            p.PersonId, p.PersonCode, p.PersonType, p.FullName,
            p.DateOfBirth is { } dob ? Iso(dob) : null,
            p.Gender, p.PhoneNumber, p.Email, p.Address,
            p.AvatarPath is not null, p.AvatarPath is not null ? (p.UpdatedAt ?? p.CreatedAt).Ticks : null,
            a.AccountId, a.Username, a.AccountType, a.Status, a.LastLoginAt,
            responsibilities,
            p.PersonType != ParentType);
    }

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");

    private Task DeleteAvatarFileAsync(string? fileName) => storage.DeleteAsync(ImageFolders.Avatars, fileName);

    private static string? DetectImageExtension(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return ".png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        return null;
    }
}
