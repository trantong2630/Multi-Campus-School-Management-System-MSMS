using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Dtos;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>
/// Màn theo dõi lớp chủ nhiệm. Chủ nhiệm đọc được hồ sơ, đơn nghỉ của lớp mình (mục 2.3) và liên hệ phụ huynh;
/// bài tập chỉ hiện tỷ lệ nộp tổng hợp theo môn, không có bài nộp hay điểm của từng học sinh.
/// Quyền tính theo trách nhiệm chủ nhiệm còn hiệu lực hôm nay (BR-09).
/// </summary>
public sealed class HomeroomOverviewService(MsmsDbContext db, CurrentUser user, IClock clock)
{
    public async Task<HomeroomOverviewDto> GetAsync(int? classId)
    {
        var today = clock.Today;
        var scope = await new AccountScopeResolver(db, clock).ResolveAsync(user.PersonId, user.AccountType);
        if (scope.HomeroomClassIds.Count == 0)
            throw ApiException.Forbidden("Chỉ giáo viên đang chủ nhiệm mới xem được màn hình này.");
        var id = classId ?? scope.HomeroomClassIds[0];
        if (!scope.HomeroomClassIds.Contains(id))
            throw ApiException.Forbidden("Không có quyền xem lớp này. Bạn không phải chủ nhiệm của lớp.");

        var options = await db.Class.AsNoTracking().Where(c => scope.HomeroomClassIds.Contains(c.ClassId))
            .OrderBy(c => c.Name).Select(c => new NamedId(c.ClassId, c.Name)).ToListAsync();
        var cls = await db.Class.AsNoTracking().Where(c => c.ClassId == id)
            .Select(c => new { c.ClassId, c.Name, c.GradeLevel, Campus = c.Campus.Name, Year = c.AcademicYear.Name, c.AcademicYearId })
            .FirstOrDefaultAsync() ?? throw ApiException.NotFound("Không tìm thấy lớp.");

        var students = await db.Enrollment.AsNoTracking()
            .Where(EnrollmentRules.ActiveAsOf(today))
            .Where(e => e.ClassId == id)
            .Select(e => new
            {
                e.StudentId, e.Student.Student.PersonCode, e.Student.Student.FullName,
                e.Student.Student.Gender, e.Student.Student.DateOfBirth,
            }).ToListAsync();
        var studentIds = students.Select(s => s.StudentId).ToList();

        var leaveFrom = today.AddDays(-30);
        var leaves = await db.LeaveRequest.AsNoTracking().Where(l => l.ClassId == id)
            .Select(l => new { l.StudentId, l.Status, l.FromDate }).ToListAsync();

        var absences = await AttendanceSupport.AbsencesByStudentAsync(db, id, leaveFrom.AddDays(1), today);

        var parents = (await db.ParentStudentLink.AsNoTracking().Where(p => studentIds.Contains(p.StudentId))
                .Select(p => new { p.StudentId, p.IsPrimary, p.Parent.FullName, p.Parent.PhoneNumber, HasAccount = p.Parent.Account != null })
                .ToListAsync())
            .GroupBy(p => p.StudentId).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.IsPrimary).ToList());

        var rows = students
            .Select(s =>
            {
                var ps = parents.GetValueOrDefault(s.StudentId) ?? [];
                var main = ps.FirstOrDefault();
                var sl = leaves.Where(l => l.StudentId == s.StudentId).ToList();
                return new HomeroomStudentRow(s.StudentId, s.PersonCode, s.FullName, s.Gender, s.DateOfBirth,
                    sl.Count(l => l.Status is "Approved" or "Pending" && l.FromDate >= leaveFrom), sl.Count(l => l.Status == "Pending"),
                    ps.Count, main?.FullName, main?.PhoneNumber, ps.Any(p => p.HasAccount),
                    absences.GetValueOrDefault(s.StudentId).Excused, absences.GetValueOrDefault(s.StudentId).Unexcused);
            })
            .OrderBy(s => LastName(s.FullName), StringComparer.Create(new System.Globalization.CultureInfo("vi-VN"), true))
            .ThenBy(s => s.FullName, StringComparer.Create(new System.Globalization.CultureInfo("vi-VN"), true))
            .ToList();


        var trendFrom = DashboardSupport.WeekStart(today).AddDays(-49);
        var days = await AttendanceSupport.DaysAsync(db, [id], trendFrom, today);
        var recent = days.Where(d => d.Date > today.AddDays(-28)).ToList();
        var unmarked = (await AttendanceSupport.UnmarkedByClassAsync(db, [id], today.AddDays(-27), today)).Values.Sum();
        var todayMarked = await db.TeachingSession.AsNoTracking()
            .Where(s => s.ClassId == id && s.Status == AttendanceRules.SessionScheduled && s.SessionDate == today)
            .Select(s => s.AttendanceRecord.Any()).ToListAsync();

        var term = await DashboardSupport.CurrentTermAsync(db, today);
        var teachers = term.SemesterId is int semId
            ? (await PublishedTimetable.LatestEntries(db, semId).Where(e => e.ClassId == id)
                    .Select(e => new { Subject = e.Subject.Name, Teacher = e.Teacher.Staff.FullName }).Distinct().ToListAsync())
                .OrderBy(t => t.Subject, StringComparer.CurrentCultureIgnoreCase)
                .Select(t => new HomeroomTeacherRow(t.Subject, t.Teacher)).ToList()
            : [];

        return new HomeroomOverviewDto(cls.ClassId, cls.Name, cls.GradeLevel, cls.Campus, cls.Year, options,
            rows.Count, rows.Count(r => r.Gender == "Male"), rows.Count(r => r.Gender == "Female"),
            leaves.Count(l => l.Status == "Pending"),
            leaves.Count(l => l.Status is "Approved" or "Pending" && l.FromDate >= leaveFrom),
            rows.Count(r => r.ParentCount == 0), rows.Count(r => r.ParentCount > 0 && !r.ParentHasAccount),
            teachers, rows,
            AttendanceSupport.Summary(recent, unmarked, todayMarked.Count, todayMarked.Count(m => m)), AttendanceSupport.Weeks(days, today));
    }

    /// <summary>Tên gọi (từ cuối) để xếp theo kiểu danh sách lớp Việt Nam.</summary>
    private static string LastName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "" : parts[^1];
    }
}
