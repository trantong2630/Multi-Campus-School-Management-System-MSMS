using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Tests.Modules.Iam;

/// <summary>WBS 4.1.2: a revoked device cannot use access or refresh tokens; another device remains signed in.</summary>
public class LoggedOutSessionTests
{
    private static (DefaultHttpContext Context, CurrentUser User) CreateRequest(Account account, Guid sessionId, string path)
    {
        var claims = new[]
        {
            new Claim(CurrentUser.AccountIdClaim, account.AccountId.ToString()),
            new Claim(CurrentUser.PersonIdClaim, account.PersonId.ToString()),
            new Claim(CurrentUser.TypeClaim, account.AccountType),
            new Claim(CurrentUser.SessionIdClaim, sessionId.ToString()),
        };
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")),
        };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return (context, new CurrentUser(new HttpContextAccessor { HttpContext = context }));
    }

    [Fact]
    public async Task RevokedDevice_AccessAndBothRefreshTokensAreRejected_IndependentDeviceStillWorks()
    {
        using var f = new AuthTestFixture();
        var account = await f.AddAccountAsync();
        var (firstSession, originalRefresh) = await f.Sessions.CreateAsync(account.AccountId, "first-device");
        var rotated = await f.Sessions.RefreshAsync(originalRefresh);
        Assert.Equal(RefreshOutcome.Ok, rotated.Outcome);
        Assert.NotNull(rotated.RefreshToken);
        var (secondSession, secondRefresh) = await f.Sessions.CreateAsync(account.AccountId, "second-device");

        // Seed persisted revocation: ExecuteUpdate itself is covered against SQL Server in API integration verification.
        firstSession.RevokedAt = f.Clock.Now;
        await f.Db.SaveChangesAsync();
        var firstReachedEndpoint = false;
        var firstMiddleware = new AccountStatusMiddleware(_ =>
        {
            firstReachedEndpoint = true;
            return Task.CompletedTask;
        });
        var first = CreateRequest(account, firstSession.SessionId, "/api/auth/me");

        await firstMiddleware.InvokeAsync(first.Context, f.Db, first.User, f.Sessions);

        Assert.False(firstReachedEndpoint);
        Assert.Equal(StatusCodes.Status401Unauthorized, first.Context.Response.StatusCode);
        first.Context.Response.Body.Position = 0;
        using var error = await JsonDocument.ParseAsync(first.Context.Response.Body);
        Assert.Equal(ErrorCodes.Unauthorized, error.RootElement.GetProperty("code").GetString());
        Assert.False(await f.Sessions.IsActiveAsync(firstSession.SessionId, account.AccountId));
        Assert.Equal(RefreshOutcome.Invalid, (await f.Sessions.RefreshAsync(originalRefresh)).Outcome);
        Assert.Equal(RefreshOutcome.Invalid, (await f.Sessions.RefreshAsync(rotated.RefreshToken)).Outcome);

        var secondReachedEndpoint = false;
        var secondMiddleware = new AccountStatusMiddleware(_ =>
        {
            secondReachedEndpoint = true;
            return Task.CompletedTask;
        });
        var second = CreateRequest(account, secondSession.SessionId, "/api/auth/me");
        await secondMiddleware.InvokeAsync(second.Context, f.Db, second.User, f.Sessions);
        Assert.True(secondReachedEndpoint);
        Assert.True(await f.Sessions.IsActiveAsync(secondSession.SessionId, account.AccountId));
        Assert.Equal(RefreshOutcome.Ok, (await f.Sessions.RefreshAsync(secondRefresh)).Outcome);
        Assert.Null(secondSession.RevokedAt);
    }

    [Theory]
    [InlineData(AccountTypes.SchoolAdmin)]
    [InlineData(AccountTypes.Principal)]
    [InlineData(AccountTypes.VicePrincipal)]
    [InlineData(AccountTypes.Teacher)]
    [InlineData(AccountTypes.Student)]
    [InlineData(AccountTypes.Parent)]
    public async Task PendingPasswordChange_AllSixRoles_CanReachLogout(string accountType)
    {
        using var f = new AuthTestFixture();
        var account = await f.AddAccountAsync(accountType, AccountStatuses.PendingPasswordChange);
        var (session, _) = await f.Sessions.CreateAsync(account.AccountId, "pending-device");
        var reachedEndpoint = false;
        var middleware = new AccountStatusMiddleware(_ =>
        {
            reachedEndpoint = true;
            return Task.CompletedTask;
        });
        var request = CreateRequest(account, session.SessionId, "/api/auth/logout");

        await middleware.InvokeAsync(request.Context, f.Db, request.User, f.Sessions);

        Assert.True(reachedEndpoint);
        Assert.Equal(StatusCodes.Status200OK, request.Context.Response.StatusCode);
    }
}
