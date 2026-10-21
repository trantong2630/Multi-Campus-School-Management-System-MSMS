namespace MSMS.Api.Common.Time;

/// <summary>"Hôm nay" luôn theo giờ Việt Nam (nguyên tắc 5). Không gọi DateTime.Now ở nơi khác.</summary>
public interface IClock
{
    DateTime Now { get; }
    DateOnly Today { get; }
}

public sealed class SystemClock : IClock
{
    private static readonly TimeZoneInfo Vn = Resolve();

    public DateTime Now => TimeZoneInfo.ConvertTime(DateTime.UtcNow, Vn);
    public DateOnly Today => DateOnly.FromDateTime(Now);

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        }
        return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "VN", "VN");
    }
}
