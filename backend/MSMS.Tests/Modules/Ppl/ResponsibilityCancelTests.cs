using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>BR-09: lịch hẹn trách nhiệm chưa bắt đầu hủy được; khóa tài khoản và cho nghỉ hủy luôn lịch hẹn của người đó.</summary>
public class ResponsibilityCancelTests
{
    private const string Homeroom = ResponsibilityKinds.Homeroom;

    [Fact(DisplayName = "PPL | Hủy lịch hẹn | Chủ nhiệm hẹn trước | hủy xong gán được người khác ngay hôm nay")]
    public async Task CancelUpcoming_ThenAssignSomeoneElseToday()
    {
        using var e = new PplEnv();
        await e.Resp.AssignAsync(new ResponsibilityAssignRequest(Homeroom, PplEnv.Class6A, PplEnv.Teacher, PplEnv.Today.AddDays(5)));
        var row = (await e.Resp.ListCurrentAsync(Homeroom, null)).Single(r => r.TargetId == PplEnv.Class6A);

        await e.Resp.CancelAsync(new ResponsibilityCancelRequest(Homeroom, row.NextRecordId!.Value));
        await e.Resp.AssignAsync(new ResponsibilityAssignRequest(Homeroom, PplEnv.Class6A, PplEnv.OtherTeacher, PplEnv.Today));

        var holder = Assert.Single(e.Db.HomeroomAssignment);
        Assert.Equal(PplEnv.OtherTeacher, holder.StaffId);
    }

    [Fact(DisplayName = "PPL | Hủy lịch hẹn | Trách nhiệm đã bắt đầu | bị từ chối, phải dùng Kết thúc")]
    public async Task CancelStarted_IsRejected()
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, PplEnv.Today);
        var id = e.Db.HomeroomAssignment.Single().Id;

        await PplEnv.Rejects(() => e.Resp.CancelAsync(new ResponsibilityCancelRequest(Homeroom, id)), "Kết thúc");
        Assert.Single(e.Db.HomeroomAssignment);
    }

    [Fact(DisplayName = "PPL | Hủy lịch hẹn | Cho cán bộ nghỉ | lịch hẹn chưa bắt đầu của người đó bị hủy")]
    public async Task DeactivateStaff_CancelsUpcomingDuties()
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, PplEnv.Today.AddDays(5));
        e.Seed("department", PplEnv.Dept1, PplEnv.Teacher, PplEnv.Today.AddDays(5));

        await e.Staff.SetActiveAsync(PplEnv.Teacher, false);

        Assert.Empty(e.Db.HomeroomAssignment);
        Assert.Empty(e.Db.DepartmentHead);
    }

    [Fact(DisplayName = "IAM | Khóa tài khoản | Giáo viên có lịch hẹn chủ nhiệm | xem trước liệt kê lịch hẹn, khóa xong lịch hẹn bị hủy")]
    public async Task LockAccount_ListsAndCancelsUpcomingDuties()
    {
        using var f = new AccountTestFixture();
        var acc = f.AddAccount(5, AccountTypes.Teacher);
        f.Db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = 1, StaffId = 5, StartDate = f.Clock.Today.AddDays(5), CreatedAt = f.Clock.Now });
        f.Db.SaveChanges();

        var impact = await f.Accounts.LockImpactAsync(acc.AccountId);
        Assert.Contains("Chủ nhiệm", Assert.Single(impact.UpcomingDuties));

        await f.Accounts.LockAsync(acc.AccountId);

        Assert.Equal(AccountStatuses.Locked, f.Db.Account.Find(acc.AccountId)!.Status);
        Assert.Empty(f.Db.HomeroomAssignment);
    }
}
