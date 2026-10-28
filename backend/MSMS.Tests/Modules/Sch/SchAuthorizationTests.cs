using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;
using MSMS.Api.Modules.Sch;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>
/// Phân quyền SCH, hai tầng (BR-01): tầng 1 là Authorization Policy theo loại tài khoản, tầng 2 là phạm vi dữ liệu do service kiểm.
/// Mỗi loại tài khoản được thử riêng.
/// </summary>
public class SchAuthorizationTests
{
    // ---- Tầng 1: policy ----
    private static async Task<bool> CanAsync(string accountType, string policy)
    {
        await using var provider = new ServiceCollection().AddLogging().AddAuthorization(Policies.Register).BuildServiceProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CurrentUser.TypeClaim, accountType)], "Test"));
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, policy);
        return result.Succeeded;
    }

    [Theory]
    [InlineData(AccountTypes.SchoolAdmin, true)]
    [InlineData(AccountTypes.Principal, false)]
    [InlineData(AccountTypes.VicePrincipal, false)]
    [InlineData(AccountTypes.Teacher, false)]
    [InlineData(AccountTypes.Student, false)]
    [InlineData(AccountTypes.Parent, false)]
    public async Task SchoolAdminPolicy_OnlySchoolAdminPasses(string accountType, bool expected) =>
        Assert.Equal(expected, await CanAsync(accountType, Policies.SchoolAdmin));

    [Theory]
    [InlineData(AccountTypes.SchoolAdmin, true)]
    [InlineData(AccountTypes.Principal, true)]
    [InlineData(AccountTypes.VicePrincipal, true)]
    [InlineData(AccountTypes.Teacher, true)]
    [InlineData(AccountTypes.Student, false)]
    [InlineData(AccountTypes.Parent, false)]
    public async Task StaffPolicy_OnlyStaffAccountsPass(string accountType, bool expected) =>
        Assert.Equal(expected, await CanAsync(accountType, Policies.Staff));

    [Theory]
    [InlineData(AccountTypes.SchoolAdmin, true)]
    [InlineData(AccountTypes.Principal, true)]
    [InlineData(AccountTypes.VicePrincipal, false)]
    [InlineData(AccountTypes.Teacher, false)]
    [InlineData(AccountTypes.Student, false)]
    [InlineData(AccountTypes.Parent, false)]
    public async Task SchoolProfileManagerPolicy_OnlyAdminAndPrincipalPass(string accountType, bool expected) =>
        Assert.Equal(expected, await CanAsync(accountType, Policies.SchoolProfileManager));

    // ---- Mọi endpoint của SCH được gắn đúng policy ----
    private static List<MethodInfo> Endpoints() =>
        typeof(SchModule).Assembly.GetTypes()
            .Where(t => t.Namespace == "MSMS.Api.Modules.Sch.Controllers" && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

    private static bool IsRead(MethodInfo m) => m.GetCustomAttributes<HttpMethodAttribute>().All(a => a.HttpMethods.Contains("GET"));

    [Fact]
    public void EveryEndpoint_HasAnExplicitAuthorizationDecision()
    {
        var undecided = Endpoints()
            .Where(m => m.GetCustomAttribute<AuthorizeAttribute>() is null && m.GetCustomAttribute<AllowAnonymousAttribute>() is null)
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}");

        Assert.Empty(undecided);
    }

    [Fact]
    public void OnlySchoolNameAndLogoAreReadableWithoutLogin()
    {
        var anonymous = Endpoints().Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}").OrderBy(n => n);

        Assert.Equal(["SchoolController.Get", "SchoolController.Logo"], anonymous);
    }

    [Fact]
    public void EveryWriteEndpoint_IsSchoolAdminOnly_ExceptSchoolProfileWhichAlsoAllowsThePrincipal()
    {
        var wrong = new List<string>();
        foreach (var m in Endpoints().Where(m => !IsRead(m)))
        {
            var policy = m.GetCustomAttribute<AuthorizeAttribute>()?.Policy;
            var expected = m.DeclaringType!.Name == "SchoolController" ? Policies.SchoolProfileManager : Policies.SchoolAdmin;
            if (policy != expected) wrong.Add($"{m.DeclaringType.Name}.{m.Name} = {policy ?? "(none)"}");
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public void EveryReadEndpoint_RequiresAStaffAccount_ExceptTheAnonymousSchoolIdentityAndAdminOnlyLookups()
    {
        var wrong = new List<string>();
        foreach (var m in Endpoints().Where(IsRead).Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is null))
        {
            var policy = m.GetCustomAttribute<AuthorizeAttribute>()?.Policy;
            var allowed = m.Name == "Candidates" ? Policies.SchoolAdmin : Policies.Staff;   // danh sách học sinh để ghi danh chỉ School Admin dùng
            if (policy != allowed) wrong.Add($"{m.DeclaringType!.Name}.{m.Name} = {policy ?? "(none)"}");
        }

        Assert.Empty(wrong);
    }

    // ---- Tầng 2: phạm vi đọc danh sách học sinh của lớp (SCH-05) ----
    private const int Campus1 = 1, Campus2 = 2, ClassInCampus1 = 11, HomeroomTeacher = 50, OtherTeacher = 51, ViceOfCampus1 = 70, ViceOfCampus2 = 71;

    private static EnrollmentService CreateRosterService(string accountType, int personId, DateOnly? homeroomEnd = null)
    {
        var db = NewDb();
        var clock = ClockAt();
        var user = UserOf(accountType, personId);
        AddCampus(db, Campus1, "Phân hiệu 1");
        AddCampus(db, Campus2, "Phân hiệu 2");
        AddYear(db, 1, "2026-2027", D(2026, 9, 5), D(2027, 5, 31), current: true);
        AddClass(db, ClassInCampus1, Campus1, 1, "6A1");
        db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = ClassInCampus1, StaffId = HomeroomTeacher, StartDate = D(2026, 9, 5), EndDate = homeroomEnd, CreatedAt = DateTime.UtcNow });
        db.CampusLeadership.AddRange(
            new CampusLeadership { CampusId = Campus1, StaffId = ViceOfCampus1, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow },
            new CampusLeadership { CampusId = Campus2, StaffId = ViceOfCampus2, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        return new EnrollmentService(db, clock, user, new ScopeResolver(db, user, clock), new AuditWriter(db, user, clock),
            new SessionService(db, clock, Options.Create(new JwtOptions())));
    }

    [Theory]
    [InlineData(AccountTypes.SchoolAdmin, 900, true)]
    [InlineData(AccountTypes.Principal, 901, true)]
    [InlineData(AccountTypes.VicePrincipal, ViceOfCampus1, true)]
    [InlineData(AccountTypes.VicePrincipal, ViceOfCampus2, false)]     // hiệu phó phân hiệu khác
    [InlineData(AccountTypes.Teacher, HomeroomTeacher, true)]
    [InlineData(AccountTypes.Teacher, OtherTeacher, false)]            // giáo viên không chủ nhiệm lớp này
    [InlineData(AccountTypes.Student, 100, false)]
    [InlineData(AccountTypes.Parent, 200, false)]
    public async Task Roster_IsReadableOnlyWithinTheCallersScope(string accountType, int personId, bool allowed)
    {
        var svc = CreateRosterService(accountType, personId);

        if (allowed)
        {
            Assert.Equal(ClassInCampus1, (await svc.GetRosterAsync(ClassInCampus1, null)).ClassId);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<ApiException>(() => svc.GetRosterAsync(ClassInCampus1, null));
            Assert.Equal(403, ex.StatusCode);
        }
    }

    [Fact]
    public async Task Roster_AHomeroomTeacherWhoseDutyEnded_LosesAccess_ButStillHasItOnTheEndDate()
    {
        var endedYesterday = CreateRosterService(AccountTypes.Teacher, HomeroomTeacher, homeroomEnd: Today.AddDays(-1));
        var endsToday = CreateRosterService(AccountTypes.Teacher, HomeroomTeacher, homeroomEnd: Today);

        Assert.Equal(403, (await Assert.ThrowsAsync<ApiException>(() => endedYesterday.GetRosterAsync(ClassInCampus1, null))).StatusCode);
        Assert.Equal(ClassInCampus1, (await endsToday.GetRosterAsync(ClassInCampus1, null)).ClassId);
    }
}
