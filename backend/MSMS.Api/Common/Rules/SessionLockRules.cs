using System.Linq.Expressions;
using MSMS.Api.Common.Time;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Common.Rules;

/// <summary>
/// Mục 6.2: tiết bị khóa khi đã kết thúc — ngày của tiết đã qua, hoặc là hôm nay và đã quá giờ kết thúc của khung giờ.
/// Một nơi duy nhất cho mọi chỗ cần hỏi "tiết này còn đổi được không" để các màn hình không lệch nhau.
/// </summary>
public static class SessionLockRules
{
    public static bool HasEnded(IClock clock, DateOnly date, PeriodSlot slot) =>
        date < clock.Today || (date == clock.Today && slot.EndTime <= TimeOnly.FromDateTime(clock.Now));

    /// <summary>Điều kiện truy vấn: tiết chưa kết thúc (ngược của <see cref="HasEnded"/>).</summary>
    public static Expression<Func<TeachingSession, bool>> NotEnded(IClock clock)
    {
        var today = clock.Today;
        var time = TimeOnly.FromDateTime(clock.Now);
        return s => s.SessionDate > today || (s.SessionDate == today && s.PeriodSlot.EndTime > time);
    }
}
