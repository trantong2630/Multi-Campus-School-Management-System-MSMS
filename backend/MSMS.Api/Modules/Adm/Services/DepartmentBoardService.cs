using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Modules.Adm.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>
/// Tình hình tổ chuyên môn cho tổ trưởng: ai đang dạy bao nhiêu tiết, ai vắng hôm nay, tiết nào của tổ chưa điểm danh và chuyên cần của các tiết thuộc môn của tổ.
/// Phạm vi là các tổ người gọi đang làm tổ trưởng tại hôm nay (BR-09). Không có số liệu bài tập: đã có màn Tiến độ bài tập rèn luyện (LRN-05).
/// </summary>
public sealed class DepartmentBoardService(MsmsDbContext db, CurrentUser user, IClock clock, SubjectResponsibilityService responsibility)
{
    private const int UnmarkedDays = 28;

    public async Task<DepartmentBoardDto> GetAsync()
    {
        var today = clock.Today;
        var scope = await new AccountScopeResolver(db, clock).ResolveAsync(user.PersonId, user.AccountType);
        var departmentIds = scope.HeadDepartmentIds.ToList();
        if (departmentIds.Count == 0) throw ApiException.Forbidden("Chỉ tổ trưởng đang giữ trách nhiệm mới xem được màn hình này.");

        var departments = await db.SubjectDepartment.AsNoTracking().Where(d => departmentIds.Contains(d.DepartmentId))
            .OrderBy(d => d.Name).Select(d => new NamedId(d.DepartmentId, d.Name)).ToListAsync();
        var subjects = await db.Subject.AsNoTracking().Where(s => departmentIds.Contains(s.DepartmentId))
            .OrderBy(s => s.Name).Select(s => new { s.SubjectId, s.Name }).ToListAsync();
        var subjectIds = subjects.Select(s => s.SubjectId).ToList();
        var term = await DashboardSupport.CurrentTermAsync(db, today);
        var classIds = await db.Class.AsNoTracking().Where(c => c.IsActive && c.AcademicYearId == term.YearId).Select(c => c.ClassId).ToListAsync();

        var teaching = await responsibility.ListAsync(null, null);
        var byTeacher = teaching.Teachers
            .Select(t => (t.TeacherId, Items: t.Items.Where(i => subjectIds.Contains(i.SubjectId)).ToList()))
            .Where(t => t.Items.Count > 0)
            .ToDictionary(t => t.TeacherId);
        var teacherIds = byTeacher.Keys.ToList();

        var staff = await db.StaffProfile.AsNoTracking()
            .Where(s => s.IsActive && ((s.DepartmentId != null && departmentIds.Contains(s.DepartmentId.Value)) || teacherIds.Contains(s.StaffId)))
            .Select(s => new { s.StaffId, s.Staff.FullName, CampusName = s.PrimaryCampus.Name, s.DepartmentId }).ToListAsync();
        var memberIds = staff.Select(s => s.StaffId).ToList();

        var absentToday = await db.TeacherAbsence.AsNoTracking()
            .Where(a => a.FromDate <= today && a.ToDate >= today && memberIds.Contains(a.TeacherId))
            .Select(a => a.TeacherId).Distinct().ToListAsync();
        var unmarked = await db.TeachingSession.AsNoTracking()
            .Where(s => s.TeacherId != null && memberIds.Contains(s.TeacherId.Value) && subjectIds.Contains(s.SubjectId)
                && s.Status == AttendanceRules.SessionScheduled && s.SessionDate > today.AddDays(-UnmarkedDays) && s.SessionDate < today
                && !s.AttendanceRecord.Any())
            .GroupBy(s => s.TeacherId!.Value).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);

        var todayRows = await db.TeachingSession.AsNoTracking()
            .Where(s => subjectIds.Contains(s.SubjectId) && classIds.Contains(s.ClassId) && s.Status == AttendanceRules.SessionScheduled && s.SessionDate == today)
            .Select(s => s.AttendanceRecord.Any()).ToListAsync();
        var pending = await db.TeachingSession.AsNoTracking()
            .CountAsync(s => subjectIds.Contains(s.SubjectId) && classIds.Contains(s.ClassId) && s.Status == "Pending" && s.SessionDate >= today);

        var days = await AttendanceSupport.DaysAsync(db, classIds, DashboardSupport.WeekStart(today).AddDays(-49), today, subjectIds);
        var recent = days.Where(d => d.Date > today.AddDays(-UnmarkedDays)).ToList();

        var members = staff.Select(s =>
            {
                var items = byTeacher.TryGetValue(s.StaffId, out var t) ? t.Items : [];
                var rows = items.GroupBy(i => i.SubjectName)
                    .Select(g => new DepartmentTeachingRow(g.Key, g.Select(i => i.ClassName).Distinct().ToList())).ToList();
                return new DepartmentMemberRow(s.StaffId, s.FullName, s.CampusName, s.DepartmentId != null && departmentIds.Contains(s.DepartmentId.Value),
                    items.Sum(i => i.PeriodsPerWeek), absentToday.Contains(s.StaffId), unmarked.GetValueOrDefault(s.StaffId), rows);
            })
            .OrderByDescending(m => m.InDepartment).ThenByDescending(m => m.PeriodsPerWeek).ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        return new DepartmentBoardDto(departments, subjects.Select(s => s.Name).ToList(), teaching.SemesterName,
            todayRows.Count, pending, absentToday.Count,
            AttendanceSupport.Summary(recent, unmarked.Values.Sum(), todayRows.Count, todayRows.Count(m => m)), AttendanceSupport.Weeks(days, today), members);
    }
}
