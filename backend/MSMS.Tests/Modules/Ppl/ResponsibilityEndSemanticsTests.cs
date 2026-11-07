using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Lrn.Services;
using MSMS.Api.Modules.Par.Services;
using MSMS.Api.Modules.Sch.Services;
using MSMS.Api.Modules.Scd.Services;
using MSMS.Api.Data.Entities;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>
/// BR-09: kết thúc trách nhiệm từ hôm nay trở về trước thì quyền mất ngay, không đợi hết ngày kết thúc. Các module khác (Par, Lrn, Sch)
/// dùng cùng cách tính với PPL và IAM (CurrentResponsibility); mỗi test thử ngày kết thúc là hôm qua, hôm nay (mất quyền) và ngày mai (còn quyền).
/// </summary>
public class ResponsibilityEndSemanticsTests
{
    [Theory(DisplayName = "PPL | Quyền theo trách nhiệm | Par: chủ nhiệm xem đơn nghỉ | kết thúc hôm qua hoặc hôm nay thì không còn thấy đơn, kết thúc ngày mai thì còn")]
    [InlineData(-1, false)] [InlineData(0, false)] [InlineData(1, true)]
    public async Task Par_HomeroomInbox(int endOffsetDays, bool stillHasAccess)
    {
        using var e = new PplEnv(AccountTypes.Teacher, PplEnv.Teacher);
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, PplEnv.Today.AddDays(-30), PplEnv.Today.AddDays(endOffsetDays));
        e.Db.LeaveRequest.Add(new LeaveRequest { StudentId = PplEnv.StudentA, ClassId = PplEnv.Class6A, ParentId = PplEnv.Parent1, FromDate = PplEnv.Today, ToDate = PplEnv.Today, SessionOfDay = "Morning", Status = "Pending", Reason = "Ốm", CreatedAt = e.Clock.Now });
        e.Db.SaveChanges();

        var inbox = await new HomeroomLeaveService(e.Db, e.Clock, e.User).GetHomeroomInboxAsync(null);

        Assert.Equal(stillHasAccess, inbox.Items.Count > 0);
    }

    [Theory(DisplayName = "PPL | Quyền theo trách nhiệm | Lrn: tổ trưởng xem tiến độ của tổ | kết thúc hôm nay thì không còn là tổ trưởng, kết thúc ngày mai thì còn")]
    [InlineData(-1, false)] [InlineData(0, false)] [InlineData(1, true)]
    public async Task Lrn_HeadDepartmentIds(int endOffsetDays, bool stillHead)
    {
        using var e = new PplEnv(AccountTypes.Teacher, PplEnv.Teacher);
        e.Seed("department", PplEnv.Dept1, PplEnv.Teacher, PplEnv.Today.AddDays(-30), PplEnv.Today.AddDays(endOffsetDays));
        var access = new LrnAccess(e.Db, e.User, e.Clock, new SubjectResponsibilityService(e.Db, e.Clock));

        Assert.Equal(stillHead, (await access.HeadDepartmentIdsAsync()).Contains(PplEnv.Dept1));
    }

    [Theory(DisplayName = "PPL | Quyền theo trách nhiệm | Sch: ngừng hoạt động tổ chuyên môn | tổ trưởng đã kết thúc từ hôm nay thì không còn chặn, kết thúc ngày mai thì còn chặn")]
    [InlineData(-1, false)] [InlineData(0, false)] [InlineData(1, true)]
    public async Task Sch_DeactivateDepartment(int endOffsetDays, bool blocked)
    {
        using var e = new PplEnv();
        e.Seed("department", PplEnv.Dept1, PplEnv.Teacher, PplEnv.Today.AddDays(-30), PplEnv.Today.AddDays(endOffsetDays));
        var svc = new CatalogService(e.Db, e.Clock);

        if (blocked) await PplEnv.Rejects(() => svc.SetDepartmentActiveAsync(PplEnv.Dept1, false), "tổ trưởng");
        else await svc.SetDepartmentActiveAsync(PplEnv.Dept1, false);
    }
}