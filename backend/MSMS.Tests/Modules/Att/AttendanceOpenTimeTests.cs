using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Rules;

namespace MSMS.Tests.Modules.Att;

/// <summary>BR-17.1: giáo viên của tiết điểm danh được từ 10 phút trước giờ bắt đầu tiết tới hết ngày của tiết.</summary>
public class AttendanceOpenTimeTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);
    private static readonly TimeOnly Start = new(13, 30);
    private const int TeacherId = 7;

    private static string? Reason(int hour, int minute, DateOnly? date = null, string accountType = AccountTypes.Teacher, int teacherId = TeacherId) =>
        AttendanceRules.WriteBlockReason(accountType, AttendanceRules.SessionScheduled, TeacherId, date ?? Today, Start,
            teacherId, Today.ToDateTime(new TimeOnly(hour, minute)));

    [Theory]
    [InlineData(7, 0, false)]     // đầu ngày: chưa mở
    [InlineData(13, 19, false)]   // còn 11 phút
    [InlineData(13, 20, true)]    // đúng 10 phút trước giờ bắt đầu
    [InlineData(13, 45, true)]    // đang diễn ra
    [InlineData(23, 59, true)]    // tới hết ngày
    public void Teacher_CanWriteFromTenMinutesBeforeStart(int hour, int minute, bool canWrite) =>
        Assert.Equal(canWrite, Reason(hour, minute) is null);

    [Fact]
    public void TooEarly_ReasonNamesOpeningTime() =>
        Assert.Contains("13:20", Reason(8, 0));

    [Fact]
    public void Teacher_StillBlockedOnOtherDays()
    {
        Assert.NotNull(Reason(13, 45, Today.AddDays(1)));
        Assert.NotNull(Reason(13, 45, Today.AddDays(-1)));
    }

    [Fact]
    public void OtherTeacher_IsBlocked() => Assert.NotNull(Reason(13, 45, teacherId: 8));

    [Fact]
    public void SchoolAdmin_IsNotBoundByStartTime() =>
        Assert.Null(Reason(7, 0, accountType: AccountTypes.SchoolAdmin));
}
