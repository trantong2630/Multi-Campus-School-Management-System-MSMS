using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Iam.Dtos;

namespace MSMS.Tests.Modules.Iam;

/// <summary>WBS 4.1.1: shared login, neutral failures and device-bound token issuance.</summary>
public class LoginTests
{
    [Theory]
    [InlineData(AccountTypes.SchoolAdmin)]
    [InlineData(AccountTypes.Principal)]
    [InlineData(AccountTypes.VicePrincipal)]
    [InlineData(AccountTypes.Teacher)]
    [InlineData(AccountTypes.Student)]
    [InlineData(AccountTypes.Parent)]
    public async Task Login_AllSixAccountTypes_IssuesTokenForTheAccountAndDevice(string accountType)
    {
        using var f = new AuthTestFixture();
        var account = await f.AddAccountAsync(accountType);

        var earliestExpiry = DateTime.UtcNow.AddMinutes(15);
        var result = await f.Auth.LoginAsync(new LoginRequest(account.Username, "CorrectPassword123"), "test-device");
        var latestExpiry = DateTime.UtcNow.AddMinutes(15);

        var session = await f.Db.AccountSession.SingleAsync();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        Assert.Equal(account.AccountId, result.User.AccountId);
        Assert.Equal(accountType, result.User.AccountType);
        Assert.Equal(account.AccountId.ToString(), jwt.Claims.Single(c => c.Type == CurrentUser.AccountIdClaim).Value);
        Assert.Equal(account.PersonId.ToString(), jwt.Claims.Single(c => c.Type == CurrentUser.PersonIdClaim).Value);
        Assert.Equal(accountType, jwt.Claims.Single(c => c.Type == CurrentUser.TypeClaim).Value);
        Assert.Equal(session.SessionId.ToString(), jwt.Claims.Single(c => c.Type == CurrentUser.SessionIdClaim).Value);
        Assert.Equal("msms-tests", jwt.Issuer);
        Assert.Contains("msms-tests", jwt.Audiences);
        Assert.Equal(f.Clock.Now, account.LastLoginAt);
        Assert.Equal(account.AccountId, session.AccountId);
        Assert.Equal("test-device", session.UserAgent);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.Equal(AuthTestFixture.Hash(result.RefreshToken!), session.RefreshTokenHash);
        Assert.NotEqual(result.RefreshToken, session.RefreshTokenHash);
        Assert.Null(session.PreviousRefreshTokenHash);
        Assert.Null(session.RevokedAt);
        Assert.Equal(f.Clock.Now.AddDays(7), session.ExpiresAt);
        Assert.InRange(result.ExpiresAt, earliestExpiry, latestExpiry);
        Assert.Equal(DateTimeKind.Utc, result.ExpiresAt.Kind);
        Assert.InRange(result.ExpiresAt - jwt.ValidTo, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("unknown", "CorrectPassword123", AccountStatuses.Active)]
    [InlineData("annguyenvan", "wrong-password", AccountStatuses.Active)]
    [InlineData("annguyenvan", "CorrectPassword123", AccountStatuses.Locked)]
    [InlineData("annguyenvan", "wrong-password", AccountStatuses.Locked)]
    public async Task Login_UnknownWrongPasswordAndLocked_HaveIdenticalNeutralFailure(
        string username, string password, string status)
    {
        using var f = new AuthTestFixture();
        var account = await f.AddAccountAsync(status: status);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            f.Auth.LoginAsync(new LoginRequest(username, password), "test-device"));

        Assert.Equal(401, error.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, error.Code);
        Assert.Equal("Tên đăng nhập hoặc mật khẩu không đúng. Kiểm tra lại rồi thử đăng nhập tiếp.", error.Message);
        Assert.Empty(await f.Db.AccountSession.ToListAsync());
        Assert.Null(account.LastLoginAt);
    }

    [Fact]
    public async Task Login_PendingPasswordChange_AuthenticatesAndReportsRequiredChange()
    {
        using var f = new AuthTestFixture();
        var account = await f.AddAccountAsync(status: AccountStatuses.PendingPasswordChange);

        var result = await f.Auth.LoginAsync(new LoginRequest(account.Username, "CorrectPassword123"), null);

        Assert.True(result.User.MustChangePassword);
        Assert.Equal(AccountStatuses.PendingPasswordChange, result.User.Status);
        Assert.Single(await f.Db.AccountSession.ToListAsync());
    }

    [Fact]
    public async Task Login_OnTwoDevices_CreatesIndependentSessions()
    {
        using var f = new AuthTestFixture();
        var account = await f.AddAccountAsync();
        var request = new LoginRequest(account.Username, "CorrectPassword123");

        var first = await f.Auth.LoginAsync(request, "browser");
        var second = await f.Auth.LoginAsync(request, "phone");

        var sessions = await f.Db.AccountSession.OrderBy(s => s.UserAgent).ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.NotEqual(sessions[0].SessionId, sessions[1].SessionId);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.All(sessions, s => Assert.Null(s.RevokedAt));
        Assert.Equal(new[] { "browser", "phone" }, sessions.Select(s => s.UserAgent));
    }
}
