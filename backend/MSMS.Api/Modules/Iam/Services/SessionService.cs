using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Modules.Iam.Services;

public enum RefreshOutcome { Ok, Invalid, Reused }

public record RefreshResult(RefreshOutcome Outcome, AccountSession? Session = null, string? RefreshToken = null);

/// <summary>Phiên đăng nhập theo thiết bị (BR-05): tạo, xoay vòng refresh token, thu hồi một phiên hoặc mọi phiên của tài khoản.</summary>
public sealed class SessionService(MsmsDbContext db, IClock clock, IOptions<JwtOptions> jwt)
{
    private static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(10);

    public async Task<(AccountSession Session, string RefreshToken)> CreateAsync(int accountId, string? userAgent)
    {
        var token = NewToken();
        var now = clock.Now;
        var session = new AccountSession
        {
            SessionId = Guid.NewGuid(), AccountId = accountId, RefreshTokenHash = Hash(token),
            UserAgent = userAgent?.Length > 300 ? userAgent[..300] : userAgent,
            CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddDays(jwt.Value.RefreshDays),
        };
        db.AccountSession.Add(session);
        await db.SaveChangesAsync();
        return (session, token);
    }

    /// <summary>Đổi refresh token lấy cặp mới. Token cũ bị dùng lại → thu hồi cả phiên (nghi bị đánh cắp).</summary>
    public async Task<RefreshResult> RefreshAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return new(RefreshOutcome.Invalid);
        var hash = Hash(refreshToken);
        var now = clock.Now;

        var session = await db.AccountSession.FirstOrDefaultAsync(s =>
            s.RefreshTokenHash == hash || s.PreviousRefreshTokenHash == hash);
        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now) return new(RefreshOutcome.Invalid);

        if (session.RefreshTokenHash != hash)
        {
            // Hai request refresh gần như cùng lúc (nhiều tab, F5): token cũ vẫn hợp lệ trong vài giây,
            // chỉ cấp access token mới, không xoay vòng lần nữa để cookie vừa đặt không bị vô hiệu.
            if (now - session.LastUsedAt <= RotationGrace) return new(RefreshOutcome.Ok, session, null);

            session.RevokedAt = now;
            await db.SaveChangesAsync();
            return new(RefreshOutcome.Reused);
        }

        var next = NewToken();
        session.PreviousRefreshTokenHash = hash;
        session.RefreshTokenHash = Hash(next);
        session.LastUsedAt = now;
        await db.SaveChangesAsync();
        return new(RefreshOutcome.Ok, session, next);
    }

    public async Task<bool> IsActiveAsync(Guid sessionId, int accountId)
    {
        var now = clock.Now;
        return await db.AccountSession.AsNoTracking().AnyAsync(s =>
            s.SessionId == sessionId && s.AccountId == accountId && s.RevokedAt == null && s.ExpiresAt > now);
    }

    public async Task RevokeAsync(Guid sessionId)
    {
        var now = clock.Now;
        await db.AccountSession.Where(s => s.SessionId == sessionId && s.RevokedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.RevokedAt, now));
    }

    /// <summary>Khóa, reset mật khẩu, đổi loại, đổi mật khẩu: hủy mọi phiên của tài khoản (trừ phiên đang dùng nếu có).</summary>
    public async Task RevokeAllAsync(int accountId, Guid? exceptSessionId = null)
    {
        var now = clock.Now;
        await db.AccountSession.Where(s => s.AccountId == accountId && s.RevokedAt == null && s.SessionId != exceptSessionId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.RevokedAt, now));
    }

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
