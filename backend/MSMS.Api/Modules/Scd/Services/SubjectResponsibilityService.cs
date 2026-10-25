using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Modules.Scd.Dtos;

namespace MSMS.Api.Modules.Scd.Services;

/// <summary>
/// SCD-04: phụ trách chuyên môn (giáo viên – môn – lớp) không nhập tay và không lưu; tính từ thời khóa biểu mỗi khi cần (BR-14).
/// Với mỗi lớp lấy phiên bản Đã phát hành mới nhất của lớp trong học kỳ hiện hành; số tiết mỗi tuần là số ô của (lớp, môn, giáo viên).
/// </summary>
public sealed class SubjectResponsibilityService(MsmsDbContext db, IClock clock)
{
    /// <summary>
    /// Xem toàn trường. <paramref name="campusId"/> và <paramref name="subjectId"/> chỉ chọn giáo viên cần hiện;
    /// mỗi giáo viên vẫn hiện đủ mọi mục ở mọi phân hiệu và tổng tiết toàn trường.
    /// </summary>
    public async Task<ResponsibilityDto> ListAsync(int? campusId, int? subjectId)
    {
        var semester = await CurrentSemesterAsync();
        if (semester is null) return new ResponsibilityDto(null, []);

        // Phiên bản Đã phát hành mới nhất của từng lớp; lớp không có trong lần Phát hành sau vẫn theo phiên bản trước đó của nó.
        var published = await db.TimetableVersionClass.AsNoTracking()
            .Where(vc => vc.Version.Status == "Published" && vc.Version.SemesterId == semester.SemesterId)
            .Select(vc => new { vc.ClassId, vc.VersionId, vc.Version.PublishedAt })
            .ToListAsync();
        var latest = published.GroupBy(x => x.ClassId)
            .Select(g => g.OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.VersionId).First())
            .ToDictionary(x => x.ClassId, x => x.VersionId);
        if (latest.Count == 0) return new ResponsibilityDto(semester.Name, []);

        var versionIds = latest.Values.Distinct().ToList();
        var cells = await db.TimetableEntry.AsNoTracking()
            .Where(e => versionIds.Contains(e.VersionId))
            .Select(e => new
            {
                e.VersionId, e.ClassId, e.SubjectId, e.TeacherId,
                SubjectAbbr = e.Subject.Abbreviation, SubjectName = e.Subject.Name,
                ClassName = e.Class.Name, e.Class.GradeLevel, CampusId = e.Class.CampusId,
                TeacherName = e.Teacher.Staff.FullName, e.Teacher.TeacherAbbr,
            }).ToListAsync();

        var items = cells.Where(c => latest.TryGetValue(c.ClassId, out var v) && v == c.VersionId).ToList();

        var teachers = items.GroupBy(c => c.TeacherId)
            .Where(g => (campusId is null || g.Any(c => c.CampusId == campusId)) && (subjectId is null || g.Any(c => c.SubjectId == subjectId)))
            .Select(g =>
            {
                var rows = g.GroupBy(c => (c.SubjectId, c.ClassId))
                    .Select(x =>
                    {
                        var first = x.First();
                        return new { first.SubjectId, first.SubjectAbbr, first.SubjectName, first.ClassId, first.ClassName, first.GradeLevel, Periods = x.Count() };
                    })
                    .OrderBy(x => x.SubjectName, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.GradeLevel).ThenBy(x => x.ClassName)
                    .Select(x => new ResponsibilityItemDto(x.SubjectId, x.SubjectAbbr, x.SubjectName, x.ClassId, x.ClassName, x.Periods))
                    .ToList();
                var head = g.First();
                return new ResponsibilityTeacherDto(g.Key, head.TeacherName, head.TeacherAbbr, rows.Sum(r => r.PeriodsPerWeek), rows);
            })
            .OrderBy(t => t.TeacherName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new ResponsibilityDto(semester.Name, teachers);
    }

    /// <summary>Học kỳ của năm học hiện hành chứa hôm nay; ngoài các học kỳ thì lấy học kỳ đã bắt đầu gần nhất, chưa có thì học kỳ đầu.</summary>
    private async Task<Data.Entities.Semester?> CurrentSemesterAsync()
    {
        var today = clock.Today;
        var semesters = await db.Semester.AsNoTracking().Where(s => s.AcademicYear.IsCurrent).OrderBy(s => s.StartDate).ToListAsync();
        return semesters.FirstOrDefault(s => s.StartDate <= today && today <= s.EndDate)
               ?? semesters.LastOrDefault(s => s.StartDate <= today)
               ?? semesters.FirstOrDefault();
    }
}
