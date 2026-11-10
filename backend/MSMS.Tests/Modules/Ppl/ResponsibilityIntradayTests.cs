using Microsoft.EntityFrameworkCore;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Thay người và kết thúc ngay trong ngày: lịch sử ghi được giờ mất quyền (EndedAt), thay nhiều lần trong một ngày không bị chặn.</summary>
public class ResponsibilityIntradayTests
{
    private static DateOnly T => PplEnv.Today;
    private static ResponsibilityAssignRequest Assign(int staff) => new("homeroom", PplEnv.Class6A, staff, T);

    [Fact(DisplayName = "PPL | Trách nhiệm | Thay người | thay ngay trong ngày người cũ vừa được gán: bản cũ còn đúng ngày đó, có giờ mất quyền")]
    public async Task Replace_SameDayAsOldStart_Allowed()
    {
        using var e = new PplEnv();
        await e.Resp.AssignAsync(Assign(PplEnv.Teacher));
        await e.Resp.AssignAsync(Assign(PplEnv.OtherTeacher));

        var records = await e.Db.HomeroomAssignment.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.Equal(T, records[0].StartDate);
        Assert.Equal(T, records[0].EndDate);                     // từ ngày = đến ngày
        Assert.Equal(e.Clock.Now, records[0].EndedAt);           // giờ người cũ mất quyền
        Assert.Equal(PplEnv.OtherTeacher, records[1].StaffId);
        Assert.Null(records[1].EndDate);
        Assert.Null(records[1].EndedAt);
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Thay người | sau khi thay trong ngày, chỉ người mới là đương nhiệm (danh sách, tra chủ nhiệm theo ngày)")]
    public async Task Replace_SameDay_OnlyNewHolderIsCurrent()
    {
        using var e = new PplEnv();
        await e.Resp.AssignAsync(Assign(PplEnv.Teacher));
        await e.Resp.AssignAsync(Assign(PplEnv.OtherTeacher));

        var row = (await e.Resp.ListCurrentAsync("homeroom", null)).Single(r => r.TargetId == PplEnv.Class6A);
        Assert.Equal(PplEnv.OtherTeacher, row.StaffId);
        Assert.Equal(e.Clock.Now, row.StartedAt);
        var homeroom = await new HomeroomService(e.Db).GetHomeroomAsync(PplEnv.Class6A, T);
        Assert.Equal(PplEnv.OtherTeacher, homeroom!.StaffId);
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Thay người | thay qua lại nhiều lần trong một ngày được, người cuối cùng là đương nhiệm")]
    public async Task Replace_ManyTimesSameDay()
    {
        using var e = new PplEnv();
        await e.Resp.AssignAsync(Assign(PplEnv.Teacher));
        await e.Resp.AssignAsync(Assign(PplEnv.OtherTeacher));
        await e.Resp.AssignAsync(Assign(PplEnv.Teacher));

        var records = await e.Db.HomeroomAssignment.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(3, records.Count);
        Assert.All(records.Take(2), r => { Assert.Equal(T, r.EndDate); Assert.NotNull(r.EndedAt); });
        Assert.Null(records[2].EndDate);
        var row = (await e.Resp.ListCurrentAsync("homeroom", null)).Single(r => r.TargetId == PplEnv.Class6A);
        Assert.Equal(PplEnv.Teacher, row.StaffId);
        Assert.Equal(PplEnv.Teacher, (await new HomeroomService(e.Db).GetHomeroomAsync(PplEnv.Class6A, T))!.StaffId);
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Thay người | người cũ giữ từ lâu: kết thúc ngày liền trước và có giờ mất quyền")]
    public async Task Replace_LongHolder_EndsYesterday_WithTime()
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(-30));
        await e.Resp.AssignAsync(Assign(PplEnv.OtherTeacher));

        var old = await e.Db.HomeroomAssignment.OrderBy(x => x.Id).FirstAsync();
        Assert.Equal(T.AddDays(-1), old.EndDate);
        Assert.Equal(e.Clock.Now, old.EndedAt);
    }

    [Theory(DisplayName = "PPL | Trách nhiệm | Kết thúc | có giờ mất quyền khi kết thúc hôm nay; kết thúc hẹn ngày sau thì chưa có")]
    [InlineData(0, true)] [InlineData(3, false)]
    public async Task End_RecordsTimeOnlyWhenAuthorityIsLostNow(int offsetDays, bool hasTime)
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(-30));
        var id = (await e.Db.HomeroomAssignment.SingleAsync()).Id;
        await e.Resp.EndAsync(new ResponsibilityEndRequest("homeroom", id, T.AddDays(offsetDays)));

        var record = await e.Db.HomeroomAssignment.SingleAsync();
        Assert.Equal(hasTime ? e.Clock.Now : null, record.EndedAt);
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Lịch sử | trả giờ nhận và giờ mất quyền, người mới nhất đứng đầu, chỉ người mới là hiện tại")]
    public async Task History_ReturnsTimes_NewestFirst()
    {
        using var e = new PplEnv();
        await e.Resp.AssignAsync(Assign(PplEnv.Teacher));
        await e.Resp.AssignAsync(Assign(PplEnv.OtherTeacher));

        var history = await e.Resp.HistoryAsync("homeroom", PplEnv.Class6A);
        Assert.Equal(2, history.Count);
        Assert.Equal(PplEnv.OtherTeacher, history[0].StaffId);
        Assert.True(history[0].IsEffective);
        Assert.Null(history[0].EndedAt);
        Assert.Equal(e.Clock.Now, history[0].AssignedAt);
        Assert.Equal(PplEnv.Teacher, history[1].StaffId);
        Assert.False(history[1].IsEffective);                    // kết thúc hôm nay = đã mất quyền
        Assert.Equal(e.Clock.Now, history[1].EndedAt);
    }
}