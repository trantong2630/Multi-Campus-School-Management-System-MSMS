using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data;
using MSMS.Api.Modules.Adm.Dtos;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>Số lượt điểm danh của một lớp trong một ngày, đã tách vắng có phép và không phép theo BR-18.</summary>
internal sealed record AttendanceDay(int ClassId, DateOnly Date, int Present, int Late, int AbsentExcused, int AbsentUnexcused)
{
    public int Marked => Present + Late + AbsentExcused + AbsentUnexcused;
}

/// <summary>
/// Thống kê điểm danh cho Bảng điều khiển. Chỉ đọc; nhãn có phép đi qua <see cref="AttendanceRules.IsExcused"/> nên khớp màn Điểm danh (spec 6.2).
/// Chỉ tính tiết Đã xếp lịch. Tỷ lệ có mặt = (Có mặt + Muộn) trên số lượt đã ghi; tiết chưa điểm danh không làm giảm tỷ lệ mà được đếm riêng.
/// </summary>
internal static class AttendanceSupport
{
    public static async Task<List<AttendanceDay>> DaysAsync(MsmsDbContext db, List<int> classIds, DateOnly from, DateOnly to, List<int>? subjectIds = null)
    {
        if (classIds.Count == 0) return [];
        var sessions = db.AttendanceRecord.AsNoTracking()
            .Where(r => classIds.Contains(r.Session.ClassId) && r.Session.Status == AttendanceRules.SessionScheduled
                && r.Session.SessionDate >= from && r.Session.SessionDate <= to);
        if (subjectIds is not null) sessions = sessions.Where(r => subjectIds.Contains(r.Session.SubjectId));

        var tallies = await sessions
            .GroupBy(r => new { r.Session.ClassId, r.Session.SessionDate, r.Status })
            .Select(g => new { g.Key.ClassId, Date = g.Key.SessionDate, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        var absents = await sessions.Where(r => r.Status == AttendanceStatuses.Absent)
            .Select(r => new
            {
                r.StudentId, r.Session.ClassId, Date = r.Session.SessionDate,
                r.Session.PeriodSlot.SessionOfDay, r.Session.PeriodSlot.PeriodNo,
            }).ToListAsync();
        var studentIds = absents.Select(a => a.StudentId).Distinct().ToList();
        var leaves = studentIds.Count == 0 ? [] : await db.LeaveRequest.AsNoTracking()
            .Where(l => l.Status == AttendanceRules.LeaveApproved && studentIds.Contains(l.StudentId) && l.FromDate <= to && l.ToDate >= from)
            .Select(l => new ApprovedLeave(l.StudentId, l.FromDate, l.ToDate, l.SessionOfDay, l.SlotIds)).ToListAsync();
        var excused = absents
            .Where(a => AttendanceRules.IsExcused(a.StudentId, a.Date, a.SessionOfDay, a.PeriodNo, leaves))
            .GroupBy(a => (a.ClassId, a.Date)).ToDictionary(g => g.Key, g => g.Count());

        return tallies.GroupBy(t => (t.ClassId, t.Date)).Select(g =>
        {
            int Of(string status) => g.Where(t => t.Status == status).Sum(t => t.Count);
            var absent = Of(AttendanceStatuses.Absent);
            var ex = excused.GetValueOrDefault(g.Key);
            return new AttendanceDay(g.Key.ClassId, g.Key.Date, Of(AttendanceStatuses.Present), Of(AttendanceStatuses.Late), ex, absent - ex);
        }).ToList();
    }

    /// <summary>Số tiết đã qua ngày, Đã xếp lịch mà chưa có bản ghi điểm danh nào, theo lớp.</summary>
    public static async Task<Dictionary<int, int>> UnmarkedByClassAsync(MsmsDbContext db, List<int> classIds, DateOnly from, DateOnly today)
    {
        if (classIds.Count == 0) return [];
        return await db.TeachingSession.AsNoTracking()
            .Where(s => classIds.Contains(s.ClassId) && s.Status == AttendanceRules.SessionScheduled
                && s.SessionDate >= from && s.SessionDate < today && !s.AttendanceRecord.Any())
            .GroupBy(s => s.ClassId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }

    /// <summary>Số buổi vắng của từng học sinh trong lớp, tách có phép và không phép.</summary>
    public static async Task<Dictionary<int, (int Excused, int Unexcused)>> AbsencesByStudentAsync(MsmsDbContext db, int classId, DateOnly from, DateOnly to)
    {
        var absents = await db.AttendanceRecord.AsNoTracking()
            .Where(r => r.Session.ClassId == classId && r.Status == AttendanceStatuses.Absent && r.Session.Status == AttendanceRules.SessionScheduled
                && r.Session.SessionDate >= from && r.Session.SessionDate <= to)
            .Select(r => new { r.StudentId, Date = r.Session.SessionDate, r.Session.PeriodSlot.SessionOfDay, r.Session.PeriodSlot.PeriodNo })
            .ToListAsync();
        if (absents.Count == 0) return [];
        var studentIds = absents.Select(a => a.StudentId).Distinct().ToList();
        var leaves = await db.LeaveRequest.AsNoTracking()
            .Where(l => l.Status == AttendanceRules.LeaveApproved && studentIds.Contains(l.StudentId) && l.FromDate <= to && l.ToDate >= from)
            .Select(l => new ApprovedLeave(l.StudentId, l.FromDate, l.ToDate, l.SessionOfDay, l.SlotIds)).ToListAsync();
        return absents.GroupBy(a => a.StudentId).ToDictionary(g => g.Key, g =>
        {
            var ex = g.Count(a => AttendanceRules.IsExcused(a.StudentId, a.Date, a.SessionOfDay, a.PeriodNo, leaves));
            return (ex, g.Count() - ex);
        });
    }

    public static double? Rate(IEnumerable<AttendanceDay> days)
    {
        int present = 0, marked = 0;
        foreach (var d in days) { present += d.Present + d.Late; marked += d.Marked; }
        return marked == 0 ? null : Math.Round(present * 100.0 / marked, 1);
    }

    public static AttendanceSummary Summary(IReadOnlyCollection<AttendanceDay> days, int unmarked, int todaySessions, int todayMarkedSessions) =>
        new(Rate(days), days.Sum(d => d.Present), days.Sum(d => d.Late), days.Sum(d => d.AbsentExcused), days.Sum(d => d.AbsentUnexcused),
            unmarked, todaySessions, todayMarkedSessions);

    /// <summary>8 tuần gần nhất tính cả tuần này (tuần bắt đầu thứ Hai): tỷ lệ có mặt và số lượt vắng.</summary>
    public static List<AttendanceWeek> Weeks(IReadOnlyCollection<AttendanceDay> days, DateOnly today)
    {
        var byWeek = days.GroupBy(d => DashboardSupport.WeekStart(d.Date)).ToDictionary(g => g.Key, g => g.ToList());
        return Enumerable.Range(0, 8).Select(i => DashboardSupport.WeekStart(today).AddDays(-7 * (7 - i)))
            .Select(w => byWeek.TryGetValue(w, out var g)
                ? new AttendanceWeek(w, Rate(g), g.Sum(d => d.AbsentExcused + d.AbsentUnexcused))
                : new AttendanceWeek(w, null, 0)).ToList();
    }

    /// <summary>Tỷ lệ có mặt theo thứ trong tuần (Thứ Hai tới Thứ Bảy): cho thấy ngày nào vắng nhiều.</summary>
    public static List<WeekdayRate> Weekdays(IEnumerable<AttendanceDay> days)
    {
        var byDay = days.GroupBy(d => d.Date.DayOfWeek).ToDictionary(g => g.Key, g => g.ToList());
        return new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday }
            .Select(d => byDay.TryGetValue(d, out var g)
                ? new WeekdayRate((int)d, Rate(g), g.Sum(x => x.AbsentExcused + x.AbsentUnexcused))
                : new WeekdayRate((int)d, null, 0)).ToList();
    }
}
