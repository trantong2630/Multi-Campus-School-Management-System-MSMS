using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Modules.Adm.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>
/// Tình hình tổ chuyên môn cho tổ trưởng: tải dạy, vắng và điểm danh của thầy cô, tiết của tổ bị ảnh hưởng, giao bài rèn luyện và tỷ lệ nộp theo môn.
/// Phạm vi là các tổ người gọi đang làm tổ trưởng tại hôm nay (BR-09); thầy cô tính là thành viên khi thuộc tổ hoặc đang dạy môn của tổ.
/// </summary>
public sealed class DepartmentBoardService(MsmsDbContext db, CurrentUser user, IClock clock, SubjectResponsibilityService responsibility)
{
    public async Task<DepartmentBoardDto> GetAsync()
    {
        var today = clock.Today;
        var now = clock.Now;
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
        var weekStarts = DashboardSupport.TrendWeekStarts(today);
        var (current, _) = DashboardSupport.Periods(today);
        var currentFrom = current.ToDateTime(TimeOnly.MinValue);

        var teaching = await responsibility.ListAsync(null, null);
        var byTeacher = teaching.Teachers
            .Select(t => (t.TeacherId, Items: t.Items.Where(i => subjectIds.Contains(i.SubjectId)).ToList()))
            .Where(t => t.Items.Count > 0)
            .ToDictionary(t => t.TeacherId);
        var teacherIds = byTeacher.Keys.ToList();

        var staff = await db.StaffProfile.AsNoTracking()
            .Where(s => s.IsActive && ((s.DepartmentId != null && departmentIds.Contains(s.DepartmentId.Value)) || teacherIds.Contains(s.StaffId)))
            .Select(s => new { s.StaffId, s.Staff.FullName, s.DepartmentId }).ToListAsync();
        var memberIds = staff.Select(s => s.StaffId).ToList();

        var absentToday = await db.TeacherAbsence.AsNoTracking()
            .Where(a => a.FromDate <= today && a.ToDate >= today && memberIds.Contains(a.TeacherId))
            .Select(a => a.TeacherId).Distinct().ToListAsync();
        var absentDays = await OperationsSupport.AbsentDaysAsync(db, memberIds, current, today);
        var unmarked = await OperationsSupport.UnmarkedByTeacherAsync(db, classIds, current, today, subjectIds);
        var affected = await OperationsSupport.AffectedAsync(db, classIds, DashboardSupport.ReadFrom(today), today, subjectIds);
        // Đọc từ cùng mốc với màn toàn trường để đủ cả 8 tuần xu hướng lẫn 4 tuần liền trước của tỷ lệ nộp.
        var assignments = await OperationsSupport.AssignmentsAsync(db, classIds, DashboardSupport.ReadFrom(today).ToDateTime(TimeOnly.MinValue), subjectIds);

        var members = staff.Select(s =>
            {
                var items = byTeacher.TryGetValue(s.StaffId, out var t) ? t.Items : [];
                var mine = assignments.Where(a => a.AssignedBy == s.StaffId).ToList();
                return new DepartmentMemberRow(s.StaffId, s.FullName, s.DepartmentId != null && departmentIds.Contains(s.DepartmentId.Value),
                    items.Sum(i => i.PeriodsPerWeek), items.Select(i => i.ClassName).Distinct().Order().ToList(),
                    absentToday.Contains(s.StaffId), absentDays.GetValueOrDefault(s.StaffId), unmarked.GetValueOrDefault(s.StaffId),
                    mine.Count(a => a.OpenAt >= currentFrom && a.OpenAt <= now), mine.Count(a => a.OpenAt <= now && a.DueAt > now),
                    OperationsSupport.Submission(mine, currentFrom, now).Rate);
            })
            .Where(m => m.InDepartment || m.PeriodsPerWeek > 0)
            .OrderByDescending(m => m.PeriodsPerWeek).ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        var subjectRates = subjects
            .Select(s => new RateRow(s.SubjectId, s.Name, OperationsSupport.Submission(assignments.Where(a => a.SubjectId == s.SubjectId), currentFrom, now).Rate))
            .Where(r => r.Rate is not null).ToList();

        var loads = members.Where(m => m.PeriodsPerWeek > 0).Select(m => m.PeriodsPerWeek).ToList();
        return new DepartmentBoardDto(departments, DashboardSupport.WeekNo(term, today), weekStarts,
            loads.Count == 0 ? null : Math.Round(loads.Average(), 1),
            OperationsSupport.AffectedKpi(affected, today, weekStarts), unmarked.Values.Sum(),
            OperationsSupport.SubmissionKpi(assignments, today, now, weekStarts),
            members.Where(m => m.AbsentToday).Select(m => m.Name).ToList(),
            OperationsSupport.AbsenceWeeks(affected, weekStarts), subjectRates,
            OperationsSupport.Summary(assignments, today, now), members);
    }
}
