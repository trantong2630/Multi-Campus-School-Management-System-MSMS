using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
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
public sealed class StaffService(MsmsDbContext db, IClock clock, CurrentUser user, ScopeResolver scope)
{
    /// <summary>
    /// 4.3.2: Hồ sơ cán bộ, chỉ xem, cùng phạm vi danh bạ (School Admin, Hiệu trưởng toàn trường; Hiệu phó theo phân hiệu chính của cán bộ).
    /// Phần giảng dạy cho cả ba vai trò quản lý; Hiệu phó chỉ thấy lớp của phân hiệu mình phụ trách.
    /// </summary>
    public async Task<StaffDetailDto> GetDetailAsync(int id)
    {
        var visible = await scope.FilterStaffAsync(db.StaffProfile.AsNoTracking().Where(s => s.StaffId == id));
        if (!await visible.AnyAsync()) throw ApiException.NotFound("Không tìm thấy hồ sơ cán bộ.");

        var profile = await GetAsync(id);
        var extra = await db.StaffProfile.AsNoTracking().Where(s => s.StaffId == id)
            .Select(s => new { s.Staff.Address, s.HireDate }).FirstAsync();
        var today = clock.Today;
        bool Current(DateOnly start, DateOnly? end) => start <= today && (end == null || end > today);

        var campus = await db.CampusLeadership.AsNoTracking().Where(x => x.StaffId == id)
            .Select(x => new { Name = x.Campus.Name, x.StartDate, x.EndDate }).ToListAsync();
        var dept = await db.DepartmentHead.AsNoTracking().Where(x => x.StaffId == id)
            .Select(x => new { Name = x.Department.Name, x.StartDate, x.EndDate }).ToListAsync();
        var homeroom = await db.HomeroomAssignment.AsNoTracking().Where(x => x.StaffId == id)
            .Select(x => new { Name = x.Class.Name, Group = x.Class.Campus.Name + " · " + x.Class.AcademicYear.Name, x.StartDate, x.EndDate }).ToListAsync();
        var responsibilities = campus.Select(x => new StaffResponsibilityDto(ResponsibilityKinds.Campus, x.Name, null, x.StartDate, x.EndDate, Current(x.StartDate, x.EndDate)))
            .Concat(dept.Select(x => new StaffResponsibilityDto(ResponsibilityKinds.Department, x.Name, null, x.StartDate, x.EndDate, Current(x.StartDate, x.EndDate))))
            .Concat(homeroom.Select(x => new StaffResponsibilityDto(ResponsibilityKinds.Homeroom, x.Name, x.Group, x.StartDate, x.EndDate, Current(x.StartDate, x.EndDate))))
            .OrderByDescending(r => r.IsCurrent).ThenByDescending(r => r.StartDate).ToList();

        return new StaffDetailDto(profile, extra.Address, extra.HireDate, responsibilities, await TeachingAsync(id));
    }

    private async Task<List<StaffTeachingDto>> TeachingAsync(int staffId)
    {
        var today = clock.Today;
        var semesterId = await db.Semester.AsNoTracking()
            .Where(s => s.AcademicYear.IsCurrent && s.StartDate <= today && s.EndDate >= today)
            .Select(s => (int?)s.SemesterId).FirstOrDefaultAsync();
        if (semesterId is null) return [];

        var entries = PublishedTimetable.LatestEntries(db, semesterId.Value).Where(e => e.TeacherId == staffId);
        if (user.AccountType == AccountTypes.VicePrincipal)
        {
            var campuses = await scope.LedCampusIdsAsync();
            entries = entries.Where(e => campuses.Contains(e.Class.CampusId));
        }
        var rows = await entries.GroupBy(e => new { e.ClassId, ClassName = e.Class.Name, CampusName = e.Class.Campus.Name, e.SubjectId, SubjectName = e.Subject.Name })
            .Select(g => new { g.Key.ClassId, g.Key.ClassName, g.Key.CampusName, g.Key.SubjectId, g.Key.SubjectName, Periods = g.Count() }).ToListAsync();
        return rows.OrderBy(r => r.ClassName).ThenBy(r => r.SubjectName)
            .Select(r => new StaffTeachingDto(r.ClassId, r.ClassName, r.CampusName, r.SubjectId, r.SubjectName, r.Periods)).ToList();
    }

    public async Task<PagedResult<StaffDto>> ListAsync(int? campusId, int? departmentId, string? accountType, bool? hasAbbr,
        string? search, bool includeInactive, int page, int pageSize)
    {
        var query = (await scope.FilterStaffAsync(db.StaffProfile.AsNoTracking()))
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

        return await query.OrderBy(s => s.Staff.FullName)
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
            if (await db.CampusLeadership.Where(EffectivePeriod.CurrentResponsibility<CampusLeadership>(today)).AnyAsync(x => x.StaffId == id))
                duties.Add("phụ trách phân hiệu");
            if (await db.DepartmentHead.Where(EffectivePeriod.CurrentResponsibility<DepartmentHead>(today)).AnyAsync(x => x.StaffId == id))
                duties.Add("tổ trưởng");
            if (await db.HomeroomAssignment.Where(EffectivePeriod.CurrentResponsibility<HomeroomAssignment>(today)).AnyAsync(x => x.StaffId == id))
                duties.Add("chủ nhiệm");
            if (duties.Count > 0)
                throw ApiException.BadRequest($"Cán bộ đang giữ: {string.Join(", ", duties)}. Chuyển giao hoặc kết thúc các trách nhiệm đó trước.");

            var sessions = await db.TeachingSession.Where(SessionLockRules.NotEnded(clock)).CountAsync(s => s.TeacherId == id && s.Status == "Scheduled");
            if (sessions > 0)
                throw ApiException.BadRequest($"Cán bộ còn {sessions} tiết Đã xếp lịch chưa kết thúc. Đổi giáo viên hoặc hủy các tiết đó trước.");

            // Lịch hẹn chưa bắt đầu hủy luôn: người đã nghỉ không được nhận trách nhiệm khi tới ngày (BR-09).
            await ResponsibilityService.UpcomingDutiesAsync(db, id, today, cancel: true);
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
        if (!Guard.StaffAgeOk(dob, clock.Today)) throw ApiException.BadRequest(Guard.StaffAgeMessage);

        var gender = Genders.Parse(r.Gender);
        if (gender == "?") throw ApiException.BadRequest("Giới tính chỉ gồm Nam, Nữ hoặc Khác.");

        var email = Guard.Optional(r.Email, "Email", 150);
        if (email is not null && !Guard.IsEmail(email)) throw ApiException.BadRequest("Email chưa đúng định dạng. Kiểm tra lại địa chỉ email.");

        var campus = await db.Campus.FindAsync(r.PrimaryCampusId) ?? throw ApiException.BadRequest("Chọn phân hiệu chính cho cán bộ.");
        if (!campus.IsActive) throw ApiException.BadRequest("Phân hiệu chính đã ngừng hoạt động. Chọn phân hiệu khác.");
        if (r.DepartmentId is { } did)
        {
            var dept = await db.SubjectDepartment.AsNoTracking().FirstOrDefaultAsync(d => d.DepartmentId == did)
                       ?? throw ApiException.BadRequest("Tổ chuyên môn không tồn tại.");
            // Tổ ngừng hoạt động không chọn được cho tổ mới; cán bộ đang thuộc tổ đó vẫn sửa được các trường khác mà không phải đổi tổ.
            var keepsCurrent = selfId is not null && await db.StaffProfile.AnyAsync(s => s.StaffId == selfId && s.DepartmentId == did);
            if (!dept.IsActive && !keepsCurrent)
                throw ApiException.BadRequest("Tổ chuyên môn đã ngừng hoạt động. Chọn tổ còn hoạt động.");
        }

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

        return new Validated(name, nationalId, gender, Guard.MobilePhone(r.PhoneNumber), email, abbr);
    }
}
