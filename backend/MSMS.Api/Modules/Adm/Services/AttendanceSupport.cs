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

    public static double? Rate(IEnumerable<AttendanceDay> days)
    {
        int present = 0, marked = 0;
        foreach (var d in days) { present += d.Present + d.Late; marked += d.Marked; }
        return marked == 0 ? null : Math.Round(present * 100.0 / marked, 1);
    }

    /// <summary>Tỷ lệ lượt vắng không phép trên số lượt đã ghi.</summary>
    public static double? UnexcusedRate(IEnumerable<AttendanceDay> days)
    {
        int unexcused = 0, marked = 0;
        foreach (var d in days) { unexcused += d.AbsentUnexcused; marked += d.Marked; }
        return DashboardSupport.Percent(unexcused, marked);
    }

    /// <summary>Một chỉ số chuyên cần theo từng tuần (tuần bắt đầu thứ Hai), đúng thứ tự weekStarts.</summary>
    public static List<double?> Weekly(IEnumerable<AttendanceDay> days, IReadOnlyList<DateOnly> weekStarts, Func<IEnumerable<AttendanceDay>, double?> measure)
    {
        var byWeek = days.GroupBy(d => DashboardSupport.WeekStart(d.Date)).ToDictionary(g => g.Key, g => g.ToList());
        return weekStarts.Select(w => byWeek.TryGetValue(w, out var g) ? measure(g) : null).ToList();
    }

    public static AttendanceMix Mix(IEnumerable<AttendanceDay> days)
    {
        var list = days.ToList();
        return new AttendanceMix(list.Sum(d => d.Present), list.Sum(d => d.Late), list.Sum(d => d.AbsentExcused), list.Sum(d => d.AbsentUnexcused));
    }

    /// <summary>Chuyên cần theo thứ Hai tới thứ Bảy.</summary>
    public static List<double?> Weekdays(IEnumerable<AttendanceDay> days)
    {
        var byDay = days.GroupBy(d => d.Date.DayOfWeek).ToDictionary(g => g.Key, g => g.ToList());
        return new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday }
            .Select(d => byDay.TryGetValue(d, out var g) ? Rate(g) : null).ToList();
    }

    /// <summary>Chỉ số chuyên cần ở dạng Kpi: kỳ 4 tuần hiện tại, kỳ liền trước và 8 tuần.</summary>
    public static Kpi Kpi(IReadOnlyCollection<AttendanceDay> days, DateOnly today, IReadOnlyList<DateOnly> weekStarts, Func<IEnumerable<AttendanceDay>, double?> measure)
    {
        var (current, previous) = DashboardSupport.Periods(today);
        return new Kpi(measure(days.Where(d => d.Date >= current)), measure(days.Where(d => d.Date >= previous && d.Date < current)),
            Weekly(days, weekStarts, measure));
    }

    /// <summary>Số ngày học liên tiếp tối thiểu để tính là vắng liên tiếp.</summary>
    public const int StreakDays = 3;

    /// <summary>
    /// Học sinh đang vắng liên tiếp: tính trên các ngày học đã qua (trước hôm nay) mà lớp có điểm danh, từ ngày gần nhất lùi về;
    /// một ngày tính là vắng khi mọi lượt đã ghi của em trong ngày đều là Vắng (có phép hay không đều tính). Chỉ trả em vắng từ 3 ngày.
    /// Key là StudentId, giá trị là lớp và số ngày liên tiếp.
    /// </summary>
    public static async Task<Dictionary<int, (int ClassId, int Days)>> StreaksAsync(MsmsDbContext db, List<int> classIds, DateOnly today)
    {
        if (classIds.Count == 0) return [];
        var from = today.AddDays(-30);
        var rows = await db.AttendanceRecord.AsNoTracking()
            .Where(r => classIds.Contains(r.Session.ClassId) && r.Session.Status == AttendanceRules.SessionScheduled
                && r.Session.SessionDate >= from && r.Session.SessionDate < today)
            .GroupBy(r => new { r.StudentId, r.Session.ClassId, r.Session.SessionDate })
            .Select(g => new { g.Key.StudentId, g.Key.ClassId, Date = g.Key.SessionDate, Attended = g.Count(r => r.Status != AttendanceStatuses.Absent) })
            .ToListAsync();

        var result = new Dictionary<int, (int ClassId, int Days)>();
        foreach (var cls in rows.GroupBy(r => r.ClassId))
        {
            var schoolDays = cls.Select(r => r.Date).Distinct().OrderByDescending(d => d).ToList();
            foreach (var student in cls.GroupBy(r => r.StudentId))
            {
                var absentAllDay = student.ToDictionary(r => r.Date, r => r.Attended == 0);
                var streak = 0;
                foreach (var day in schoolDays)
                {
                    if (!absentAllDay.TryGetValue(day, out var absent) || !absent) break;
                    streak++;
                }
                if (streak >= StreakDays && (!result.TryGetValue(student.Key, out var known) || known.Days < streak))
                    result[student.Key] = (cls.Key, streak);
            }
        }
        return result;
    }
}
