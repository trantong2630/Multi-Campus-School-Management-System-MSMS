using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Text;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;
using MSMS.Api.Modules.Sch.Dtos;

namespace MSMS.Api.Modules.Sch.Services;

/// <summary>SCH-05: danh sách học sinh của lớp, ghi danh, chuyển lớp giữa năm, kết thúc ghi danh (BR-09).</summary>
public sealed class EnrollmentService(MsmsDbContext db, IClock clock, CurrentUser user, ScopeResolver scope,
    AuditWriter audit, SessionService sessions)
{
    private const string Studying = "Studying";

    /// <summary>4.2.29 / 4.2.33: học sinh có ghi danh hiệu lực của lớp tại ngày xem (mặc định hôm nay).</summary>
    public async Task<RosterDto> GetRosterAsync(int classId, DateOnly? date)
    {
        var cls = await db.Class.AsNoTracking().FirstOrDefaultAsync(c => c.ClassId == classId)
                  ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        await EnsureCanReadRosterAsync(cls);

        var on = date ?? clock.Today;
        var students = await db.Enrollment.AsNoTracking().RosterAt(classId, on)
            .OrderBy(e => e.Student.Student.FullName)
            .Select(e => new RosterItemDto(e.EnrollmentId, e.StudentId, e.Student.Student.PersonCode, e.Student.Student.FullName,
                e.Student.Student.DateOfBirth, e.Student.Student.Gender, e.StartDate, e.EndDate, e.Reason))
            .ToListAsync();
        return new RosterDto(cls.ClassId, cls.Name, on, students);
    }

    /// <summary>Tìm học sinh đang học chưa có ghi danh còn hiệu lực trong năm học để chọn ghi danh vào lớp.</summary>
    public async Task<List<StudentCandidateDto>> FindCandidatesAsync(int yearId, string? search)
    {
        var today = clock.Today;
        var q = TextNormalizer.Normalize(search);
        return await db.StudentProfile.AsNoTracking()
            .Where(s => s.Status == Studying)
            .Where(s => !s.Enrollment.Any(e => e.Class.AcademicYearId == yearId && (e.EndDate == null || e.EndDate >= today)))
            .Where(s => q == "" || s.Student.FullName.Contains(q) || s.Student.PersonCode.Contains(q)
                        || (s.Student.NationalId != null && s.Student.NationalId.Contains(q)))
            .OrderBy(s => s.Student.FullName)
            .Take(30)
            .Select(s => new StudentCandidateDto(s.StudentId, s.Student.PersonCode, s.Student.FullName, s.Student.DateOfBirth))
            .ToListAsync();
    }

    /// <summary>4.2.30: ghi danh học sinh vào lớp; để trống ngày kết thúc nghĩa là tới hết năm học.</summary>
    public async Task EnrollAsync(int classId, EnrollRequest r)
    {
        var cls = await db.Class.Include(c => c.AcademicYear).FirstOrDefaultAsync(c => c.ClassId == classId)
                  ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        if (!cls.IsActive) throw ApiException.BadRequest("Lớp đã ngừng hoạt động. Kích hoạt lại lớp trước khi ghi danh.");
        var student = await db.StudentProfile.Include(s => s.Student).FirstOrDefaultAsync(s => s.StudentId == r.StudentId)
                      ?? throw ApiException.BadRequest("Không tìm thấy học sinh. Chọn lại học sinh từ danh sách.");
        if (student.Status != Studying)
            throw ApiException.BadRequest($"{student.Student.FullName} không còn ở trạng thái Đang học nên không ghi danh được.");

        EnsureInYear(cls.AcademicYear, r.StartDate);
        await EnsureNoOverlapAsync(student, cls.AcademicYear, r.StartDate);

        db.Enrollment.Add(new Enrollment { StudentId = r.StudentId, ClassId = classId, StartDate = r.StartDate, CreatedAt = clock.Now });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// 4.2.31: chuyển lớp giữa năm trong một transaction. Ghi danh cũ kết thúc vào ngày liền trước ngày hiệu lực,
    /// ghi danh mới mở từ ngày hiệu lực; dữ liệu đã phát sinh ở lớp cũ giữ nguyên (BR-09).
    /// </summary>
    public async Task TransferAsync(int enrollmentId, TransferRequest r)
    {
        var old = await FindEnrollmentAsync(enrollmentId);
        if (old.EndDate != null)
            throw ApiException.BadRequest("Ghi danh này đã kết thúc hoặc đã có lịch chuyển lớp. Chỉ chuyển được ghi danh đang mở.");
        var target = await db.Class.Include(c => c.AcademicYear).FirstOrDefaultAsync(c => c.ClassId == r.ToClassId)
                     ?? throw ApiException.BadRequest("Chọn lớp muốn chuyển đến.");
        if (target.ClassId == old.ClassId) throw ApiException.BadRequest("Lớp chuyển đến trùng lớp hiện tại. Chọn lớp khác.");
        if (target.AcademicYearId != old.Class.AcademicYearId)
            throw ApiException.BadRequest("Chỉ chuyển lớp trong cùng năm học. Sang năm mới dùng Kết chuyển năm học.");
        if (!target.IsActive) throw ApiException.BadRequest("Lớp chuyển đến đã ngừng hoạt động. Chọn lớp khác.");
        EnsureInYear(target.AcademicYear, r.EffectiveDate);

        await using var tx = await db.Database.BeginTransactionAsync();
        EffectivePeriod.CloseBefore(old, r.EffectiveDate);
        old.Reason = EnrollmentCloseReasons.ClassTransfer;
        db.Enrollment.Add(new Enrollment { StudentId = old.StudentId, ClassId = target.ClassId, StartDate = r.EffectiveDate, CreatedAt = clock.Now });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>
    /// 4.2.32: kết thúc ghi danh đang mở, ngày kết thúc là ngày thao tác (BR-09). Chuyển trường hoặc tốt nghiệp
    /// đồng thời đổi trạng thái học sinh và khóa tài khoản học sinh trong cùng thao tác (BR-05).
    /// </summary>
    public async Task CloseAsync(int enrollmentId, CloseEnrollmentRequest r)
    {
        if (!EnrollmentCloseReasons.All.Contains(r.Reason))
            throw ApiException.BadRequest("Chọn lý do kết thúc ghi danh: chuyển lớp, chuyển trường hoặc tốt nghiệp.");
        var e = await FindEnrollmentAsync(enrollmentId);
        if (e.EndDate != null) throw ApiException.BadRequest("Ghi danh này đã kết thúc từ trước.");

        var today = clock.Today;
        await using var tx = await db.Database.BeginTransactionAsync();
        var endDate = e.StartDate > today ? e.StartDate : today;   // ghi danh chưa hiệu lực thì rút về đúng ngày bắt đầu
        var yearEnd = e.Class.AcademicYear.EndDate;
        e.EndDate = endDate > yearEnd ? yearEnd : endDate;          // đóng sau khi hết năm học thì ghi đúng ngày cuối năm học
        e.Reason = r.Reason;

        int? lockedAccountId = null;
        if (r.Reason != EnrollmentCloseReasons.ClassTransfer)
        {
            e.Student.Status = r.Reason == EnrollmentCloseReasons.Graduated ? "Graduated" : "Transferred";
            e.Student.LeftDate = e.EndDate;
            var account = await db.Account.FirstOrDefaultAsync(a => a.PersonId == e.StudentId);
            if (account is { Status: not AccountStatuses.Locked })
            {
                account.Status = AccountStatuses.Locked;
                lockedAccountId = account.AccountId;
                audit.Add(AuditCategories.Account, "LockAccount", [account.AccountId], new { reason = r.Reason, enrollmentId });
            }
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        if (lockedAccountId is int id) await sessions.RevokeAllAsync(id);
    }

    private async Task<Enrollment> FindEnrollmentAsync(int id) =>
        await db.Enrollment.Include(e => e.Class).ThenInclude(c => c.AcademicYear).Include(e => e.Student).FirstOrDefaultAsync(e => e.EnrollmentId == id)
        ?? throw ApiException.NotFound("Không tìm thấy ghi danh.");

    private static void EnsureInYear(AcademicYear year, DateOnly date)
    {
        if (date < year.StartDate || date > year.EndDate)
            throw ApiException.BadRequest(
                $"Ngày hiệu lực phải nằm trong năm học {year.Name} ({year.StartDate:dd/MM/yyyy} – {year.EndDate:dd/MM/yyyy}).");
    }

    /// <summary>Mỗi học sinh một ghi danh mở trong một năm học: khoảng mới không được chạm khoảng nào đã có (BR-09).</summary>
    private async Task EnsureNoOverlapAsync(StudentProfile student, AcademicYear year, DateOnly start)
    {
        var existing = await db.Enrollment.AsNoTracking()
            .Where(e => e.StudentId == student.StudentId && e.Class.AcademicYearId == year.AcademicYearId)
            .Select(e => new { e.StartDate, e.EndDate, ClassName = e.Class.Name })
            .ToListAsync();
        var clash = existing.FirstOrDefault(e =>
            EnrollmentRules.Overlaps(start, year.EndDate, e.StartDate, EnrollmentRules.EffectiveEnd(e.EndDate, year.EndDate)));
        if (clash is not null)
            throw ApiException.BadRequest(
                $"{student.Student.FullName} đã có ghi danh ở lớp {clash.ClassName} trong năm học {year.Name}. " +
                "Dùng Chuyển lớp, hoặc kết thúc ghi danh cũ trước khi ghi danh lớp mới.");
    }

    /// <summary>Quản lý và hiệu phó phân hiệu đọc được; giáo viên chỉ đọc lớp mình đang chủ nhiệm.</summary>
    private async Task EnsureCanReadRosterAsync(Class cls)
    {
        switch (user.AccountType)
        {
            case AccountTypes.SchoolAdmin or AccountTypes.Principal:
                return;
            case AccountTypes.VicePrincipal when await scope.CanReadCampusAsync(cls.CampusId):
                return;
            case AccountTypes.Teacher:
                var today = clock.Today;
                if (await db.HomeroomAssignment.Where(EffectivePeriod.EffectiveAt<HomeroomAssignment>(today))
                        .AnyAsync(h => h.ClassId == cls.ClassId && h.StaffId == user.PersonId))
                    return;
                break;
        }
        throw ApiException.Forbidden("Không có quyền xem danh sách học sinh của lớp này.");
    }
}
