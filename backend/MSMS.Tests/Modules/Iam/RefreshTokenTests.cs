using Microsoft.EntityFrameworkCore;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Tests.Modules.Iam;

/// <summary>WBS 4.1.1: refresh tokens are stored as hashes and rotate within a seven-day session.</summary>
public class RefreshTokenTests
{
    [Fact]
    public async Task Refresh_CurrentToken_RotatesHashAndKeepsSessionExpiry()
    {
        using var f = new AuthTestFixture();
        var account = await f.AddAccountAsync();
        var (session, token) = await f.Sessions.CreateAsync(account.AccountId, "phone");
        var originalExpiry = session.ExpiresAt;
        f.Clock.Now = f.Clock.Now.AddMinutes(15);

        var result = await f.Sessions.RefreshAsync(token);

        Assert.Equal(RefreshOutcome.Ok, result.Outcome);
        Assert.NotNull(result.RefreshToken);
        Assert.NotEqual(token, result.RefreshToken);
        Assert.Equal(session.SessionId, result.Session!.SessionId);
        Assert.Equal(AuthTestFixture.Hash(token), session.PreviousRefreshTokenHash);
        Assert.Equal(AuthTestFixture.Hash(result.RefreshToken), session.RefreshTokenHash);
        Assert.Equal(f.Clock.Now, session.LastUsedAt);
        Assert.Equal(originalExpiry, session.ExpiresAt);
        Assert.Single(await f.Db.AccountSession.ToListAsync());
        Assert.Null(session.RevokedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unrecognized-token")]
    public async Task Refresh_MissingOrUnknownToken_IsInvalid(string? token)
    {
        using var f = new AuthTestFixture();

        var result = await f.Sessions.RefreshAsync(token);

        Assert.Equal(RefreshOutcome.Invalid, result.Outcome);
        Assert.Null(result.Session);
        Assert.Null(result.RefreshToken);
        Assert.Empty(await f.Db.AccountSession.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Refresh_AtOrAfterSevenDayExpiry_IsInvalidAndDoesNotRotate(int secondsAfterExpiry)
    {
        using var f = new AuthTestFixture();
        var account = await f.AddAccountAsync();
        var (session, token) = await f.Sessions.CreateAsync(account.AccountId, null);
        var originalHash = session.RefreshTokenHash;
        f.Clock.Now = session.ExpiresAt.AddSeconds(secondsAfterExpiry);

        var result = await f.Sessions.RefreshAsync(token);

        Assert.Equal(RefreshOutcome.Invalid, result.Outcome);
        Assert.Null(result.RefreshToken);
        Assert.Equal(originalHash, session.RefreshTokenHash);
        Assert.Null(session.PreviousRefreshTokenHash);
    }
}
