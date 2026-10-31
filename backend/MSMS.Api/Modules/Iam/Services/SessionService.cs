using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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
    public async Task<(AccountSession Session, string RefreshToken)> CreateAsync(int accountId, string? userAgent)
    {
        var sessionId = Guid.NewGuid();
        var token = NewToken(sessionId);
        var now = clock.Now;
        var session = new AccountSession
        {
            SessionId = sessionId, AccountId = accountId, RefreshTokenHash = Hash(token),
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
        var signedSessionId = ReadSignedSessionId(refreshToken);

        // Chữ ký cho phép nhận ra cả token đã xoay nhiều lần mà không lưu lịch sử token hay đổi schema.
        // Token ngẫu nhiên của phiên được tạo trước bản cập nhật vẫn được tra bằng bản băm.
        var session = await db.AccountSession.FirstOrDefaultAsync(s =>
            s.SessionId == signedSessionId || s.RefreshTokenHash == hash || s.PreviousRefreshTokenHash == hash);
        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now) return new(RefreshOutcome.Invalid);

        if (session.RefreshTokenHash != hash)
        {
            session.RevokedAt = now;
            await db.SaveChangesAsync();
            return new(RefreshOutcome.Reused);
        }

        var next = NewToken(session.SessionId);
        var nextHash = Hash(next);
        if (db.Database.IsRelational())
        {
            // So sánh và thay nguyên tử: hai request cùng dùng một token không thể cùng xoay thành công.
            var changed = await db.AccountSession.Where(s => s.SessionId == session.SessionId
                    && s.RefreshTokenHash == hash && s.RevokedAt == null && s.ExpiresAt > now)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.PreviousRefreshTokenHash, hash)
                    .SetProperty(s => s.RefreshTokenHash, nextHash).SetProperty(s => s.LastUsedAt, now));
            if (changed == 0)
            {
                await RevokeAsync(session.SessionId);
                return new(RefreshOutcome.Reused);
            }
        }
        session.PreviousRefreshTokenHash = hash;
        session.RefreshTokenHash = nextHash;
        session.LastUsedAt = now;
        if (!db.Database.IsRelational()) await db.SaveChangesAsync();
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

    private string NewToken(Guid sessionId)
    {
        var payload = $"{sessionId:N}.{Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48))}";
        return $"{payload}.{Base64UrlEncoder.Encode(Sign(payload))}";
    }

    private Guid? ReadSignedSessionId(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var sessionId)) return null;
        try
        {
            var signature = Base64UrlEncoder.DecodeBytes(parts[2]);
            return CryptographicOperations.FixedTimeEquals(signature, Sign($"{parts[0]}.{parts[1]}")) ? sessionId : null;
        }
        catch (FormatException) { return null; }
    }

    private byte[] Sign(string payload) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwt.Value.Key),
        Encoding.UTF8.GetBytes($"msms:refresh:{payload}"));

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
