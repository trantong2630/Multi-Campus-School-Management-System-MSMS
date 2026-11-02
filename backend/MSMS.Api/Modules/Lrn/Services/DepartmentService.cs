using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Modules.Lrn.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>
/// LRN-05: tiến độ giao bài của tổ. Tổ trưởng xem bài của mọi lớp thuộc các môn trong tổ mình, ở mọi phân hiệu, và số bài mỗi giáo viên đã giao.
/// Chỉ đọc: chi tiết một bài đi qua cùng các API xem kết quả, nơi <see cref="LrnAccess"/> chỉ cho xem, không có đường ghi.
/// </summary>
public sealed class DepartmentService(MsmsDbContext db, IClock clock, LrnAccess access, SubjectResponsibilityService responsibility)
{
    public async Task<DepartmentOverviewDto> OverviewAsync(int? subjectId)
    {
        var departments = await access.HeadDepartmentIdsAsync();
        if (departments.Count == 0) return new DepartmentOverviewDto(false, [], [], [], null, []);

        var subjects = await db.Subject.AsNoTracking().Where(s => departments.Contains(s.DepartmentId))
            .OrderBy(s => s.Name).Select(s => new SubjectOptionDto(s.SubjectId, s.Name)).ToListAsync();
        var subjectIds = subjects.Select(s => s.SubjectId).ToList();

        var q = db.Assignment.AsNoTracking().Where(a => subjectIds.Contains(a.SubjectId));
        if (subjectId is not null) q = q.Where(a => a.SubjectId == subjectId);

        var rows = await q.OrderByDescending(a => a.DueAt).ThenByDescending(a => a.AssignmentId)
            .Select(a => new
            {
                a.AssignmentId, a.Title, a.Class.CampusId, CampusName = a.Class.Campus.Name, a.ClassId, ClassName = a.Class.Name,
                a.SubjectId, SubjectName = a.Subject.Name, a.AssignedByPersonId, AssignedByName = a.AssignedByPerson.FullName,
                a.OpenAt, a.DueAt, RecipientCount = a.AssignmentRecipient.Count, SubmittedCount = a.Submission.Count(s => s.SubmittedAt != null),
            }).ToListAsync();

        var now = clock.Now;
        var items = rows.Select(r => new DepartmentItemDto(r.AssignmentId, r.Title, r.CampusId, r.CampusName, r.ClassId, r.ClassName, r.SubjectId, r.SubjectName,
            r.AssignedByName, r.OpenAt, r.DueAt, AssignmentRules.IsClosed(r.DueAt, now), r.RecipientCount, r.SubmittedCount)).ToList();

        var personIds = rows.Select(r => r.AssignedByPersonId).Distinct().ToList();
        var campusOf = await db.StaffProfile.AsNoTracking().Where(s => personIds.Contains(s.StaffId))
            .Select(s => new { s.StaffId, CampusId = s.PrimaryCampusId, CampusName = s.PrimaryCampus.Name }).ToDictionaryAsync(s => s.StaffId);

        var teachers = rows.GroupBy(r => (r.AssignedByPersonId, r.AssignedByName))
            .Select(g => new TeacherProgressDto(g.Key.AssignedByPersonId, g.Key.AssignedByName, campusOf.GetValueOrDefault(g.Key.AssignedByPersonId)?.CampusId ?? 0, campusOf.GetValueOrDefault(g.Key.AssignedByPersonId)?.CampusName ?? "", g.Count(), g.Count(r => !AssignmentRules.IsClosed(r.DueAt, now))))
            .OrderByDescending(t => t.AssignmentCount).ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var (semesterName, members) = await MembersAsync(departments, subjectIds);
        return new DepartmentOverviewDto(true, subjects, teachers, items, semesterName, members);
    }

    /// <summary>Thầy cô trong tổ và phân công dạy các môn của tổ ở học kỳ hiện hành (toàn trường, không lọc theo môn đang chọn).</summary>
    private async Task<(string? SemesterName, List<DepartmentMemberDto> Members)> MembersAsync(List<int> departments, List<int> subjectIds)
    {
        var teaching = await responsibility.ListAsync(null, null);
        var byTeacher = teaching.Teachers
            .Select(t => (t.TeacherId, t.TeacherName, Items: t.Items.Where(i => subjectIds.Contains(i.SubjectId)).ToList()))
            .Where(t => t.Items.Count > 0)
            .ToDictionary(t => t.TeacherId);
        var teacherIds = byTeacher.Keys.ToList();

        var staff = await db.StaffProfile.AsNoTracking()
            .Where(s => s.IsActive && ((s.DepartmentId != null && departments.Contains(s.DepartmentId.Value)) || teacherIds.Contains(s.StaffId)))
            .Select(s => new { s.StaffId, s.Staff.FullName, CampusName = s.PrimaryCampus.Name, s.DepartmentId })
            .ToListAsync();

        var members = staff.Select(s =>
        {
            var items = byTeacher.TryGetValue(s.StaffId, out var t) ? t.Items : [];
            var subjectsTaught = items.GroupBy(i => (i.SubjectId, i.SubjectName))
                .Select(g => new DepartmentTeachingDto(g.Key.SubjectId, g.Key.SubjectName, g.Select(i => i.ClassName).Distinct().ToList()))
                .ToList();
            return new DepartmentMemberDto(s.StaffId, s.FullName, s.CampusName,
                s.DepartmentId != null && departments.Contains(s.DepartmentId.Value), items.Sum(i => i.PeriodsPerWeek), subjectsTaught);
        })
            .OrderByDescending(m => m.InDepartment).ThenBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return (teaching.SemesterName, members);
    }
}
