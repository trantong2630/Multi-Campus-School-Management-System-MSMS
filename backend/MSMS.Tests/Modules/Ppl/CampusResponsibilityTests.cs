using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Mỗi Hiệu phó chỉ phụ trách một phân hiệu tại một thời điểm (PPL-04, 4.3.28).</summary>
public class CampusResponsibilityTests
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    private static (AccountTestFixture f, ResponsibilityService svc) Setup()
    {
        var f = new AccountTestFixture();
        f.AddAccount(10, AccountTypes.VicePrincipal);
        f.AddAccount(11, AccountTypes.VicePrincipal);
        var user = AccountTestFixture.User(AccountTypes.SchoolAdmin, 900);
        return (f, new ResponsibilityService(f.Db, user, f.Clock, new ScopeResolver(f.Db, user, f.Clock)));
    }

    private static ResponsibilityAssignRequest Assign(int campusId, int staffId, DateOnly start) =>
        new(ResponsibilityKinds.Campus, campusId, staffId, start);

    [Fact]
    public async Task Vice_principal_leading_one_campus_cannot_be_assigned_a_second()
    {
        var (f, svc) = Setup();
        await svc.AssignAsync(Assign(1, 10, Today));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.AssignAsync(Assign(2, 10, Today)));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("chỉ phụ trách một phân hiệu", ex.Message);
        Assert.Single(f.Db.CampusLeadership);
    }

    [Fact]
    public async Task Upcoming_assignment_also_blocks_a_second_campus()
    {
        var (_, svc) = Setup();
        await svc.AssignAsync(Assign(1, 10, Today.AddDays(5)));

        await Assert.ThrowsAsync<ApiException>(() => svc.AssignAsync(Assign(2, 10, Today)));
    }

    [Fact]
    public async Task After_the_first_responsibility_ended_the_vice_principal_can_lead_another_campus()
    {
        var (f, svc) = Setup();
        f.Db.CampusLeadership.Add(new CampusLeadership { CampusId = 1, StaffId = 10, StartDate = Today.AddDays(-30) }); f.Db.SaveChanges();
        f.Db.CampusLeadership.Single().EndDate = Today.AddDays(-1);
        await f.Db.SaveChangesAsync();

        await svc.AssignAsync(Assign(2, 10, Today));

        Assert.Equal(2, f.Db.CampusLeadership.Count());
    }

    [Fact]
    public async Task Replacing_the_holder_of_a_campus_is_not_blocked_and_other_vice_principals_are_unaffected()
    {
        var (f, svc) = Setup();
        f.Db.CampusLeadership.Add(new CampusLeadership { CampusId = 1, StaffId = 10, StartDate = Today.AddDays(-10) }); f.Db.SaveChanges();

        await svc.AssignAsync(Assign(1, 11, Today));   // thay người ở chính phân hiệu đó
        await svc.AssignAsync(Assign(2, 10, Today));   // hiệu phó cũ đã được giải phóng, nhận phân hiệu khác

        Assert.Equal(3, f.Db.CampusLeadership.Count());
    }
}