using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Iam.Controllers;

namespace MSMS.Tests.Authorization;

public class IamPolicyTests
{
    [Theory]
    [InlineData(AccountTypes.SchoolAdmin)]
    [InlineData(AccountTypes.Principal)]
    [InlineData(AccountTypes.VicePrincipal)]
    [InlineData(AccountTypes.Teacher)]
    [InlineData(AccountTypes.Student)]
    [InlineData(AccountTypes.Parent)]
    public async Task EachRole_AccountManagementAndRoleBoundActions_HaveIndependentPolicies(string role)
    {
        await using var provider = new ServiceCollection().AddLogging().AddAuthorization(Policies.Register).BuildServiceProvider();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CurrentUser.TypeClaim, role)], "Test"));
        var expectations = new Dictionary<string, bool>
        {
            [Policies.SchoolAdmin] = role == AccountTypes.SchoolAdmin,
            [Policies.Teacher] = role == AccountTypes.Teacher,
            [Policies.Student] = role == AccountTypes.Student,
            [Policies.Parent] = role == AccountTypes.Parent,
            [Policies.ConversationParticipant] = role is AccountTypes.Parent or AccountTypes.Teacher,
            [Policies.ReportViewer] = role is AccountTypes.Teacher or AccountTypes.Student or AccountTypes.Parent,
            [Policies.TimetableManager] = role == AccountTypes.VicePrincipal,
            [Policies.DashboardReader] = role is AccountTypes.Principal or AccountTypes.VicePrincipal,
            [Policies.AssignmentOversight] = role is AccountTypes.Principal or AccountTypes.VicePrincipal or AccountTypes.Teacher,
        };
        foreach (var (policy, expected) in expectations)
            Assert.Equal(expected, (await auth.AuthorizeAsync(principal, policy)).Succeeded);
    }

    [Fact]
    public void AccountEndpoints_IncludingDirectoryAndSheet_AreSchoolAdminOnly_NoPasswordReadEndpoint()
    {
        var controller = typeof(AccountsController);
        Assert.Equal(Policies.SchoolAdmin, controller.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>());
        foreach (var method in controller.GetMethods().Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any()))
        {
            Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.DoesNotContain(method.GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy != Policies.SchoolAdmin);
        }
        var readNames = controller.GetMethods().Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any(a => a.HttpMethods.Contains("GET")))
            .Select(m => m.Name).OrderBy(x => x);
        Assert.Equal(new[] { "Directory", "List", "LockImpact" }, readNames);
    }
}
