namespace MSMS.Api.Common.Rules;

/// <summary>Quy tắc ngày học của phân hiệu (SCH-03): trong học kỳ và không trùng ngày nghỉ toàn trường hay của phân hiệu.</summary>
public static class SchoolCalendarRules
{
    /// <summary>Thứ 2–7 theo cách gọi của trường (Thứ 2 = Monday ... Thứ 7 = Saturday). Chủ nhật không có tiết.</summary>
    public static DayOfWeek ToDayOfWeek(int vnDay) => (DayOfWeek)(vnDay - 1);

    public static int ToVnDay(DayOfWeek day) => (int)day + 1;

    public static bool IsTeachingWeekday(DateOnly date) => date.DayOfWeek != DayOfWeek.Sunday;

    /// <summary>Các ngày học trong [from, to], bỏ Chủ nhật và ngày nghỉ.</summary>
    public static IEnumerable<DateOnly> SchoolDays(DateOnly from, DateOnly to, ISet<DateOnly> holidays)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (IsTeachingWeekday(d) && !holidays.Contains(d)) yield return d;
        }
    }
}
