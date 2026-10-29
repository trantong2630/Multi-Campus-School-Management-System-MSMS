using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using MSMS.Api.Common.Auth;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>
/// Quyền đọc của SCH theo spec: phân hiệu chỉ Hiệu trưởng và Hiệu phó đọc thêm (cùng School Admin); danh sách lớp theo phạm vi
/// (Hiệu phó: phân hiệu mình phụ trách, giáo viên: lớp mình chủ nhiệm).
/// </summary>
public class SchReadScopeTests
{
    private const int Campus1 = 1, Campus2 = 2, Year1 = 1, Teacher = 50, OtherTeacher = 51, ViceOfCampus1 = 70, ViceOfCampus2 = 71;

    private static (MsmsDbContext Db, ClassService Service) CreateAs(string accountType, int personId, DateOnly? homeroomEnd = null)
    {
        var db = NewDb();
        var clock = ClockAt();
        var user = UserOf(accountType, personId);
        AddCampus(db, Campus1, "Phân hiệu 1");
        AddCampus(db, Campus2, "Phân hiệu 2");
        AddYear(db, Year1, "2026-2027", D(2026, 9, 5), D(2027, 5, 31), current: true);
        AddClass(db, 11, Campus1, Year1, "6A1");
        AddClass(db, 12, Campus1, Year1, "6A2");
        AddClass(db, 21, Campus2, Year1, "6B1");
        db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = 11, StaffId = Teacher, StartDate = D(2026, 9, 5), EndDate = homeroomEnd, CreatedAt = DateTime.UtcNow });
        db.CampusLeadership.AddRange(
            new CampusLeadership { CampusId = Campus1, StaffId = ViceOfCampus1, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow },
            new CampusLeadership { CampusId = Campus2, StaffId = ViceOfCampus2, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        return (db, new ClassService(db, clock, user, new ScopeResolver(db, user, clock)));
    }

    private static async Task<string[]> NamesSeenBy(string accountType, int personId, DateOnly? homeroomEnd = null)
    {
        var (_, svc) = CreateAs(accountType, personId, homeroomEnd);
        return (await svc.ListAsync(null, null, includeInactive: false)).Select(c => c.Name).Order().ToArray();
    }

    [Theory]
    [InlineData(AccountTypes.SchoolAdmin, 900)]
    [InlineData(AccountTypes.Principal, 901)]
    public async Task SchoolAdminAndPrincipal_SeeEveryClass(string accountType, int personId) =>
        Assert.Equal(["6A1", "6A2", "6B1"], await NamesSeenBy(accountType, personId));

    [Fact]
    public async Task VicePrincipal_SeesOnlyClassesOfTheCampusTheyLead()
    {
        Assert.Equal(["6A1", "6A2"], await NamesSeenBy(AccountTypes.VicePrincipal, ViceOfCampus1));
        Assert.Equal(["6B1"], await NamesSeenBy(AccountTypes.VicePrincipal, ViceOfCampus2));
        Assert.Empty(await NamesSeenBy(AccountTypes.VicePrincipal, 999));   // chưa phụ trách phân hiệu nào
    }

    [Fact]
    public async Task Teacher_SeesOnlyTheClassTheyAreHomeroomOf()
    {
        Assert.Equal(["6A1"], await NamesSeenBy(AccountTypes.Teacher, Teacher));
        Assert.Empty(await NamesSeenBy(AccountTypes.Teacher, OtherTeacher));
    }

    [Fact]
    public async Task Teacher_WhoseHomeroomDutyEnded_NoLongerSeesTheClass()
    {
        Assert.Empty(await NamesSeenBy(AccountTypes.Teacher, Teacher, homeroomEnd: Today.AddDays(-1)));
        Assert.Equal(["6A1"], await NamesSeenBy(AccountTypes.Teacher, Teacher, homeroomEnd: Today));   // còn đúng đến hết ngày kết thúc
    }

    [Theory]
    [InlineData(AccountTypes.Student, 100)]
    [InlineData(AccountTypes.Parent, 200)]
    public async Task StudentsAndParents_SeeNoClassList(string accountType, int personId) =>
        Assert.Empty(await NamesSeenBy(accountType, personId));

    [Fact]
    public async Task TheCampusFilter_NeverWidensTheCallersScope()
    {
        var (_, svc) = CreateAs(AccountTypes.VicePrincipal, ViceOfCampus1);

        var asked = await svc.ListAsync(Campus2, null, includeInactive: false);   // xin phân hiệu mình không phụ trách

        Assert.Empty(asked);
    }

    // ---- Phân hiệu: School Admin, Hiệu trưởng, Hiệu phó (SCH-01) ----
    private static async Task<bool> CanReadCampusesAsync(string accountType)
    {
        await using var provider = new ServiceCollection().AddLogging().AddAuthorization(Policies.Register).BuildServiceProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CurrentUser.TypeClaim, accountType)], "Test"));
        return (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, Policies.CampusReader)).Succeeded;
    }

    [Theory]
    [InlineData(AccountTypes.SchoolAdmin, true)]
    [InlineData(AccountTypes.Principal, true)]
    [InlineData(AccountTypes.VicePrincipal, true)]
    [InlineData(AccountTypes.Teacher, false)]
    [InlineData(AccountTypes.Student, false)]
    [InlineData(AccountTypes.Parent, false)]
    public async Task CampusList_IsReadableOnlyByAdminPrincipalAndVicePrincipal(string accountType, bool expected) =>
        Assert.Equal(expected, await CanReadCampusesAsync(accountType));
}
