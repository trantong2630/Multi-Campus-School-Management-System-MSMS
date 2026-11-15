using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Dtos;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>
/// ADM-01 Báo cáo toàn trường (Hiệu trưởng): so sánh phân hiệu và lớp trong một khoảng ngày, kèm cảnh báo theo ngưỡng cố định.
/// Chỉ cảnh báo về kết quả vận hành (chuyên cần, điểm danh, nộp bài); thiếu sót thiết lập như lớp chưa có chủ nhiệm nằm ở Bảng điều khiển.
/// Chỉ đọc và tổng hợp từ điểm danh, lịch dạy và bài tập; không lưu số liệu suy ra, không có bảng cấu hình ngưỡng.
/// Lớp lấy theo năm học hiện hành. Tiết chưa điểm danh chỉ tính tiết đã qua ngày; bài tập chỉ tính bài đã hết hạn nộp trong khoảng ngày.
/// </summary>
public sealed class SchoolReportService(MsmsDbContext db, IClock clock)
{
    public const int DefaultDays = 28;
    public const int MaxDays = 186;

    /// <summary>Ngưỡng cảnh báo đặt cứng. Đổi ngưỡng là đổi mã nguồn và tài liệu (không lưu trong CSDL).</summary>
    public static readonly ReportThresholds Thresholds = new(LowAttendanceBelow: 90, UnmarkedSessionsFrom: 5, LowSubmissionBelow: 50);

    public static class AlertKinds
    {
        public const string LowAttendance = "LowAttendance";
        public const string ManyUnmarked = "ManyUnmarked";
        public const string LowSubmission = "LowSubmission";
    }

    private static readonly string[] AlertOrder = [AlertKinds.LowAttendance, AlertKinds.ManyUnmarked, AlertKinds.LowSubmission];

    public async Task<SchoolReportDto> GetAsync(DateOnly? from, DateOnly? to, int? campusId, int? gradeLevel)
    {
        var today = clock.Today;
        var end = to ?? today;
        var start = from ?? end.AddDays(-(DefaultDays - 1));
        if (end < start) throw ApiException.BadRequest("Ngày kết thúc phải sau ngày bắt đầu.");
        if (end.DayNumber - start.DayNumber >= MaxDays) throw ApiException.BadRequest($"Báo cáo tối đa {MaxDays} ngày mỗi lần. Chọn khoảng ngắn hơn.");

        var term = await DashboardSupport.CurrentTermAsync(db, today);
        var activeCampuses = db.Campus.AsNoTracking().Where(c => c.IsActive);
        if (campusId is not null)
        {
            if (!await activeCampuses.AnyAsync(c => c.CampusId == campusId)) throw ApiException.NotFound("Không tìm thấy phân hiệu đang hoạt động này.");
            activeCampuses = activeCampuses.Where(c => c.CampusId == campusId);
        }
        var campuses = await activeCampuses.OrderBy(c => c.Name).Select(c => new { c.CampusId, c.Name }).ToListAsync();
        var campusIds = campuses.Select(c => c.CampusId).ToList();

        var inScope = db.Class.AsNoTracking().Where(c => c.IsActive && c.AcademicYearId == term.YearId && campusIds.Contains(c.CampusId));
        var gradeOptions = await inScope.Select(c => (int)c.GradeLevel).Distinct().OrderBy(g => g).ToListAsync();
        var classQuery = gradeLevel is null ? inScope : inScope.Where(c => c.GradeLevel == gradeLevel);

        var classes = await classQuery
            .Select(c => new
            {
                c.ClassId, ClassName = c.Name, c.CampusId, CampusName = c.Campus.Name, c.GradeLevel,
                Students = c.Enrollment.AsQueryable().Count(EffectivePeriod.EffectiveAt<Enrollment>(today)),
            })
            .ToListAsync();
        var classIds = classes.Select(c => c.ClassId).ToList();
        var classCampus = classes.ToDictionary(c => c.ClassId, c => c.CampusId);

        var days = await AttendanceSupport.DaysAsync(db, classIds, start, end);
        var unmarkedUntil = end.AddDays(1) < today ? end.AddDays(1) : today;
        var unmarked = unmarkedUntil > start
            ? await AttendanceSupport.UnmarkedByClassAsync(db, classIds, start, unmarkedUntil)
            : [];
        var cancelled = classIds.Count == 0 ? [] : await db.TeachingSession.AsNoTracking()
            .Where(s => classIds.Contains(s.ClassId) && s.Status == "Cancelled" && s.SessionDate >= start && s.SessionDate <= end)
            .GroupBy(s => s.ClassId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);

        var now = clock.Now;
        var dueFrom = start.ToDateTime(TimeOnly.MinValue);
        var dueTo = end.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var assignments = classIds.Count == 0 ? [] : (await db.Assignment.AsNoTracking()
                .Where(a => classIds.Contains(a.ClassId) && a.DueAt >= dueFrom && a.DueAt < dueTo && a.DueAt <= now)
                .Select(a => new { a.ClassId, Recipients = a.AssignmentRecipient.Count, Submitted = a.Submission.Count(s => s.SubmittedAt != null) })
                .ToListAsync())
            .GroupBy(a => a.ClassId).ToDictionary(g => g.Key, g => (Count: g.Count(), Recipients: g.Sum(x => x.Recipients), Submitted: g.Sum(x => x.Submitted)));

        var homerooms = classIds.Count == 0 ? [] : (await db.HomeroomAssignment.AsNoTracking()
                .Where(EffectivePeriod.CurrentResponsibility<HomeroomAssignment>(today))
                .Where(h => classIds.Contains(h.ClassId))
                .Select(h => new { h.ClassId, h.Staff.Staff.FullName }).ToListAsync())
            .GroupBy(h => h.ClassId).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.FullName).Order()));

        var classRows = classes.Select(c =>
        {
            var a = assignments.GetValueOrDefault(c.ClassId);
            return new ReportClassRow(c.ClassId, c.ClassName, c.GradeLevel, c.CampusId, c.CampusName, homerooms.GetValueOrDefault(c.ClassId), c.Students,
                AttendanceSupport.Rate(days.Where(d => d.ClassId == c.ClassId)), unmarked.GetValueOrDefault(c.ClassId),
                a.Count, SubmissionRate(a.Recipients, a.Submitted));
        }).OrderBy(r => r.AttendanceRate ?? 101).ThenByDescending(r => r.UnmarkedSessions)
          .ThenBy(r => r.ClassName, StringComparer.CurrentCultureIgnoreCase).ToList();

        var campusRows = campuses.Select(c =>
        {
            var cs = classes.Where(x => x.CampusId == c.CampusId).ToList();
            var ids = cs.Select(x => x.ClassId).ToHashSet();
            var recipients = assignments.Where(a => ids.Contains(a.Key)).Sum(a => a.Value.Recipients);
            var submitted = assignments.Where(a => ids.Contains(a.Key)).Sum(a => a.Value.Submitted);
            return new ReportCampusRow(c.CampusId, c.Name, cs.Count, cs.Sum(x => x.Students),
                AttendanceSupport.Rate(days.Where(d => classCampus[d.ClassId] == c.CampusId)),
                ids.Sum(id => unmarked.GetValueOrDefault(id)), ids.Sum(id => cancelled.GetValueOrDefault(id)),
                assignments.Where(a => ids.Contains(a.Key)).Sum(a => a.Value.Count), SubmissionRate(recipients, submitted));
        }).ToList();

        return new SchoolReportDto(term.YearName, term.SemesterName, start, end, campusRows, classRows,
            Alerts(classRows), Thresholds, gradeOptions, campusId, gradeLevel);
    }

    private static double? SubmissionRate(int recipients, int submitted) =>
        recipients == 0 ? null : Math.Round(submitted * 100.0 / recipients, 1);

    private static List<ReportAlert> Alerts(List<ReportClassRow> classes)
    {
        var alerts = new List<ReportAlert>();
        foreach (var c in classes)
        {
            ReportAlert Make(string kind, double? value, double? threshold) =>
                new(kind, c.ClassId, c.ClassName, c.CampusId, c.CampusName, value, threshold);
            if (c.AttendanceRate is { } rate && rate < Thresholds.LowAttendanceBelow)
                alerts.Add(Make(AlertKinds.LowAttendance, rate, Thresholds.LowAttendanceBelow));
            if (c.UnmarkedSessions >= Thresholds.UnmarkedSessionsFrom)
                alerts.Add(Make(AlertKinds.ManyUnmarked, c.UnmarkedSessions, Thresholds.UnmarkedSessionsFrom));
            if (c.AssignmentCount > 0 && c.SubmissionRate is { } sub && sub < Thresholds.LowSubmissionBelow)
                alerts.Add(Make(AlertKinds.LowSubmission, sub, Thresholds.LowSubmissionBelow));
        }
        return alerts
            .OrderBy(a => Array.IndexOf(AlertOrder, a.Kind))
            .ThenBy(a => a.Kind == AlertKinds.ManyUnmarked ? -(a.Value ?? 0) : a.Value ?? 0)
            .ThenBy(a => a.CampusName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(a => a.ClassName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
