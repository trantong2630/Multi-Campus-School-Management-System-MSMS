using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Ppl.Services;

/// <summary>PPL-02: danh bạ học sinh (4.3.12–4.3.15). Import chỉ tạo hồ sơ, sửa hồ sơ luôn làm ở đây (BR-10, BR-11).</summary>
public sealed class StudentService(MsmsDbContext db, IClock clock, ScopeResolver scope, EnrollmentService enrollments)
{
    public static readonly string[] Statuses = ["Studying", "Transferred", "Graduated"];

    public async Task<PagedResult<StudentDto>> ListAsync(int? yearId, int? campusId, int? classId, string? status, string? search, int page, int pageSize)
    {
        var today = clock.Today;
        if (status is not null && !Statuses.Contains(status)) throw ApiException.BadRequest("Trạng thái học sinh không hợp lệ.");

        var query = await scope.FilterStudentsAsync(db.StudentProfile.AsNoTracking());
        query = query.Where(s => status == null || s.Status == status);

        // "Lớp đang học": ghi danh trong năm được chọn, hoặc ghi danh hiệu lực hôm nay (BR-09) nếu không chọn năm.
        Expression<Func<Enrollment, bool>> inScope = yearId != null ? e => e.Class.AcademicYearId == yearId : EnrollmentRules.ActiveAt(today);
        if (yearId != null || campusId != null || classId != null)
            query = query.Where(s => s.Enrollment.AsQueryable().Where(inScope).Any(e =>
                (campusId == null || e.Class.CampusId == campusId)
                && (classId == null || e.ClassId == classId)));

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(s => s.Student.FullName.Contains(term) || s.Student.PersonCode.Contains(term));

        return await query.OrderBy(s => s.Student.FullName).ThenBy(s => s.StudentId)
            .Select(s => new
            {
                s,
                Cur = s.Enrollment.AsQueryable().Where(inScope)
                    .OrderByDescending(e => e.StartDate).Select(e => new { e.ClassId, ClassName = e.Class.Name, e.Class.CampusId, CampusName = e.Class.Campus.Name })
                    .FirstOrDefault(),
                Guardian = s.ParentStudentLink.Where(l => l.IsPrimary).Select(l => new { l.Parent.FullName, l.Parent.PhoneNumber }).FirstOrDefault(),
            })
            .Select(x => new StudentDto(x.s.StudentId, x.s.Student.PersonCode, x.s.Student.FullName, x.s.Student.DateOfBirth, x.s.Student.NationalId!,
                x.s.Student.Gender, x.s.Student.PhoneNumber, x.s.Student.Address, x.s.Status, x.s.Note,
                x.Cur == null ? null : x.Cur.CampusId, x.Cur == null ? null : x.Cur.CampusName,
                x.Cur == null ? null : x.Cur.ClassId, x.Cur == null ? null : x.Cur.ClassName,
                x.Guardian == null ? null : x.Guardian.FullName, x.Guardian == null ? null : x.Guardian.PhoneNumber))
            .ToPagedAsync(page, pageSize);
    }

    public async Task<StudentDto> CreateAsync(StudentRequest r)
    {
        var v = await ValidateAsync(null, r);
        await using var tx = await db.Database.BeginTransactionAsync();
        var person = new Person
        {
            PersonType = "Student", PersonCode = await PersonCodes.NextAsync(db, PersonCodes.Student),
            FullName = v.FullName, DateOfBirth = r.DateOfBirth, NationalId = v.NationalId, Gender = v.Gender, PhoneNumber = v.Phone, Address = v.Address,
            CreatedAt = clock.Now,
        };
        db.Person.Add(person);
        db.StudentProfile.Add(new StudentProfile { Student = person, Status = "Studying", AdmissionDate = clock.Today, Note = v.Note });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return await GetAsync(person.PersonId);
    }

    public async Task<StudentDto> UpdateAsync(int id, StudentRequest r)
    {
        var student = await db.StudentProfile.Include(s => s.Student).FirstOrDefaultAsync(s => s.StudentId == id)
            ?? throw ApiException.NotFound("Không tìm thấy hồ sơ học sinh.");
        var v = await ValidateAsync(id, r);

        student.Student.FullName = v.FullName;
        student.Student.DateOfBirth = r.DateOfBirth;
        student.Student.Gender = v.Gender;
        student.Student.PhoneNumber = v.Phone;
        student.Student.Address = v.Address;
        student.Student.UpdatedAt = clock.Now;
        student.Note = v.Note;
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    /// <summary>
    /// 4.3.19–4.3.20: học sinh rời trường (chuyển trường hoặc tốt nghiệp). Đóng ghi danh đang mở, đổi trạng thái hồ sơ và khóa tài khoản
    /// trong cùng một transaction (BR-05); một em không hợp lệ thì không em nào bị đổi. Tốt nghiệp chỉ dành cho học sinh khối cuối cấp.
    /// </summary>
    public async Task LeaveAsync(StudentLeaveRequest r)
    {
        if (r.Reason is not (EnrollmentCloseReasons.SchoolTransfer or EnrollmentCloseReasons.Graduated))
            throw ApiException.BadRequest("Lý do chỉ gồm chuyển trường hoặc tốt nghiệp.");
        var ids = r.StudentIds?.Distinct().ToList() ?? [];
        if (ids.Count == 0) throw ApiException.BadRequest("Chọn ít nhất một học sinh.");

        var today = clock.Today;
        var open = await db.Enrollment.AsNoTracking().Where(EnrollmentRules.ActiveAt(today))
            .Where(e => ids.Contains(e.StudentId) && e.Student.Status == "Studying")
            .Select(e => new { e.EnrollmentId, e.StudentId, StudentName = e.Student.Student.FullName, ClassName = e.Class.Name, e.Class.GradeLevel })
            .ToListAsync();

        var missing = ids.Except(open.Select(o => o.StudentId)).ToList();
        if (missing.Count > 0)
        {
            var names = await db.Person.Where(p => missing.Contains(p.PersonId)).Select(p => p.FullName).Take(3).ToListAsync();
            throw ApiException.BadRequest($"{string.Join(", ", names)}: không còn học hoặc chưa có ghi danh đang mở. Tải lại danh sách rồi chọn lại.");
        }

        if (r.Reason == EnrollmentCloseReasons.Graduated)
        {
            var notFinal = open.FirstOrDefault(o => RolloverRules.NextGrade(o.GradeLevel) is not null);
            if (notFinal is not null)
                throw ApiException.BadRequest($"{notFinal.StudentName} đang học lớp {notFinal.ClassName}, chưa phải khối cuối cấp nên không cho tốt nghiệp được.");
        }

        await enrollments.CloseManyAsync(new CloseManyRequest(open.Select(o => o.EnrollmentId).ToList(), r.Reason));
    }

    private async Task<StudentDto> GetAsync(int id)
    {
        var today = clock.Today;
        var s = await db.StudentProfile.AsNoTracking().Include(x => x.Student).FirstOrDefaultAsync(x => x.StudentId == id)
            ?? throw ApiException.NotFound("Không tìm thấy hồ sơ học sinh.");
        var cur = await db.Enrollment.AsNoTracking()
            .Where(EnrollmentRules.ActiveAt(today)).Where(e => e.StudentId == id)
            .OrderByDescending(e => e.StartDate)
            .Select(e => new { e.ClassId, ClassName = e.Class.Name, e.Class.CampusId, CampusName = e.Class.Campus.Name }).FirstOrDefaultAsync();
        var g = await db.ParentStudentLink.AsNoTracking().Where(l => l.StudentId == id && l.IsPrimary)
            .Select(l => new { l.Parent.FullName, l.Parent.PhoneNumber }).FirstOrDefaultAsync();
        return new StudentDto(s.StudentId, s.Student.PersonCode, s.Student.FullName, s.Student.DateOfBirth, s.Student.NationalId!, s.Student.Gender,
            s.Student.PhoneNumber, s.Student.Address, s.Status, s.Note, cur?.CampusId, cur?.CampusName, cur?.ClassId, cur?.ClassName, g?.FullName, g?.PhoneNumber);
    }

    private record Validated(string FullName, string? NationalId, string? Gender, string? Phone, string? Address, string? Note);

    private async Task<Validated> ValidateAsync(int? selfId, StudentRequest r)
    {
        var name = Guard.Required(r.FullName, "Họ và tên", 150);
        if (r.DateOfBirth is not { } dob) throw ApiException.BadRequest("Ngày sinh không được để trống.");
        if (dob > clock.Today) throw ApiException.BadRequest("Ngày sinh không được ở tương lai.");
        if (!Guard.StudentAgeOk(dob, clock.Today)) throw ApiException.BadRequest(Guard.StudentAgeMessage);

        var gender = Genders.Parse(r.Gender);
        if (gender == "?") throw ApiException.BadRequest("Giới tính chỉ gồm Nam, Nữ hoặc Khác.");

        // Số định danh chỉ nhập khi tạo, duy nhất toàn trường; sửa hồ sơ không đổi được (BR-10).
        string? nationalId = null;
        if (selfId is null)
        {
            nationalId = Guard.NationalId(r.NationalId);
            var owner = await db.Person.Where(p => p.NationalId == nationalId).Select(p => new { p.PersonCode, p.FullName }).FirstOrDefaultAsync();
            if (owner is not null)
                throw ApiException.BadRequest($"Số định danh {nationalId} đã thuộc hồ sơ {owner.PersonCode} ({owner.FullName}). Mở hồ sơ đó để sửa thay vì tạo mới.");
        }

        return new Validated(name, nationalId, gender, Guard.MobilePhone(r.PhoneNumber),
            Guard.Optional(r.Address, "Địa chỉ", 255), Guard.Optional(r.Note, "Ghi chú", 500));
    }
}
