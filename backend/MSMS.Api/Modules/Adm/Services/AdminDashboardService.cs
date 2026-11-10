using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Dtos;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>
/// Thống kê hệ thống của School Admin: quy mô, tài khoản, thiếu sót dữ liệu cần bổ sung, hoạt động hệ thống và tiến độ thời khóa biểu.
/// Quy mô và cấu trúc trường hiển thị trên các màn Cơ cấu nhà trường (StructureStatsService), không lặp lại ở đây.
/// Chỉ những việc School Admin tự sửa được (danh mục, hồ sơ, tài khoản, trách nhiệm); không có số liệu giảng dạy đứng tên người khác (BR-02).
/// </summary>
public sealed class AdminDashboardService(MsmsDbContext db, IClock clock)
{
    public static class GapKeys
    {
        public const string ClassesWithoutHomeroom = "ClassesWithoutHomeroom";
        public const string StudentsWithoutClass = "StudentsWithoutClass";
        public const string StudentsWithoutParent = "StudentsWithoutParent";
        public const string StaffWithoutAccount = "StaffWithoutAccount";
        public const string TeachersWithoutDepartment = "TeachersWithoutDepartment";
        public const string DepartmentsWithoutHead = "DepartmentsWithoutHead";
        public const string CampusesWithoutLeader = "CampusesWithoutLeader";
    }

    public async Task<AdminDashboardDto> GetAsync()
    {
        var today = clock.Today;
        var term = await DashboardSupport.CurrentTermAsync(db, today);

        var classStats = await db.Class.AsNoTracking()
            .Where(c => c.IsActive && c.AcademicYearId == term.YearId && c.Campus.IsActive)
            .Select(c => new
            {
                c.ClassId, c.CampusId,
                HasHomeroom = c.HomeroomAssignment.AsQueryable().Any(EffectivePeriod.CurrentResponsibility<HomeroomAssignment>(today)),
            }).ToListAsync();

        var campuses = await db.Campus.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name)
            .Select(c => new
            {
                c.CampusId, c.Name,
                HasLeader = c.CampusLeadership.AsQueryable().Any(EffectivePeriod.CurrentResponsibility<CampusLeadership>(today)),
            }).ToListAsync();

        var departments = await db.SubjectDepartment.AsNoTracking().Where(d => d.IsActive)
            .Select(d => d.DepartmentHead.AsQueryable().Any(EffectivePeriod.CurrentResponsibility<DepartmentHead>(today))).ToListAsync();

        var accounts = (await db.Account.AsNoTracking()
                .Select(a => new { a.AccountType, a.Status, NeverLoggedIn = a.LastLoginAt == null }).ToListAsync())
            .GroupBy(a => a.AccountType)
            .Select(g => new AccountTypeRow(g.Key, g.Count(),
                g.Count(a => a.Status == AccountStatuses.Active),
                g.Count(a => a.Status == AccountStatuses.PendingPasswordChange),
                g.Count(a => a.Status == AccountStatuses.Locked),
                g.Count(a => a.NeverLoggedIn)))
            .OrderBy(r => Array.IndexOf(AccountOrder, r.AccountType)).ToList();

        var studying = db.StudentProfile.AsNoTracking().Where(s => s.Status == "Studying");
        var gaps = new List<DataGap>
        {
            new(GapKeys.ClassesWithoutHomeroom, classStats.Count(c => !c.HasHomeroom)),
            new(GapKeys.StudentsWithoutClass, await studying.CountAsync(s => !s.Enrollment.AsQueryable().Any(EnrollmentRules.ActiveAsOf(today)))),
            new(GapKeys.StudentsWithoutParent, await studying.CountAsync(s => !s.ParentStudentLink.Any())),
            new(GapKeys.StaffWithoutAccount, await db.StaffProfile.AsNoTracking().CountAsync(s => s.IsActive && s.Staff.Account == null)),
            new(GapKeys.TeachersWithoutDepartment, await db.StaffProfile.AsNoTracking()
                .CountAsync(s => s.IsActive && s.DepartmentId == null && s.Staff.Account != null && s.Staff.Account.AccountType == AccountTypes.Teacher)),
            new(GapKeys.DepartmentsWithoutHead, departments.Count(h => !h)),
            new(GapKeys.CampusesWithoutLeader, campuses.Count(c => !c.HasLeader)),
        };

        var activityFrom = today.AddDays(-13);
        var fromTime = activityFrom.ToDateTime(TimeOnly.MinValue);
        var audit = await db.AuditLog.AsNoTracking().Where(a => a.CreatedAt >= fromTime)
            .Select(a => new { a.CreatedAt, a.Category }).ToListAsync();
        var byDay = audit.GroupBy(a => DateOnly.FromDateTime(a.CreatedAt)).ToDictionary(g => g.Key, g => g.Count());
        var activity = Enumerable.Range(0, 14).Select(i => activityFrom.AddDays(i))
            .Select(d => new DayCount(d, byDay.GetValueOrDefault(d))).ToList();
        var byCategory = audit.GroupBy(a => a.Category).Select(g => new CategoryCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Count).ToList();

        var published = term.SemesterId is int semId
            ? await PublishedTimetable.LatestEntries(db, semId).Select(e => e.ClassId).Distinct().ToListAsync()
            : [];
        var timetables = campuses.Select(c =>
        {
            var ids = classStats.Where(x => x.CampusId == c.CampusId).Select(x => x.ClassId).ToList();
            return new CampusTimetable(c.CampusId, c.Name, ids.Count, ids.Count(published.Contains));
        }).ToList();

        var studentCount = await studying.CountAsync();
        var staffCount = await db.StaffProfile.AsNoTracking().CountAsync(s => s.IsActive);
        return new AdminDashboardDto(term.YearName, term.SemesterName, term.SemesterStart, term.SemesterEnd,
            campuses.Count, classStats.Count, studentCount, staffCount,
            accounts, gaps, activity, byCategory, timetables);
    }

    private static readonly string[] AccountOrder =
        [AccountTypes.SchoolAdmin, AccountTypes.Principal, AccountTypes.VicePrincipal, AccountTypes.Teacher, AccountTypes.Student, AccountTypes.Parent];
}
