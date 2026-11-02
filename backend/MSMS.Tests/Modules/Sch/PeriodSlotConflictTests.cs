using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>
/// Giờ của các khung tiết đang dùng không được chồng nhau và phải đúng thứ tự: kiểm xung đột giáo viên (BR-13) dựa trên khung giờ nên
/// giờ chồng nhau làm lọt xung đột thật (một giáo viên dạy hai lớp cùng lúc ở hai khung khác nhau mà không bị báo).
/// </summary>
public class PeriodSlotConflictTests
{
    private static (MsmsDbContext Db, PeriodSlotService Service) Create()
    {
        var db = NewDb();
        return (db, new PeriodSlotService(db));
    }

    private static PeriodSlotRequest Slot(string session, byte no, string start, string end) =>
        new(session, no, TimeOnly.Parse(start), TimeOnly.Parse(end));

    [Fact]
    public async Task OverlappingTimesInTheSameSession_AreRejected_WithTheClashingSlotNamed()
    {
        var (_, svc) = Create();
        await svc.CreateAsync(Slot("Morning", 1, "07:00", "07:45"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Slot("Morning", 2, "07:30", "08:15")));

        Assert.Contains("Sáng tiết 1", ex.Message);
        Assert.Contains("07:00", ex.Message);
    }

    [Fact]
    public async Task BackToBackPeriods_AreAllowed_WhenOneEndsExactlyWhenTheNextStarts()
    {
        var (_, svc) = Create();
        await svc.CreateAsync(Slot("Morning", 1, "07:00", "07:45"));

        await svc.CreateAsync(Slot("Morning", 2, "07:45", "08:30"));

        Assert.Equal(2, (await svc.ListAsync()).Count);
    }

    [Fact]
    public async Task APeriodThatStartsBeforeAnEarlierNumberedOne_IsRejected()
    {
        var (_, svc) = Create();
        await svc.CreateAsync(Slot("Morning", 3, "07:50", "08:35"));

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Slot("Morning", 2, "08:40", "09:25")));   // tiết 2 nằm sau tiết 3
        await svc.CreateAsync(Slot("Morning", 2, "07:00", "07:45"));                                             // đúng thứ tự thì được
    }

    [Fact]
    public async Task MorningMustEndBeforeAfternoonStarts()
    {
        var (_, svc) = Create();
        await svc.CreateAsync(Slot("Afternoon", 1, "13:30", "14:15"));

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Slot("Morning", 5, "13:00", "13:45")));   // buổi Sáng chạm sang buổi Chiều
        await svc.CreateAsync(Slot("Morning", 5, "10:35", "11:20"));
    }

    [Fact]
    public async Task AnAfternoonSlotThatStartsBeforeTheMorningEnds_IsRejected()
    {
        var (_, svc) = Create();
        await svc.CreateAsync(Slot("Morning", 1, "07:00", "07:45"));

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Slot("Afternoon", 1, "07:30", "08:15")));
    }

    [Fact]
    public async Task AFullDayOfTenPeriods_IsAcceptedInAnyOrderOfCreation()
    {
        var (_, svc) = Create();
        (string, byte, string, string)[] day =
        [
            ("Afternoon", 3, "15:20", "16:05"), ("Morning", 1, "07:00", "07:45"), ("Morning", 3, "08:55", "09:40"), ("Afternoon", 1, "13:30", "14:15"),
            ("Morning", 2, "07:50", "08:35"), ("Afternoon", 2, "14:20", "15:05"), ("Morning", 4, "09:45", "10:30"), ("Morning", 5, "10:35", "11:20"),
            ("Afternoon", 4, "16:10", "16:55"), ("Afternoon", 5, "17:00", "17:45"),
        ];

        foreach (var (session, no, start, end) in day) await svc.CreateAsync(Slot(session, no, start, end));

        Assert.Equal(10, (await svc.ListAsync()).Count);
    }

    [Fact]
    public async Task UpdatingTimes_CannotCreateAnOverlap_ButOwnOldTimesAreNotAConflict()
    {
        var (_, svc) = Create();
        var first = await svc.CreateAsync(Slot("Morning", 1, "07:00", "07:45"));
        await svc.CreateAsync(Slot("Morning", 2, "07:50", "08:35"));

        await svc.UpdateTimesAsync(first.PeriodSlotId, new PeriodSlotTimesRequest(new TimeOnly(7, 5), new TimeOnly(7, 48)));   // dịch nhẹ, vẫn không chạm
        await Assert.ThrowsAsync<ApiException>(() =>
            svc.UpdateTimesAsync(first.PeriodSlotId, new PeriodSlotTimesRequest(new TimeOnly(7, 5), new TimeOnly(8, 0))));      // lấn sang tiết 2
    }

    [Fact]
    public async Task InactiveSlots_DoNotBlock_ButReactivatingOneThatNowClashesIsRejected()
    {
        var (_, svc) = Create();
        var second = await svc.CreateAsync(Slot("Morning", 2, "07:50", "08:35"));
        await svc.SetActiveAsync(second.PeriodSlotId, false);

        await svc.CreateAsync(Slot("Morning", 3, "08:00", "08:45"));           // khung tiết 2 đang ngừng dùng nên không chiếm giờ

        await Assert.ThrowsAsync<ApiException>(() => svc.SetActiveAsync(second.PeriodSlotId, true));   // bật lại thì chồng với tiết 3
        Assert.False((await svc.ListAsync()).Single(p => p.PeriodNo == 2).IsActive);
    }
}
