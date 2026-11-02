using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Dtos;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>
/// Dashboard giáo viên: chỉ dữ liệu của chính mình — tiết mình dạy, lớp và môn mình phụ trách (BR-14), bài mình giao.
/// Chủ nhiệm và tổ trưởng chỉ có thẻ đường tắt; số liệu của lớp, của tổ nằm ở màn trách nhiệm riêng.
/// </summary>
public sealed class TeacherDashboardService(MsmsDbContext db, CurrentUser user, IClock clock)
{
    /// <summary>Bài sắp hết hạn: còn mở và hạn nộp trong 48 giờ tới.</summary>
    private static readonly TimeSpan DueSoonWindow = TimeSpan.FromHours(48);

    public async Task<TeacherDashboardDto> GetAsync()
    {
        var me = user.PersonId;
        var today = clock.Today;
        var now = clock.Now;
        var term = await DashboardSupport.CurrentTermAsync(db, today);
        var scope = await new AccountScopeResolver(db, clock).ResolveAsync(me, AccountTypes.Teacher);
        var weekStart = DashboardSupport.WeekStart(today);
        var weekEnd = weekStart.AddDays(6);

        var fullName = await db.Person.AsNoTracking().Where(p => p.PersonId == me).Select(p => p.FullName).FirstAsync();

        var mySessions = db.TeachingSession.AsNoTracking().Where(s => s.TeacherId == me && s.Status == "Scheduled");
        var todaySessions = await mySessions.Where(s => s.SessionDate == today)
            .OrderBy(s => s.PeriodSlot.StartTime)
            .Select(s => new TeacherSessionItem(s.SessionId, s.PeriodSlot.PeriodNo, s.PeriodSlot.SessionOfDay,
                s.PeriodSlot.StartTime, s.PeriodSlot.EndTime, s.ClassId, s.Class.Name, s.Subject.Name))
            .ToListAsync();
        var weekCount = await mySessions.CountAsync(s => s.SessionDate >= weekStart && s.SessionDate <= weekEnd);

        var classIds = scope.SubjectResponsibilities.Select(r => r.ClassId).Distinct().ToList();
        var subjectIds = scope.SubjectResponsibilities.Select(r => r.SubjectId).Distinct().ToList();
        var classInfo = await db.Class.AsNoTracking().Where(c => classIds.Contains(c.ClassId))
            .Select(c => new
            {
                c.ClassId, c.Name, c.GradeLevel,
                Students = c.Enrollment.AsQueryable().Count(EffectivePeriod.EffectiveAt<Enrollment>(today)),
            }).ToListAsync();
        var subjectNames = await db.Subject.AsNoTracking().Where(s => subjectIds.Contains(s.SubjectId))
            .ToDictionaryAsync(s => s.SubjectId, s => s.Name);

        var facts = await DashboardSupport.FactsAsync(db.Assignment
            .Where(a => a.AssignedByPersonId == me && a.Class.AcademicYearId == term.YearId));

        var classes = classInfo
            .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(c =>
            {
                var cf = facts.Where(f => f.ClassId == c.ClassId).ToList();
                var subjects = scope.SubjectResponsibilities.Where(r => r.ClassId == c.ClassId)
                    .Select(r => subjectNames.GetValueOrDefault(r.SubjectId) ?? "").Where(n => n != "").Distinct().ToList();
                return new TeacherClassRow(c.ClassId, c.Name, c.GradeLevel, subjects, c.Students, cf.Count, DashboardSupport.Rate(cf, now));
            }).ToList();

        var open = facts.Where(f => f.OpenAt <= now && !AssignmentRules.IsClosed(f.DueAt, now)).ToList();
        var recentlyClosed = facts.Where(f => AssignmentRules.IsClosed(f.DueAt, now) && f.DueAt >= now.AddDays(-7));

        var factIds = facts.OrderByDescending(f => f.DueAt).Take(6).Select(f => f.AssignmentId).ToList();
        var recent = (await db.Assignment.AsNoTracking().Where(a => factIds.Contains(a.AssignmentId))
                .Select(a => new { a.AssignmentId, a.Title, ClassName = a.Class.Name }).ToListAsync())
            .Join(facts, a => a.AssignmentId, f => f.AssignmentId, (a, f) => new TeacherAssignmentItem(
                a.AssignmentId, a.Title, a.ClassName, f.SubjectName, f.DueAt, AssignmentRules.IsClosed(f.DueAt, now), f.Recipients, f.Submitted))
            .OrderByDescending(a => a.DueAt).ToList();

        var homeroom = await db.Class.AsNoTracking().Where(c => scope.HomeroomClassIds.Contains(c.ClassId))
            .OrderBy(c => c.Name).Select(c => new NamedId(c.ClassId, c.Name)).ToListAsync();
        var departments = await db.SubjectDepartment.AsNoTracking().Where(d => scope.HeadDepartmentIds.Contains(d.DepartmentId))
            .OrderBy(d => d.Name).Select(d => new NamedId(d.DepartmentId, d.Name)).ToListAsync();

        return new TeacherDashboardDto(fullName, term.YearName, term.SemesterName,
            todaySessions.Count, weekCount, classes.Count, classes.Sum(c => c.StudentCount), subjectIds.Count,
            open.Count, open.Count(f => f.DueAt <= now + DueSoonWindow), recentlyClosed.Count(f => f.Submitted < f.Recipients),
            DashboardSupport.Rate(facts, now), todaySessions, classes, recent, homeroom, departments);
    }
}
