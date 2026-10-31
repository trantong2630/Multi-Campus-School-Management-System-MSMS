namespace MSMS.Api.Common.Rules;

/// <summary>Quy tắc học kỳ (SCH-03). Nguồn duy nhất của mốc kết thúc muộn nhất: backend kiểm theo đây và trả mốc này cho giao diện hiển thị.</summary>
public static class SemesterRules
{
    /// <summary>
    /// Ngày kết thúc muộn nhất của học kỳ: học kỳ 1 là 18/01, học kỳ 2 là 31/05 của năm kết thúc năm học, và không quá ngày kết thúc năm học.
    /// </summary>
    public static DateOnly LatestEnd(int semesterNo, DateOnly yearEnd)
    {
        var mark = new DateOnly(yearEnd.Year, semesterNo == 1 ? 1 : 5, semesterNo == 1 ? 18 : 31);
        return mark < yearEnd ? mark : yearEnd;
    }
}
