namespace MSMS.Api.Common.Rules;

/// <summary>Một ngày có phải ngày học không, và nếu không thì vì sao.</summary>
public enum SchoolDayStatus
{
    SchoolDay,
    /// <summary>Không nằm trong học kỳ nào (trước kỳ 1, giữa hai kỳ, sau kỳ 2).</summary>
    OutsideSemester,
    Sunday,
    /// <summary>Trùng ngày nghỉ toàn trường hoặc của phân hiệu.</summary>
    Holiday,
}

/// <summary>
/// Quy tắc ngày học (SCH-03, 4.2.20): một ngày là ngày dạy khi nằm trong một học kỳ, không phải Chủ nhật và không trùng ngày nghỉ
/// của toàn trường hay của phân hiệu. Phát hành, tiết bù và mọi nơi cần biết "ngày này có dạy không" phải gọi qua đây.
/// </summary>
public static class SchoolCalendarRules
{
    /// <summary>Thứ 2–7 theo cách gọi của trường (Thứ 2 = Monday ... Thứ 7 = Saturday). Chủ nhật không có tiết.</summary>
    public static DayOfWeek ToDayOfWeek(int vnDay) => (DayOfWeek)(vnDay - 1);

    public static int ToVnDay(DayOfWeek day) => (int)day + 1;

    public static bool IsTeachingWeekday(DateOnly date) => date.DayOfWeek != DayOfWeek.Sunday;

    /// <summary>
    /// Phân loại một ngày. <paramref name="holidays"/> là các ngày nghỉ đã áp dụng cho phân hiệu đang xét (toàn trường và của riêng nó).
    /// Ngoài học kỳ được xét trước, rồi tới Chủ nhật, rồi tới ngày nghỉ, để lý do trả về là lý do căn bản nhất.
    /// </summary>
    public static SchoolDayStatus Classify(DateOnly date, IEnumerable<(DateOnly Start, DateOnly End)> semesters, ISet<DateOnly> holidays)
    {
        if (!semesters.Any(s => s.Start <= date && date <= s.End)) return SchoolDayStatus.OutsideSemester;
        if (!IsTeachingWeekday(date)) return SchoolDayStatus.Sunday;
        return holidays.Contains(date) ? SchoolDayStatus.Holiday : SchoolDayStatus.SchoolDay;
    }

    public static bool IsSchoolDay(DateOnly date, IEnumerable<(DateOnly Start, DateOnly End)> semesters, ISet<DateOnly> holidays) =>
        Classify(date, semesters, holidays) == SchoolDayStatus.SchoolDay;

    /// <summary>Các ngày học trong [from, to].</summary>
    public static IEnumerable<DateOnly> SchoolDays(
        DateOnly from, DateOnly to, IReadOnlyCollection<(DateOnly Start, DateOnly End)> semesters, ISet<DateOnly> holidays)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (IsSchoolDay(d, semesters, holidays)) yield return d;
        }
    }
}
