using Microsoft.EntityFrameworkCore;
using MSMS.Api.Data;

namespace MSMS.Api.Modules.Adm.Services;

internal sealed record CurrentTerm(
    int YearId, string? YearName, int? SemesterId, string? SemesterName, DateOnly? SemesterStart, DateOnly? SemesterEnd, DateOnly? YearStart = null);

/// <summary>Phép tính dùng chung của các màn Tình hình. Chỉ đọc và tổng hợp, không lưu số liệu suy ra.</summary>
internal static class DashboardSupport
{
    /// <summary>Độ dài kỳ so sánh của mọi chỉ số chính: 4 tuần tính tới hôm nay, so với 4 tuần liền trước.</summary>
    public const int PeriodDays = 28;

    /// <summary>Số tuần của đường xu hướng (tuần cuối là tuần này).</summary>
    public const int TrendWeeks = 8;

    public static async Task<CurrentTerm> CurrentTermAsync(MsmsDbContext db, DateOnly today)
    {
        var year = await db.AcademicYear.AsNoTracking().Where(y => y.IsCurrent)
            .Select(y => new { y.AcademicYearId, y.Name, y.StartDate }).FirstOrDefaultAsync();
        if (year is null) return new CurrentTerm(0, null, null, null, null, null);
        var sem = await db.Semester.AsNoTracking()
            .Where(s => s.AcademicYearId == year.AcademicYearId && s.StartDate <= today && s.EndDate >= today)
            .Select(s => new { s.SemesterId, s.Name, s.StartDate, s.EndDate }).FirstOrDefaultAsync();
        return new CurrentTerm(year.AcademicYearId, year.Name, sem?.SemesterId, sem?.Name, sem?.StartDate, sem?.EndDate, year.StartDate);
    }

    /// <summary>Thứ Hai của tuần chứa ngày D.</summary>
    public static DateOnly WeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    /// <summary>Tuần thứ mấy của năm học (tuần 1 là tuần chứa ngày khai giảng); trống khi chưa có năm học hoặc chưa tới ngày bắt đầu.</summary>
    public static int? WeekNo(CurrentTerm term, DateOnly today) =>
        term.YearStart is { } start && today >= start ? (WeekStart(today).DayNumber - WeekStart(start).DayNumber) / 7 + 1 : null;

    /// <summary>Thứ Hai của 8 tuần xu hướng, cũ đến mới.</summary>
    public static List<DateOnly> TrendWeekStarts(DateOnly today) =>
        Enumerable.Range(0, TrendWeeks).Select(i => WeekStart(today).AddDays(-7 * (TrendWeeks - 1 - i))).ToList();

    /// <summary>Ngày đầu tiên cần đọc để có đủ 8 tuần xu hướng và hai kỳ 4 tuần so sánh.</summary>
    public static DateOnly ReadFrom(DateOnly today)
    {
        var trend = TrendWeekStarts(today)[0];
        var previous = today.AddDays(-2 * PeriodDays + 1);
        return trend < previous ? trend : previous;
    }

    /// <summary>Ngày đầu của kỳ hiện tại (4 tuần tính tới hôm nay) và của kỳ liền trước.</summary>
    public static (DateOnly Current, DateOnly Previous) Periods(DateOnly today) =>
        (today.AddDays(-PeriodDays + 1), today.AddDays(-2 * PeriodDays + 1));

    public static double? Percent(int part, int whole) => whole == 0 ? null : Math.Round(part * 100.0 / whole, 1);
}
