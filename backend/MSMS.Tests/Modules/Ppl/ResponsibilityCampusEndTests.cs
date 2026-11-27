using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>
/// BR-09: không để phân hiệu đang hoạt động mất Hiệu phó. Kết thúc phụ trách phân hiệu có hiệu lực hôm nay mà phân hiệu còn lớp hoạt động
/// trong năm học hiện hành và chưa có người kế nhiệm thì bị chặn, phải dùng Thay người; kết thúc vào ngày sau (bàn giao có hẹn) vẫn được.
/// </summary>
public class ResponsibilityCampusEndTests
{
    private const string Campus = ResponsibilityKinds.Campus;
    private const int SecondVicePrincipal = 17;

    private static (PplEnv Env, int RecordId) Create()
    {
        var e = new PplEnv();
        e.Seed("campus", PplEnv.Campus1, PplEnv.ViceP, PplEnv.Today.AddDays(-30));
        return (e, e.Db.CampusLeadership.Single().Id);
    }

    [Fact(DisplayName = "PPL | Kết thúc phụ trách phân hiệu | Còn lớp hoạt động, chưa có người kế nhiệm | kết thúc hôm nay bị chặn, bản ghi giữ nguyên")]
    public async Task EndToday_WithActiveClasses_AndNoSuccessor_IsRejected()
    {
        var (e, id) = Create();
        using var _ = e;

        await PplEnv.Rejects(() => e.Resp.EndAsync(new ResponsibilityEndRequest(Campus, id, PplEnv.Today)), "Thay người");

        var record = e.Db.CampusLeadership.Single();
        Assert.Null(record.EndDate);
        Assert.Null(record.EndedAt);
    }

    [Fact(DisplayName = "PPL | Kết thúc phụ trách phân hiệu | Thông báo | nêu tên phân hiệu và số lớp đang hoạt động")]
    public async Task Rejection_NamesTheCampusAndTheClassCount()
    {
        var (e, id) = Create();
        using var _ = e;

        var ex = await Assert.ThrowsAsync<MSMS.Api.Common.Errors.ApiException>(() =>
            e.Resp.EndAsync(new ResponsibilityEndRequest(Campus, id, PplEnv.Today)));

        Assert.Contains("Phân hiệu 1", ex.Message);
        Assert.Contains("2 lớp", ex.Message);   // 6A và 9A
    }

    [Fact(DisplayName = "PPL | Kết thúc phụ trách phân hiệu | Bàn giao có hẹn | kết thúc vào ngày sau vẫn được")]
    public async Task EndInTheFuture_IsAllowed()
    {
        var (e, id) = Create();
        using var _ = e;

        await e.Resp.EndAsync(new ResponsibilityEndRequest(Campus, id, PplEnv.Today.AddDays(3)));

        Assert.Equal(PplEnv.Today.AddDays(3), e.Db.CampusLeadership.Single().EndDate);
    }

    [Fact(DisplayName = "PPL | Kết thúc phụ trách phân hiệu | Phân hiệu không còn lớp hoạt động | kết thúc hôm nay được")]
    public async Task EndToday_WhenNoActiveClasses_IsAllowed()
    {
        using var e = new PplEnv();
        e.Seed("campus", PplEnv.CampusInactive, PplEnv.ViceP, PplEnv.Today.AddDays(-30));   // phân hiệu cũ, không có lớp nào
        var id = e.Db.CampusLeadership.Single().Id;

        await e.Resp.EndAsync(new ResponsibilityEndRequest(Campus, id, PplEnv.Today));

        Assert.Equal(PplEnv.Today, e.Db.CampusLeadership.Single().EndDate);
    }

    [Fact(DisplayName = "PPL | Kết thúc phụ trách phân hiệu | Đã có người kế nhiệm | kết thúc hôm nay được")]
    public async Task EndToday_WithASuccessor_IsAllowed()
    {
        var (e, id) = Create();
        using var _ = e;
        e.AddStaff(SecondVicePrincipal, "Hiệu phó B", AccountTypes.VicePrincipal);
        e.Seed("campus", PplEnv.Campus1, SecondVicePrincipal, PplEnv.Today.AddDays(1));   // người kế nhiệm đã hẹn

        await e.Resp.EndAsync(new ResponsibilityEndRequest(Campus, id, PplEnv.Today));

        Assert.Equal(PplEnv.Today, e.Db.CampusLeadership.Single(x => x.Id == id).EndDate);
    }

    [Fact(DisplayName = "PPL | Kết thúc phụ trách phân hiệu | Thay người | vẫn dùng được khi phân hiệu còn lớp hoạt động")]
    public async Task Replace_StillWorks_WithActiveClasses()
    {
        var (e, id) = Create();
        using var _ = e;
        e.AddStaff(SecondVicePrincipal, "Hiệu phó B", AccountTypes.VicePrincipal);

        await e.Resp.AssignAsync(new ResponsibilityAssignRequest(Campus, PplEnv.Campus1, SecondVicePrincipal, PplEnv.Today));

        Assert.Equal(PplEnv.Today, e.Db.CampusLeadership.Single(x => x.Id == id).EndDate);
        Assert.Null(e.Db.CampusLeadership.Single(x => x.StaffId == SecondVicePrincipal).EndDate);
    }

    [Fact(DisplayName = "PPL | Kết thúc phụ trách phân hiệu | Tổ trưởng và chủ nhiệm | không bị áp ràng buộc này")]
    public async Task EndDepartmentAndHomeroom_AreNotAffected()
    {
        using var e = new PplEnv();
        e.Seed("department", PplEnv.Dept1, PplEnv.Teacher, PplEnv.Today.AddDays(-30));
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.OtherTeacher, PplEnv.Today.AddDays(-30));

        await e.Resp.EndAsync(new ResponsibilityEndRequest(ResponsibilityKinds.Department, e.Db.DepartmentHead.Single().Id, PplEnv.Today));
        await e.Resp.EndAsync(new ResponsibilityEndRequest(ResponsibilityKinds.Homeroom, e.Db.HomeroomAssignment.Single().Id, PplEnv.Today));

        Assert.Equal(PplEnv.Today, e.Db.DepartmentHead.Single().EndDate);
        Assert.Equal(PplEnv.Today, e.Db.HomeroomAssignment.Single().EndDate);
    }
}
