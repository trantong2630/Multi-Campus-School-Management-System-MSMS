using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>SCH-04 (4.2.21–4.2.24): khung giờ dùng chung toàn trường, khóa là cặp (Buổi, Tiết) (BR-07).</summary>
public class PeriodSlotServiceTests
{
    private static (MsmsDbContext Db, PeriodSlotService Service) Create()
    {
        var db = NewDb();
        return (db, new PeriodSlotService(db));
    }

    private static PeriodSlotRequest Slot(string session, byte no, string start = "07:00", string end = "07:45") =>
        new(session, no, TimeOnly.Parse(start), TimeOnly.Parse(end));

    [Fact]
    public async Task MorningPeriod1_AndAfternoonPeriod1_AreTwoDifferentSlots()
    {
        var (_, svc) = Create();

        await svc.CreateAsync(Slot(SessionOfDay.Morning, 1));
        await svc.CreateAsync(Slot(SessionOfDay.Afternoon, 1, "13:30", "14:15"));

        Assert.Equal(2, (await svc.ListAsync()).Count);
    }

    [Fact]
    public async Task Create_SameSessionAndPeriod_IsRejected()
    {
        var (_, svc) = Create();
        await svc.CreateAsync(Slot(SessionOfDay.Morning, 1));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Slot(SessionOfDay.Morning, 1, "08:00", "08:45")));

        Assert.Contains("Sáng tiết 1", ex.Message);
    }

    [Theory]
    [InlineData("Evening", 1, "07:00", "07:45")]   // buổi không hợp lệ
    [InlineData("Morning", 0, "07:00", "07:45")]    // tiết phải từ 1
    [InlineData("Morning", 1, "07:45", "07:45")]    // kết thúc không sau bắt đầu
    [InlineData("Morning", 1, "08:00", "07:45")]
    public async Task Create_InvalidInput_IsRejected(string session, byte no, string start, string end)
    {
        var (db, svc) = Create();

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Slot(session, no, start, end)));

        Assert.Empty(db.PeriodSlot);
    }

    [Fact]
    public async Task List_PutsMorningFirst_ThenByPeriodNumber()
    {
        var (_, svc) = Create();
        await svc.CreateAsync(Slot(SessionOfDay.Afternoon, 1, "13:30", "14:15"));
        await svc.CreateAsync(Slot(SessionOfDay.Morning, 2, "07:50", "08:35"));
        await svc.CreateAsync(Slot(SessionOfDay.Morning, 1));

        var order = (await svc.ListAsync()).Select(p => $"{p.SessionOfDay}{p.PeriodNo}");

        Assert.Equal(["Morning1", "Morning2", "Afternoon1"], order);
    }

    [Fact]
    public async Task UpdateTimes_ChangesOnlyTheTimes_AndRejectsBadTimes()
    {
        var (_, svc) = Create();
        var slot = await svc.CreateAsync(Slot(SessionOfDay.Morning, 1));

        await svc.UpdateTimesAsync(slot.PeriodSlotId, new PeriodSlotTimesRequest(new TimeOnly(7, 15), new TimeOnly(8, 0)));
        await Assert.ThrowsAsync<ApiException>(() =>
            svc.UpdateTimesAsync(slot.PeriodSlotId, new PeriodSlotTimesRequest(new TimeOnly(9, 0), new TimeOnly(8, 0))));

        var saved = (await svc.ListAsync()).Single();
        Assert.Equal(new TimeOnly(7, 15), saved.StartTime);
        Assert.Equal(new TimeOnly(8, 0), saved.EndTime);
        Assert.Equal(404, (await Assert.ThrowsAsync<ApiException>(() =>
            svc.UpdateTimesAsync(999, new PeriodSlotTimesRequest(new TimeOnly(7, 0), new TimeOnly(8, 0))))).StatusCode);
    }

    [Fact]
    public async Task Deactivate_UnusedSlot_ThenReactivate_Works()
    {
        var (_, svc) = Create();
        var slot = await svc.CreateAsync(Slot(SessionOfDay.Morning, 1));

        await svc.SetActiveAsync(slot.PeriodSlotId, false);
        Assert.False((await svc.ListAsync()).Single().IsActive);

        await svc.SetActiveAsync(slot.PeriodSlotId, true);
        Assert.True((await svc.ListAsync()).Single().IsActive);
    }

    [Fact]
    public async Task Deactivate_SlotUsedByATeachingSession_IsRejected()
    {
        var (db, svc) = Create();
        var slot = await svc.CreateAsync(Slot(SessionOfDay.Morning, 1));
        db.TeachingSession.Add(new TeachingSession
        {
            ClassId = 1, SubjectId = 1, SessionDate = Today, PeriodSlotId = slot.PeriodSlotId,
            Status = "Scheduled", Source = "Generated", CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ApiException>(() => svc.SetActiveAsync(slot.PeriodSlotId, false));

        Assert.True((await svc.ListAsync()).Single().IsActive);
    }

    [Fact]
    public async Task Deactivate_SlotUsedByATimetableEntry_IsRejected()
    {
        var (db, svc) = Create();
        var slot = await svc.CreateAsync(Slot(SessionOfDay.Morning, 1));
        db.TimetableEntry.Add(new TimetableEntry { VersionId = 1, ClassId = 1, DayOfWeek = 2, PeriodSlotId = slot.PeriodSlotId, SubjectId = 1, TeacherId = 2 });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ApiException>(() => svc.SetActiveAsync(slot.PeriodSlotId, false));
    }
}
