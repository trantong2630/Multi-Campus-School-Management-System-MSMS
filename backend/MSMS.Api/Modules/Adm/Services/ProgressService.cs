using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>
/// Tiến độ bài tập rèn luyện cho cấp quản lý. Hiệu trưởng xem toàn trường (lọc được phân hiệu), Hiệu phó chỉ phân hiệu đang phụ trách,
/// giáo viên chủ nhiệm chỉ lớp mình. Tổ trưởng dùng màn của tổ (LRN-05). Chỉ trả số liệu tổng hợp theo bài; chi tiết từng học sinh không đi qua đây.
/// Bài và lớp lấy theo năm học hiện hành.
/// </summary>
public sealed class ProgressService(MsmsDbContext db, CurrentUser user, IClock clock, SubjectResponsibilityService responsibility)
{
    public async Task<ProgressDto> GetAsync(int? campusId)
    {
        var today = clock.Today;
        var now = clock.Now;
        var term = await DashboardSupport.CurrentTermAsync(db, today);
        var scope = await new AccountScopeResolver(db, clock).ResolveAsync(user.PersonId, user.AccountType);

        var classes = db.Class.AsNoTracking().Where(c => c.IsActive && c.AcademicYearId == term.YearId);
        IQueryable<Campus> campusQuery = db.Campus.AsNoTracking().Where(c => c.IsActive);
        string level;
        switch (user.AccountType)
        {
            case AccountTypes.Principal:
                level = "School";
                if (campusId is not null) classes = classes.Where(c => c.CampusId == campusId);
                break;
            case AccountTypes.VicePrincipal:
                level = "Campus";
                var led = scope.LedCampusIds;
                if (led.Count == 0) throw ApiException.Forbidden("Chưa được gán phụ trách phân hiệu nào.");
                if (campusId is not null && !led.Contains(campusId.Value)) throw ApiException.Forbidden("Không có quyền xem dữ liệu của phân hiệu này.");
                var ids = campusId is null ? led.ToList() : [campusId.Value];
                classes = classes.Where(c => ids.Contains(c.CampusId));
                campusQuery = campusQuery.Where(c => led.Contains(c.CampusId));
                break;
            case AccountTypes.Teacher:
                level = "Class";
                var homeroom = scope.HomeroomClassIds;
                if (homeroom.Count == 0) throw ApiException.Forbidden("Chỉ giáo viên đang chủ nhiệm mới xem được màn hình này.");
                classes = classes.Where(c => homeroom.Contains(c.ClassId));
                campusQuery = campusQuery.Where(_ => false);
                break;
            default:
                throw ApiException.Forbidden("Không có quyền xem tiến độ bài tập.");
        }

        var campuses = await campusQuery.OrderBy(c => c.Name).Select(c => new NamedId(c.CampusId, c.Name)).ToListAsync();
        var classRows = await classes.OrderBy(c => c.GradeLevel).ThenBy(c => c.Name)
            .Select(c => new ProgressClassRow(c.ClassId, c.Name, c.GradeLevel, c.CampusId)).ToListAsync();
        var classIds = classRows.Select(c => c.ClassId).ToList();

        var rows = await db.Assignment.AsNoTracking().Where(a => classIds.Contains(a.ClassId))
            .OrderByDescending(a => a.DueAt)
            .Select(a => new
            {
                a.AssignmentId, a.Title, a.Class.CampusId, CampusName = a.Class.Campus.Name,
                a.Subject.DepartmentId, DepartmentName = a.Subject.Department.Name,
                a.ClassId, ClassName = a.Class.Name, a.Class.GradeLevel, a.SubjectId, SubjectName = a.Subject.Name,
                TeacherId = a.AssignedByPersonId, TeacherName = a.AssignedByPerson.FullName,
                a.OpenAt, a.DueAt, Recipients = a.AssignmentRecipient.Count, Submitted = a.Submission.Count(s => s.SubmittedAt != null),
            }).ToListAsync();
        var items = rows.Select(r => new ProgressItemRow(r.AssignmentId, r.Title, r.CampusId, r.CampusName, r.DepartmentId, r.DepartmentName,
            r.ClassId, r.ClassName, r.GradeLevel, r.SubjectId, r.SubjectName, r.TeacherId, r.TeacherName,
            r.OpenAt, r.DueAt, AssignmentRules.IsClosed(r.DueAt, now), r.Recipients, r.Submitted)).ToList();

        var teaching = await responsibility.ListAsync(null, null);
        var duties = teaching.Teachers
            .Select(t => (t.TeacherId, t.TeacherName, Count: t.Items.Count(i => classIds.Contains(i.ClassId))))
            .Where(t => t.Count > 0).ToList();
        var teacherIds = duties.Select(t => t.TeacherId).ToList();
        var staff = await db.StaffProfile.AsNoTracking().Where(s => teacherIds.Contains(s.StaffId))
            .Select(s => new { s.StaffId, s.PrimaryCampusId, s.DepartmentId, DepartmentName = s.Department != null ? s.Department.Name : null })
            .ToDictionaryAsync(s => s.StaffId);
        var teachers = duties.Select(t =>
        {
            var s = staff.GetValueOrDefault(t.TeacherId);
            return new ProgressTeacherRow(t.TeacherId, t.TeacherName, s?.PrimaryCampusId ?? 0, s?.DepartmentId, s?.DepartmentName, t.Count);
        }).OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        return new ProgressDto(level, term.YearName, term.SemesterName, now, campuses, classRows, teachers, items);
    }
}
