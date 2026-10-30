using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Modules.Lrn.Dtos;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>
/// LRN-05: tiến độ giao bài của tổ. Tổ trưởng xem bài của mọi lớp thuộc các môn trong tổ mình, ở mọi phân hiệu, và số bài mỗi giáo viên đã giao.
/// Chỉ đọc: chi tiết một bài đi qua cùng các API xem kết quả, nơi <see cref="LrnAccess"/> chỉ cho xem, không có đường ghi.
/// </summary>
public sealed class DepartmentService(MsmsDbContext db, IClock clock, LrnAccess access)
{
    public async Task<DepartmentOverviewDto> OverviewAsync(int? subjectId)
    {
        var departments = await access.HeadDepartmentIdsAsync();
        if (departments.Count == 0) return new DepartmentOverviewDto(false, [], [], []);

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

        var teachers = rows.GroupBy(r => (r.AssignedByPersonId, r.AssignedByName))
            .Select(g => new TeacherProgressDto(g.Key.AssignedByPersonId, g.Key.AssignedByName, g.Count(), g.Count(r => !AssignmentRules.IsClosed(r.DueAt, now))))
            .OrderByDescending(t => t.AssignmentCount).ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return new DepartmentOverviewDto(true, subjects, teachers, items);
    }
}
