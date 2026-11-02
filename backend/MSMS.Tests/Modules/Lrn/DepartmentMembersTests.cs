using MSMS.Api.Common.Auth;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Services;
using MSMS.Api.Modules.Scd.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Lrn;

/// <summary>LRN-05: tổ trưởng thấy thành viên tổ và ai đang dạy môn của tổ ở lớp nào; người không làm tổ trưởng không thấy gì.</summary>
public class DepartmentMembersTests
{
    private static DepartmentService Service(AccountTestFixture f, int personId)
    {
        var user = AccountTestFixture.User(AccountTypes.Teacher, personId);
        var responsibility = new SubjectResponsibilityService(f.Db, f.Clock);
        return new DepartmentService(f.Db, f.Clock, new LrnAccess(f.Db, user, f.Clock, responsibility), responsibility);
    }

    [Fact]
    public async Task Head_SeesMembersAndTeaching_OthersSeeNothing()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(5);          // tổ trưởng, thuộc tổ
        f.AddAccount(6);          // thuộc tổ, chưa dạy lớp nào
        f.AddAccount(7);          // tổ khác nhưng đang dạy môn của tổ
        f.Db.StaffProfile.Find(5)!.DepartmentId = 1;
        f.Db.StaffProfile.Find(6)!.DepartmentId = 1;
        f.Db.DepartmentHead.Add(new DepartmentHead { DepartmentId = 1, StaffId = 5, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.SaveChanges();
        f.Publish(1, 1, 7);

        var d = await Service(f, 5).OverviewAsync(null);
        Assert.Equal(3, d.Members.Count);
        Assert.True(d.Members.Single(m => m.PersonId == 6).InDepartment);
        Assert.Empty(d.Members.Single(m => m.PersonId == 6).Teaching);
        var outsider = d.Members.Single(m => m.PersonId == 7);
        Assert.False(outsider.InDepartment);
        Assert.Equal("6A", Assert.Single(Assert.Single(outsider.Teaching).ClassNames));
        Assert.Equal(1, outsider.PeriodsPerWeek);

        var notHead = await Service(f, 6).OverviewAsync(null);
        Assert.False(notHead.IsHead);
        Assert.Empty(notHead.Members);
    }
}
