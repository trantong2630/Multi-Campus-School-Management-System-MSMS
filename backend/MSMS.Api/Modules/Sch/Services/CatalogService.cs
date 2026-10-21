using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;

namespace MSMS.Api.Modules.Sch.Services;

/// <summary>SCH-02: tổ chuyên môn và môn học (kèm viết tắt môn dùng trong file thời khóa biểu).</summary>
public sealed class CatalogService(MsmsDbContext db, IClock clock)
{
    // ---- Tổ chuyên môn ----
    public async Task<List<DepartmentDto>> ListDepartmentsAsync() =>
        await db.SubjectDepartment.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.DepartmentId, d.Name, d.Description, d.IsActive, d.Subject.Count(s => s.IsActive)))
            .ToListAsync();

    public async Task<DepartmentDto> CreateDepartmentAsync(DepartmentRequest r)
    {
        var name = Guard.Required(r.Name, "Tên tổ chuyên môn", 150);
        if (await db.SubjectDepartment.AnyAsync(d => d.Name == name))
            throw ApiException.BadRequest($"Tổ chuyên môn \"{name}\" đã có. Chọn tên khác.");
        var dept = new SubjectDepartment { Name = name, Description = Guard.Optional(r.Description, "Mô tả", 255), IsActive = true };
        db.SubjectDepartment.Add(dept);
        await db.SaveChangesAsync();
        return new DepartmentDto(dept.DepartmentId, dept.Name, dept.Description, true, 0);
    }

    public async Task UpdateDepartmentAsync(int id, DepartmentRequest r)
    {
        var dept = await db.SubjectDepartment.FindAsync(id) ?? throw ApiException.NotFound("Không tìm thấy tổ chuyên môn.");
        var name = Guard.Required(r.Name, "Tên tổ chuyên môn", 150);
        if (await db.SubjectDepartment.AnyAsync(d => d.DepartmentId != id && d.Name == name))
            throw ApiException.BadRequest($"Tổ chuyên môn \"{name}\" đã có. Chọn tên khác.");
        dept.Name = name;
        dept.Description = Guard.Optional(r.Description, "Mô tả", 255);
        await db.SaveChangesAsync();
    }

    public async Task SetDepartmentActiveAsync(int id, bool isActive)
    {
        var dept = await db.SubjectDepartment.FindAsync(id) ?? throw ApiException.NotFound("Không tìm thấy tổ chuyên môn.");
        if (!isActive)
        {
            var today = clock.Today;
            var subjects = await db.Subject.CountAsync(s => s.DepartmentId == id && s.IsActive);
            if (subjects > 0)
                throw ApiException.BadRequest($"Tổ còn {subjects} môn đang hoạt động. Ngừng hoạt động các môn đó trước.");
            if (await db.DepartmentHead.Where(EffectivePeriod.EffectiveAt<DepartmentHead>(today)).AnyAsync(h => h.DepartmentId == id))
                throw ApiException.BadRequest("Tổ đang có tổ trưởng. Kết thúc trách nhiệm tổ trưởng trước.");
        }
        dept.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    // ---- Môn học ----
    public async Task<List<SubjectDto>> ListSubjectsAsync(int? departmentId) =>
        await db.Subject.AsNoTracking()
            .Where(s => departmentId == null || s.DepartmentId == departmentId)
            .OrderBy(s => s.Department.Name).ThenBy(s => s.Name)
            .Select(s => new SubjectDto(s.SubjectId, s.DepartmentId, s.Department.Name, s.Name, s.Abbreviation, s.IsActive))
            .ToListAsync();

    public async Task<SubjectDto> CreateSubjectAsync(SubjectRequest r)
    {
        var (name, abbr) = await ValidateSubjectAsync(null, r);
        var subject = new Subject { DepartmentId = r.DepartmentId, Name = name, Abbreviation = abbr, IsActive = true };
        db.Subject.Add(subject);
        await db.SaveChangesAsync();
        var deptName = await db.SubjectDepartment.Where(d => d.DepartmentId == r.DepartmentId).Select(d => d.Name).FirstAsync();
        return new SubjectDto(subject.SubjectId, subject.DepartmentId, deptName, subject.Name, subject.Abbreviation, true);
    }

    /// <summary>Đổi viết tắt chỉ ảnh hưởng các lần import sau, không sửa dữ liệu đã có.</summary>
    public async Task UpdateSubjectAsync(int id, SubjectRequest r)
    {
        var subject = await db.Subject.FindAsync(id) ?? throw ApiException.NotFound("Không tìm thấy môn học.");
        var (name, abbr) = await ValidateSubjectAsync(id, r);
        subject.DepartmentId = r.DepartmentId;
        subject.Name = name;
        subject.Abbreviation = abbr;
        await db.SaveChangesAsync();
    }

    public async Task SetSubjectActiveAsync(int id, bool isActive)
    {
        var subject = await db.Subject.FindAsync(id) ?? throw ApiException.NotFound("Không tìm thấy môn học.");
        subject.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    private async Task<(string Name, string Abbr)> ValidateSubjectAsync(int? selfId, SubjectRequest r)
    {
        var name = Guard.Required(r.Name, "Tên môn học", 150);
        var abbr = Guard.Abbreviation(r.Abbreviation, "Viết tắt môn");
        var dept = await db.SubjectDepartment.FindAsync(r.DepartmentId)
            ?? throw ApiException.BadRequest("Chọn tổ chuyên môn cho môn học.");
        if (!dept.IsActive) throw ApiException.BadRequest("Tổ chuyên môn đã ngừng hoạt động. Chọn tổ khác.");
        if (await db.Subject.AnyAsync(s => s.SubjectId != selfId && s.Name == name))
            throw ApiException.BadRequest($"Môn học \"{name}\" đã có. Chọn tên khác.");
        if (await db.Subject.AnyAsync(s => s.SubjectId != selfId && s.Abbreviation == abbr))
            throw ApiException.BadRequest($"Viết tắt \"{abbr}\" đã được môn khác dùng. Viết tắt môn phải duy nhất toàn trường.");
        return (name, abbr);
    }
}
