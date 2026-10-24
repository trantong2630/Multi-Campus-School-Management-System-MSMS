namespace MSMS.Api.Common.Rules;

public static class LeaveRequestRules
{
    private const string Morning = "Morning";
    private const string Afternoon = "Afternoon";
    private const string AllDay = "AllDay";

    /// <summary>
    /// BR-19: Kiểm tra xung đột giữa đơn xin nghỉ mới và các đơn đã tồn tại của cùng học sinh.
    /// Xung đột xảy ra khi chồng ngày VÀ chồng buổi.
    /// </summary>
    public static bool HasConflict(
        DateOnly fromDate,
        DateOnly toDate,
        string sessionOfDay,
        IEnumerable<(DateOnly From, DateOnly To, string Session)> existingLeaves)
    {
        foreach (var existing in existingLeaves)
        {
            if (HasDateOverlap(fromDate, toDate, existing.From, existing.To) &&
                HasSessionOverlap(sessionOfDay, existing.Session))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDateOverlap(DateOnly from1, DateOnly to1, DateOnly from2, DateOnly to2)
        => from1 <= to2 && from2 <= to1;

    private static bool HasSessionOverlap(string session1, string session2)
    {
        if (session1 == AllDay || session2 == AllDay)
            return true;

        return session1 == session2;
    }
}
