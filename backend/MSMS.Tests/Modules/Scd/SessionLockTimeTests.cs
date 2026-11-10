using MSMS.Api.Common.Time;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Tests.Modules.Scd;

/// <summary>Mục 6.2: tiết bị khóa khi đã kết thúc — ngày đã qua, hoặc hôm nay và đã quá giờ kết thúc của khung giờ.</summary>
public class SessionLockTimeTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private sealed class FakeClock(DateTime now) : IClock
    {
        public DateTime Now => now;
        public DateOnly Today => DateOnly.FromDateTime(now);
    }

    private static ScheduleSupport At(int hour, int minute) =>
        new(null!, null!, null!, new FakeClock(Today.ToDateTime(new TimeOnly(hour, minute))), null!);

    private static readonly PeriodSlot Period = new() { StartTime = new TimeOnly(13, 30), EndTime = new TimeOnly(14, 15) };

    [Theory]
    [InlineData(13, 0, false)]    // trước giờ bắt đầu
    [InlineData(13, 45, false)]   // đang diễn ra: vẫn đổi được
    [InlineData(14, 14, false)]
    [InlineData(14, 15, true)]    // đúng giờ kết thúc
    [InlineData(16, 0, true)]
    public void TodaysPeriod_IsLockedOnlyAfterItEnds(int hour, int minute, bool ended) =>
        Assert.Equal(ended, At(hour, minute).HasEnded(Today, Period));

    [Fact]
    public void YesterdayEnded_TomorrowNot()
    {
        var support = At(8, 0);
        Assert.True(support.HasEnded(Today.AddDays(-1), Period));
        Assert.False(support.HasEnded(Today.AddDays(1), Period));
    }

    [Fact]
    public void NotEnded_ExpressionMatchesHasEnded()
    {
        var support = At(14, 0);
        var notEnded = support.NotEnded().Compile();
        foreach (var offset in new[] { -1, 0, 1 })
        {
            var s = new TeachingSession { SessionDate = Today.AddDays(offset), PeriodSlot = Period };
            Assert.Equal(!support.HasEnded(s.SessionDate, Period), notEnded(s));
        }
    }
}
