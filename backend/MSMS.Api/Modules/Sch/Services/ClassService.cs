using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;

namespace MSMS.Api.Modules.Sch.Services;

/// <summary>SCH-05: lớp theo phân hiệu và năm học. Ghi danh học sinh làm cùng danh bạ học sinh (PPL-02).</summary>
public sealed class ClassService(MsmsDbContext db, IClock clock)
{
    public async Task<List<ClassDto>> ListAsync(int? campusId, int? yearId, bool includeInactive)
    {
        var today = clock.Today;
        return await db.Class.AsNoTracking()
            .Where(c => (campusId == null || c.CampusId == campusId)
                        && (yearId == null || c.AcademicYearId == yearId)
                        && (includeInactive || c.IsActive))
            .OrderBy(c => c.Campus.Name).ThenBy(c => c.GradeLevel).ThenBy(c => c.Name)
            .Select(c => new ClassDto(c.ClassId, c.CampusId, c.Campus.Name, c.AcademicYearId, c.AcademicYear.Name,
                c.Name, c.GradeLevel, c.IsActive,
                c.Enrollment.AsQueryable().Count(EnrollmentRules.ActiveAt(today)),
                c.HomeroomAssignment
                    .Where(h => h.StartDate <= today && (h.EndDate == null || h.EndDate >= today))
                    .Select(h => h.Staff.Staff.FullName).FirstOrDefault()))
            .ToListAsync();
    }

    public async Task<ClassDto> CreateAsync(ClassRequest r)
    {
        var name = Guard.Required(r.Name, "Tên lớp", 50);
        EnsureGrade(r.GradeLevel);
        var campus = await db.Campus.FindAsync(r.CampusId) ?? throw ApiException.BadRequest("Chọn phân hiệu cho lớp.");
        if (!campus.IsActive) throw ApiException.BadRequest("Phân hiệu đã ngừng hoạt động. Chọn phân hiệu khác.");
        var year = await db.AcademicYear.FindAsync(r.AcademicYearId) ?? throw ApiException.BadRequest("Chọn năm học cho lớp.");
        if (await db.Class.AnyAsync(c => c.CampusId == r.CampusId && c.AcademicYearId == r.AcademicYearId && c.Name == name))
            throw ApiException.BadRequest($"Lớp \"{name}\" đã có ở {campus.Name}, năm học {year.Name}. Tên lớp phải duy nhất trong phạm vi đó.");

        var cls = new Class
        {
            CampusId = r.CampusId, AcademicYearId = r.AcademicYearId, Name = name, GradeLevel = r.GradeLevel,
            IsActive = true, CreatedAt = clock.Now,
        };
        db.Class.Add(cls);
        await db.SaveChangesAsync();
        return new ClassDto(cls.ClassId, campus.CampusId, campus.Name, year.AcademicYearId, year.Name, name, cls.GradeLevel, true, 0, null);
    }

    public async Task UpdateAsync(int id, ClassUpdateRequest r)
    {
        var cls = await db.Class.FindAsync(id) ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        var name = Guard.Required(r.Name, "Tên lớp", 50);
        EnsureGrade(r.GradeLevel);
        if (await db.Class.AnyAsync(c => c.ClassId != id && c.CampusId == cls.CampusId && c.AcademicYearId == cls.AcademicYearId && c.Name == name))
            throw ApiException.BadRequest($"Lớp \"{name}\" đã có trong cùng phân hiệu và năm học. Chọn tên khác.");
        cls.Name = name;
        cls.GradeLevel = r.GradeLevel;
        await db.SaveChangesAsync();
    }

    public async Task SetActiveAsync(int id, bool isActive)
    {
        var cls = await db.Class.FindAsync(id) ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        if (!isActive)
        {
            var today = clock.Today;
            var open = await db.Enrollment.Where(EnrollmentRules.ActiveAt(today)).CountAsync(e => e.ClassId == id);
            if (open > 0)
                throw ApiException.BadRequest($"Lớp còn {open} học sinh có ghi danh đang mở. Chuyển hoặc kết thúc ghi danh trước.");
        }
        cls.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    private static void EnsureGrade(byte grade)
    {
        if (grade is < 6 or > 9) throw ApiException.BadRequest("Khối chỉ từ 6 đến 9.");
    }
}
