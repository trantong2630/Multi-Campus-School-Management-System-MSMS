using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;

namespace MSMS.Api.Modules.Ppl.Services;

public static class Genders
{
    public static readonly string[] All = ["Male", "Female", "Other"];

    /// <summary>Nhãn trong file Excel (Nam / Nữ / Khác) hoặc giá trị lưu trong DB.</summary>
    public static string? Parse(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "nam" or "male" => "Male",
        "nữ" or "nu" or "female" => "Female",
        "khác" or "khac" or "other" => "Other",
        _ => "?",
    };
}

/// <summary>PPL-01: danh bạ cán bộ. Import chỉ tạo hồ sơ, sửa hồ sơ luôn làm ở đây (BR-11).</summary>
public sealed class StaffService(MsmsDbContext db, IClock clock)
{
    public Task<PagedResult<StaffDto>> ListAsync(int? campusId, int? departmentId, string? accountType, bool? hasAbbr,
        string? search, bool includeInactive, int page, int pageSize)
    {
        var query = db.StaffProfile.AsNoTracking()
            .Where(s => includeInactive || s.IsActive)
            .Where(s => campusId == null || s.PrimaryCampusId == campusId)
            .Where(s => departmentId == null || s.DepartmentId == departmentId)
            .Where(s => accountType == null || (s.Staff.Account != null && s.Staff.Account.AccountType == accountType))
            .Where(s => hasAbbr == null || (hasAbbr == true ? s.TeacherAbbr != null : s.TeacherAbbr == null));

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            // Tìm phân biệt dấu (collation Vietnamese_CI_AS của DB): gõ có dấu mới khớp, không phân biệt hoa thường.
            query = query.Where(s => s.Staff.FullName.Contains(term)
                                     || s.Staff.PersonCode.Contains(term)
                                     || (s.TeacherAbbr != null && s.TeacherAbbr == term));
        }

        return query.OrderBy(s => s.Staff.FullName)
            .Select(s => new StaffDto(s.StaffId, s.Staff.PersonCode, s.Staff.FullName, s.Staff.DateOfBirth, s.Staff.NationalId, s.Staff.Gender,
                s.Staff.PhoneNumber, s.Staff.Email, s.PrimaryCampusId, s.PrimaryCampus.Name, s.DepartmentId,
                s.Department == null ? null : s.Department.Name, s.TeacherAbbr, s.Position, s.IsActive,
                s.Staff.Account == null ? null : s.Staff.Account.AccountId,
                s.Staff.Account == null ? null : s.Staff.Account.AccountType,
                s.Staff.Account == null ? null : s.Staff.Account.Status))
            .ToPagedAsync(page, pageSize);
    }

    public async Task<StaffDto> GetAsync(int id) =>
        (await ListByIdAsync(id)) ?? throw ApiException.NotFound("Không tìm thấy hồ sơ cán bộ.");

    public async Task<StaffDto> CreateAsync(StaffRequest r)
    {
        var v = await ValidateAsync(null, r);
        await using var tx = await db.Database.BeginTransactionAsync();
        var person = new Person
        {
            PersonType = "Staff", PersonCode = await PersonCodes.NextAsync(db, PersonCodes.Staff),
            FullName = v.FullName, DateOfBirth = r.DateOfBirth, NationalId = v.NationalId, Gender = v.Gender, PhoneNumber = v.Phone, Email = v.Email,
            CreatedAt = clock.Now,
        };
        db.Person.Add(person);
        db.StaffProfile.Add(new StaffProfile
        {
            Staff = person, PrimaryCampusId = r.PrimaryCampusId, DepartmentId = r.DepartmentId, TeacherAbbr = v.Abbr,
            Position = Guard.Optional(r.Position, "Chức danh", 100), IsActive = true,
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return await GetAsync(person.PersonId);
    }

    public async Task<StaffDto> UpdateAsync(int id, StaffRequest r)
    {
        var staff = await db.StaffProfile.Include(s => s.Staff).FirstOrDefaultAsync(s => s.StaffId == id)
            ?? throw ApiException.NotFound("Không tìm thấy hồ sơ cán bộ.");
        var v = await ValidateAsync(id, r);

        staff.Staff.FullName = v.FullName;
        staff.Staff.DateOfBirth = r.DateOfBirth;
        staff.Staff.Gender = v.Gender;
        staff.Staff.PhoneNumber = v.Phone;
        staff.Staff.Email = v.Email;
        staff.Staff.UpdatedAt = clock.Now;
        staff.PrimaryCampusId = r.PrimaryCampusId;
        staff.DepartmentId = r.DepartmentId;
        staff.TeacherAbbr = v.Abbr;
        staff.Position = Guard.Optional(r.Position, "Chức danh", 100);
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task SetActiveAsync(int id, bool isActive)
    {
        var staff = await db.StaffProfile.FindAsync(id) ?? throw ApiException.NotFound("Không tìm thấy hồ sơ cán bộ.");
        if (!isActive)
        {
            var today = clock.Today;
            var duties = new List<string>();
            if (await db.CampusLeadership.Where(EffectivePeriod.EffectiveAt<CampusLeadership>(today)).AnyAsync(x => x.StaffId == id))
                duties.Add("phụ trách phân hiệu");
            if (await db.DepartmentHead.Where(EffectivePeriod.EffectiveAt<DepartmentHead>(today)).AnyAsync(x => x.StaffId == id))
                duties.Add("tổ trưởng");
            if (await db.HomeroomAssignment.Where(EffectivePeriod.EffectiveAt<HomeroomAssignment>(today)).AnyAsync(x => x.StaffId == id))
                duties.Add("chủ nhiệm");
            if (duties.Count > 0)
                throw ApiException.BadRequest($"Cán bộ đang giữ: {string.Join(", ", duties)}. Chuyển giao hoặc kết thúc các trách nhiệm đó trước.");

            var sessions = await db.TeachingSession.CountAsync(s => s.TeacherId == id && s.Status == "Scheduled" && s.SessionDate >= today);
            if (sessions > 0)
                throw ApiException.BadRequest($"Cán bộ còn {sessions} tiết Đã xếp lịch từ hôm nay. Đổi giáo viên hoặc hủy các tiết đó trước.");
        }
        staff.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    private Task<StaffDto?> ListByIdAsync(int id) =>
        db.StaffProfile.AsNoTracking().Where(s => s.StaffId == id)
            .Select(s => new StaffDto(s.StaffId, s.Staff.PersonCode, s.Staff.FullName, s.Staff.DateOfBirth, s.Staff.NationalId, s.Staff.Gender,
                s.Staff.PhoneNumber, s.Staff.Email, s.PrimaryCampusId, s.PrimaryCampus.Name, s.DepartmentId,
                s.Department == null ? null : s.Department.Name, s.TeacherAbbr, s.Position, s.IsActive,
                s.Staff.Account == null ? null : s.Staff.Account.AccountId,
                s.Staff.Account == null ? null : s.Staff.Account.AccountType,
                s.Staff.Account == null ? null : s.Staff.Account.Status))
            .FirstOrDefaultAsync();

    private record Validated(string FullName, string? NationalId, string? Gender, string? Phone, string? Email, string? Abbr);

    private async Task<Validated> ValidateAsync(int? selfId, StaffRequest r)
    {
        var name = Guard.Required(r.FullName, "Họ và tên", 150);
        if (r.DateOfBirth is not { } dob) throw ApiException.BadRequest("Ngày sinh không được để trống.");
        if (dob > clock.Today) throw ApiException.BadRequest("Ngày sinh không được ở tương lai.");

        var gender = Genders.Parse(r.Gender);
        if (gender == "?") throw ApiException.BadRequest("Giới tính chỉ gồm Nam, Nữ hoặc Khác.");

        var email = Guard.Optional(r.Email, "Email", 150);
        if (email is not null && !Guard.IsEmail(email)) throw ApiException.BadRequest("Email chưa đúng định dạng. Kiểm tra lại địa chỉ email.");

        var campus = await db.Campus.FindAsync(r.PrimaryCampusId) ?? throw ApiException.BadRequest("Chọn phân hiệu chính cho cán bộ.");
        if (!campus.IsActive) throw ApiException.BadRequest("Phân hiệu chính đã ngừng hoạt động. Chọn phân hiệu khác.");
        if (r.DepartmentId is { } did && !await db.SubjectDepartment.AnyAsync(d => d.DepartmentId == did))
            throw ApiException.BadRequest("Tổ chuyên môn không tồn tại.");

        string? abbr = null;
        if (!string.IsNullOrWhiteSpace(r.TeacherAbbr))
        {
            abbr = Guard.Abbreviation(r.TeacherAbbr, "Viết tắt giáo viên");
            if (await db.StaffProfile.AnyAsync(s => s.StaffId != selfId && s.TeacherAbbr == abbr))
                throw ApiException.BadRequest($"Viết tắt \"{abbr}\" đã được cán bộ khác dùng. Viết tắt giáo viên phải duy nhất toàn trường.");
        }

        // Số định danh chỉ nhập khi tạo; sửa hồ sơ không đổi được (BR-10).
        string? nationalId = null;
        if (selfId is null)
        {
            nationalId = Guard.NationalId(r.NationalId);
            var owner = await db.Person.Where(p => p.NationalId == nationalId).Select(p => new { p.PersonCode, p.FullName }).FirstOrDefaultAsync();
            if (owner is not null)
                throw ApiException.BadRequest($"Số định danh {nationalId} đã thuộc hồ sơ {owner.PersonCode} ({owner.FullName}). Mở hồ sơ đó để sửa thay vì tạo mới.");
        }

        return new Validated(name, nationalId, gender, Guard.Optional(r.PhoneNumber, "Số điện thoại", 20), email, abbr);
    }
}
