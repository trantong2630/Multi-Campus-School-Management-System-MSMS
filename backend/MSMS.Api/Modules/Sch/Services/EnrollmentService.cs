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
        var cls = await db.Class.AsNoTracking().Include(c => c.AcademicYear).FirstOrDefaultAsync(c => c.ClassId == classId)
                  ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        await EnsureCanReadRosterAsync(cls);

        var on = date ?? EnrollmentRules.DefaultViewDate(clock.Today, cls.AcademicYear.StartDate);
        var students = await db.Enrollment.AsNoTracking().RosterAt(classId, on)
            .OrderBy(e => e.Student.Student.FullName)
            .Select(e => new RosterItemDto(e.EnrollmentId, e.StudentId, e.Student.Student.PersonCode, e.Student.Student.FullName,
                e.Student.Student.DateOfBirth, e.Student.Student.Gender, e.StartDate, e.EndDate, e.Reason))
            .ToListAsync();
        return new RosterDto(cls.ClassId, cls.Name, on, students);
    }

    /// <summary>Tìm học sinh đang học chưa có ghi danh còn hiệu lực trong năm học để chọn ghi danh vào lớp.</summary>
    public async Task<List<StudentCandidateDto>> FindCandidatesAsync(int yearId, string? search, int limit = 30)
    {
        limit = Math.Clamp(limit, 1, 300);
        var today = clock.Today;
        var q = TextNormalizer.Normalize(search);
        return await db.StudentProfile.AsNoTracking()
            .Where(s => s.Status == Studying)
            .Where(s => !s.Enrollment.Any(e => e.Class.AcademicYearId == yearId && (e.EndDate == null || e.EndDate >= today)))
            .Where(s => q == "" || s.Student.FullName.Contains(q) || s.Student.PersonCode.Contains(q)
                        || (s.Student.NationalId != null && s.Student.NationalId.Contains(q)))
            .OrderBy(s => s.Student.FullName)
            .Take(limit)
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
        var target = await db.Class.Include(c => c.AcademicYear).FirstOrDefaultAsync(c => c.ClassId == r.ToClassId)
                     ?? throw ApiException.BadRequest("Chọn lớp muốn chuyển đến.");
        TransferCore(old, target, r.EffectiveDate);

        await using var tx = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Chuyển lớp giữa năm cho nhiều học sinh cùng lúc tới một lớp, cùng ngày hiệu lực. Một em sai thì không chuyển em nào.</summary>
    public async Task TransferManyAsync(TransferManyRequest r)
    {
        if (r.EnrollmentIds is not { Count: > 0 }) throw ApiException.BadRequest("Chọn ít nhất một học sinh để chuyển lớp.");
        var target = await db.Class.Include(c => c.AcademicYear).FirstOrDefaultAsync(c => c.ClassId == r.ToClassId)
                     ?? throw ApiException.BadRequest("Chọn lớp muốn chuyển đến.");
        var items = await LoadManyAsync(r.EnrollmentIds);
        foreach (var old in items)
        {
            try { TransferCore(old, target, r.EffectiveDate); }
            catch (ApiException ex) { throw ApiException.BadRequest($"{old.Student.Student.FullName}: {ex.Message}"); }
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Kiểm tra rồi đánh dấu chuyển lớp trên các thực thể đang theo dõi; chưa lưu.</summary>
    private void TransferCore(Enrollment old, Class target, DateOnly effectiveDate)
    {
        if (old.EndDate != null)
            throw ApiException.BadRequest("Ghi danh này đã kết thúc hoặc đã có lịch chuyển lớp. Chỉ chuyển được ghi danh đang mở.");
        if (target.ClassId == old.ClassId) throw ApiException.BadRequest("Lớp chuyển đến trùng lớp hiện tại. Chọn lớp khác.");
        if (target.AcademicYearId != old.Class.AcademicYearId)
            throw ApiException.BadRequest("Chỉ chuyển lớp trong cùng năm học. Sang năm mới dùng Kết chuyển năm học.");
        if (!target.IsActive) throw ApiException.BadRequest("Lớp chuyển đến đã ngừng hoạt động. Chọn lớp khác.");
        EnsureInYear(target.AcademicYear, effectiveDate);

        EffectivePeriod.CloseBefore(old, effectiveDate);
        old.Reason = EnrollmentCloseReasons.ClassTransfer;
        db.Enrollment.Add(new Enrollment { StudentId = old.StudentId, ClassId = target.ClassId, StartDate = effectiveDate, CreatedAt = clock.Now });
    }

    private async Task<List<Enrollment>> LoadManyAsync(IEnumerable<int> enrollmentIds)
    {
        var ids = enrollmentIds.Distinct().ToList();
        var items = await db.Enrollment.Include(e => e.Class).ThenInclude(c => c.AcademicYear).Include(e => e.Student).ThenInclude(s => s.Student)
            .Where(e => ids.Contains(e.EnrollmentId)).ToListAsync();
        if (items.Count != ids.Count) throw ApiException.NotFound("Không tìm thấy ghi danh. Tải lại danh sách rồi chọn lại.");
        return items;
    }

    /// <summary>
    /// Ghi danh nhiều học sinh vào một lớp cùng ngày hiệu lực, trong một lần lưu: một em không hợp lệ thì không ghi danh em nào
    /// và báo rõ tên em đó.
    /// </summary>
    public async Task EnrollManyAsync(int classId, EnrollManyRequest r)
    {
        if (r.StudentIds is not { Count: > 0 }) throw ApiException.BadRequest("Chọn ít nhất một học sinh để ghi danh.");
        var cls = await db.Class.Include(c => c.AcademicYear).FirstOrDefaultAsync(c => c.ClassId == classId)
                  ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        if (!cls.IsActive) throw ApiException.BadRequest("Lớp đã ngừng hoạt động. Kích hoạt lại lớp trước khi ghi danh.");
        EnsureInYear(cls.AcademicYear, r.StartDate);

        var ids = r.StudentIds.Distinct().ToList();
        var students = await db.StudentProfile.Include(s => s.Student).Where(s => ids.Contains(s.StudentId)).ToListAsync();
        if (students.Count != ids.Count) throw ApiException.BadRequest("Có học sinh không tồn tại. Tải lại danh sách rồi chọn lại.");
        foreach (var s in students)
        {
            if (s.Status != Studying)
                throw ApiException.BadRequest($"{s.Student.FullName} không còn ở trạng thái Đang học nên không ghi danh được.");
            await EnsureNoOverlapAsync(s, cls.AcademicYear, r.StartDate);
        }

        db.Enrollment.AddRange(students.Select(s => new Enrollment { StudentId = s.StudentId, ClassId = classId, StartDate = r.StartDate, CreatedAt = clock.Now }));
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// 4.2.32: kết thúc ghi danh đang mở, ngày kết thúc là ngày thao tác (BR-09). Chuyển trường hoặc tốt nghiệp đồng thời đổi trạng thái học sinh,
    /// khóa tài khoản học sinh (BR-05) và kết thúc MỌI ghi danh còn lại của em: ghi danh đang hiệu lực thì đóng, ghi danh chưa bắt đầu
    /// (ví dụ lớp năm sau tạo bởi kết chuyển) thì bỏ hẳn vì chưa từng có hiệu lực.
    /// </summary>
    public async Task CloseAsync(int enrollmentId, CloseEnrollmentRequest r)
    {
        if (!EnrollmentCloseReasons.All.Contains(r.Reason))
            throw ApiException.BadRequest("Chọn lý do kết thúc ghi danh: chuyển lớp, chuyển trường hoặc tốt nghiệp.");
        var e = await FindEnrollmentAsync(enrollmentId);
        if (e.EndDate != null) throw ApiException.BadRequest("Ghi danh này đã kết thúc từ trước.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var lockedAccountId = await CloseCoreAsync(e, r.Reason, clock.Today);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        if (lockedAccountId is int id) await sessions.RevokeAllAsync(id);
    }

    /// <summary>Kết thúc ghi danh của nhiều học sinh cùng lý do trong một transaction; một em sai thì không kết thúc em nào.</summary>
    public async Task CloseManyAsync(CloseManyRequest r)
    {
        if (!EnrollmentCloseReasons.All.Contains(r.Reason))
            throw ApiException.BadRequest("Chọn lý do kết thúc ghi danh: chuyển lớp, chuyển trường hoặc tốt nghiệp.");
        if (r.EnrollmentIds is not { Count: > 0 }) throw ApiException.BadRequest("Chọn ít nhất một học sinh để kết thúc ghi danh.");
        var items = await LoadManyAsync(r.EnrollmentIds);
        var bad = items.FirstOrDefault(e => e.EndDate != null);
        if (bad is not null) throw ApiException.BadRequest($"{bad.Student.Student.FullName}: ghi danh này đã kết thúc từ trước.");

        var today = clock.Today;
        var locked = new List<int>();
        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var e in items)
            if (await CloseCoreAsync(e, r.Reason, today) is int accountId) locked.Add(accountId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        foreach (var id in locked) await sessions.RevokeAllAsync(id);
    }

    /// <summary>Đặt ngày kết thúc cho một ghi danh (và khóa tài khoản nếu em rời trường); chưa lưu. Trả về tài khoản vừa khóa để hủy phiên sau khi lưu.</summary>
    private async Task<int?> CloseCoreAsync(Enrollment e, string reason, DateOnly today)
    {
        var leaving = reason != EnrollmentCloseReasons.ClassTransfer;
        var enrollmentId = e.EnrollmentId;

        DateOnly leftDate;
        if (leaving && e.StartDate > today)
        {
            db.Enrollment.Remove(e);   // chưa bắt đầu thì không có gì để đóng, em đã rời trường
            leftDate = today;
        }
        else
        {
            var endDate = e.StartDate > today ? e.StartDate : today;   // ghi danh chưa hiệu lực thì rút về đúng ngày bắt đầu
            var yearEnd = e.Class.AcademicYear.EndDate;
            e.EndDate = endDate > yearEnd ? yearEnd : endDate;          // đóng sau khi hết năm học thì ghi đúng ngày cuối năm học
            e.Reason = reason;
            leftDate = e.EndDate.Value;
        }

        int? lockedAccountId = null;
        if (leaving)
        {
            await EndOtherEnrollmentsAsync(e, today, reason);
            e.Student.Status = reason == EnrollmentCloseReasons.Graduated ? "Graduated" : "Transferred";
            e.Student.LeftDate = leftDate;
            var account = await db.Account.FirstOrDefaultAsync(a => a.PersonId == e.StudentId);
            if (account is { Status: not AccountStatuses.Locked })
            {
                account.Status = AccountStatuses.Locked;
                lockedAccountId = account.AccountId;
                audit.Add(AuditCategories.Account, "LockAccount", [account.AccountId], new { reason, enrollmentId });
            }
        }
        return lockedAccountId;
    }

    /// <summary>
    /// Kết chuyển lớp 9: cho các học sinh được chọn tốt nghiệp (không có khối 10). Mỗi em: ghi danh đóng vào cuối năm học với lý do Tốt nghiệp,
    /// hồ sơ chuyển sang Đã tốt nghiệp, tài khoản khóa (BR-05), ghi danh năm sau (nếu đã xếp) bị bỏ. Không hoàn tác từ màn kết chuyển.
    /// </summary>
    public async Task<int> GraduateAsync(int classId, GraduateRequest r)
    {
        if (r.StudentIds is not { Count: > 0 }) throw ApiException.BadRequest("Chọn ít nhất một học sinh để cho tốt nghiệp.");
        var cls = await db.Class.Include(c => c.AcademicYear).FirstOrDefaultAsync(c => c.ClassId == classId)
                  ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        if (RolloverRules.NextGrade(cls.GradeLevel) is not null)
            throw ApiException.BadRequest($"Lớp {cls.Name} chưa phải khối cuối cấp nên không cho tốt nghiệp được.");

        var year = cls.AcademicYear;
        var wanted = r.StudentIds.Distinct().ToList();
        var enrollments = await db.Enrollment.Include(e => e.Student).Where(EnrollmentRules.ActiveAt(year.EndDate))
            .Where(e => e.ClassId == classId && e.Student.Status == Studying && wanted.Contains(e.StudentId))
            .ToListAsync();
        if (enrollments.Select(e => e.StudentId).Distinct().Count() != wanted.Count)
            throw ApiException.BadRequest("Có học sinh không còn học ở lớp này. Tải lại danh sách rồi chọn lại.");

        var today = clock.Today;
        var lockedIds = new List<int>();
        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var e in enrollments)
        {
            e.EndDate = year.EndDate;
            e.Reason = EnrollmentCloseReasons.Graduated;
            await EndOtherEnrollmentsAsync(e, today, EnrollmentCloseReasons.Graduated);
            e.Student.Status = "Graduated";
            e.Student.LeftDate = year.EndDate;
            var account = await db.Account.FirstOrDefaultAsync(a => a.PersonId == e.StudentId);
            if (account is { Status: not AccountStatuses.Locked })
            {
                account.Status = AccountStatuses.Locked;
                lockedIds.Add(account.AccountId);
                audit.Add(AuditCategories.Account, "LockAccount", [account.AccountId], new { reason = EnrollmentCloseReasons.Graduated, enrollmentId = e.EnrollmentId });
            }
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        foreach (var id in lockedIds) await sessions.RevokeAllAsync(id);
        return enrollments.Count;
    }

    /// <summary>Học sinh rời trường: đóng ghi danh đang hiệu lực khác của em, bỏ ghi danh chưa bắt đầu. Ghi danh đã kết thúc không đụng tới.</summary>
    private async Task EndOtherEnrollmentsAsync(Enrollment kept, DateOnly today, string reason)
    {
        var others = await db.Enrollment.Include(x => x.Class).ThenInclude(c => c.AcademicYear)
            .Where(x => x.StudentId == kept.StudentId && x.EnrollmentId != kept.EnrollmentId)
            .Where(EnrollmentRules.OpenOrUpcoming(today))
            .ToListAsync();
        foreach (var o in others)
        {
            if (o.StartDate > today) db.Enrollment.Remove(o);
            else
            {
                var yearEnd = o.Class.AcademicYear.EndDate;
                o.EndDate = today > yearEnd ? yearEnd : today;
                o.Reason = reason;
            }
        }
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
