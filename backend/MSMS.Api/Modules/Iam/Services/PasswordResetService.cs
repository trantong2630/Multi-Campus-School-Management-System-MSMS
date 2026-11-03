using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Text;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Dtos;

namespace MSMS.Api.Modules.Iam.Services;

/// <summary>IAM-03, 4.1.21–4.1.24: OTP theo tên đăng nhập và email có sẵn trong hồ sơ (BR-04).</summary>
public sealed class PasswordResetService(MsmsDbContext db, IClock clock, IOtpEmailSender email, SessionService sessions)
{
    private static readonly PasswordHasher<PasswordResetOtp> Hasher = new();
    private const string VerifiedPrefix = "verified:";
    private const string InvalidCode = "Mã xác nhận không hợp lệ hoặc đã hết hiệu lực. Kiểm tra mã hoặc gửi lại sau 60 giây.";
    private const string VerificationRequired = "Phiên xác minh không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu mã mới và xác minh lại.";

    public async Task RequestAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var username = TextNormalizer.Normalize(request.Username);
        if (username.Length is 0 or > 100) return;

        PasswordResetOtp otp;
        string code;
        string recipient;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var account = await PasswordResetState.AccountsForUpdate(db).Include(a => a.Person)
                .FirstOrDefaultAsync(a => a.Username == username, ct);
            if (account is null || account.Status != AccountStatuses.Active || string.IsNullOrWhiteSpace(account.Person.Email))
                return;

            var now = clock.Now;
            var recent = await db.PasswordResetOtp.Where(o => o.AccountId == account.AccountId)
                .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.OtpId).Take(11).ToListAsync(ct);
            // Áp dụng cả khi mã trước đã dùng, nhập sai hết lượt hoặc SMTP thất bại.
            if (recent.FirstOrDefault() is { } last && now < last.CreatedAt.AddSeconds(PasswordResetState.ResendSeconds)) return;

            await PasswordResetState.InvalidateAsync(db, account.AccountId, now, ct);
            // Mã mới không trùng các mã gần đây, kể cả mã đã xác minh hoặc đã sử dụng.
            do { code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"); }
            while (recent.Any(previous => Hasher.VerifyHashedPassword(previous, ReadCodeHash(previous.OtpHash), code)
                != PasswordVerificationResult.Failed));
            otp = new PasswordResetOtp
            {
                AccountId = account.AccountId, CreatedAt = now,
                ExpiresAt = now.AddMinutes(PasswordResetState.LifetimeMinutes),
            };
            otp.OtpHash = Hasher.HashPassword(otp, code);
            db.PasswordResetOtp.Add(otp);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            recipient = account.Person.Email.Trim();
        }

        // Không giữ transaction/khóa SQL trong lúc chờ SMTP. Mã mới đã thay mã cũ trước khi gửi.
        var sent = false;
        try { sent = await email.SendAsync(recipient, code, otp.ExpiresAt, ct); }
        finally
        {
            if (!sent)
            {
                var now = clock.Now;
                if (db.Database.IsRelational())
                    await db.PasswordResetOtp.Where(o => o.OtpId == otp.OtpId && o.ConsumedAt == null)
                        .ExecuteUpdateAsync(u => u.SetProperty(o => o.ConsumedAt, now), CancellationToken.None);
                else
                {
                    otp.ConsumedAt = now;
                    await db.SaveChangesAsync(CancellationToken.None);
                }
            }
        }
    }

    public async Task<PasswordResetVerificationResponse> VerifyAsync(VerifyPasswordResetRequest request, CancellationToken ct = default)
    {
        var username = TextNormalizer.Normalize(request.Username);
        if (username.Length is 0 or > 100) throw ApiException.BadRequest(InvalidCode);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var account = await PasswordResetState.AccountsForUpdate(db).FirstOrDefaultAsync(a => a.Username == username, ct);
        var now = clock.Now;
        if (account is null || account.Status != AccountStatuses.Active) throw ApiException.BadRequest(InvalidCode);
        var otp = await db.PasswordResetOtp.Where(o => o.AccountId == account.AccountId && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.OtpId).FirstOrDefaultAsync(ct);
        if (otp is null || otp.ExpiresAt <= now || otp.AttemptCount >= PasswordResetState.MaxAttempts
            || otp.OtpHash.StartsWith(VerifiedPrefix, StringComparison.Ordinal))
            throw ApiException.BadRequest(InvalidCode);

        var code = request.Code ?? "";
        var validFormat = code.Length == 6 && code.All(c => c is >= '0' and <= '9');
        if (!validFormat || Hasher.VerifyHashedPassword(otp, otp.OtpHash, code) == PasswordVerificationResult.Failed)
        {
            otp.AttemptCount++;
            if (otp.AttemptCount >= PasswordResetState.MaxAttempts) otp.ConsumedAt = now;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);   // Lưu lần sai trước khi trả lỗi, không bị rollback bởi exception.
            throw ApiException.BadRequest(InvalidCode);
        }

        // Bằng chứng xác minh ngẫu nhiên 256 bit chỉ giữ trong bộ nhớ trang; DB chỉ giữ bản băm.
        // Giữ bản băm OTP gốc để lần gửi lại không sinh trùng mã. Tất cả vừa cột OtpHash hiện tại (255 ký tự).
        var resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        otp.OtpHash = $"{VerifiedPrefix}{otp.OtpHash}:{HashToken(resetToken)}";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new PasswordResetVerificationResponse(resetToken, otp.ExpiresAt);
    }

    public async Task ResetAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var username = TextNormalizer.Normalize(request.Username);
        if (username.Length is 0 or > 100 || request.ResetToken?.Length != 64)
            throw ApiException.BadRequest(VerificationRequired, "PASSWORD_RESET_VERIFICATION_REQUIRED");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var account = await PasswordResetState.AccountsForUpdate(db).FirstOrDefaultAsync(a => a.Username == username, ct);
        var now = clock.Now;
        if (account is null || account.Status != AccountStatuses.Active)
            throw ApiException.BadRequest(VerificationRequired, "PASSWORD_RESET_VERIFICATION_REQUIRED");

        var otp = await db.PasswordResetOtp.Where(o => o.AccountId == account.AccountId && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.OtpId).FirstOrDefaultAsync(ct);
        var storedHash = otp is null ? null : ReadVerificationHash(otp.OtpHash);
        if (otp is null || otp.ExpiresAt <= now || storedHash is null
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(storedHash), Encoding.ASCII.GetBytes(HashToken(request.ResetToken))))
            throw ApiException.BadRequest(VerificationRequired, "PASSWORD_RESET_VERIFICATION_REQUIRED");

        // Kiểm tra quyền xác minh trước khi kiểm tra hoặc thay đổi mật khẩu.
        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < PasswordResetState.MinPasswordLength)
            throw ApiException.BadRequest($"Mật khẩu mới cần ít nhất {PasswordResetState.MinPasswordLength} ký tự.");
        if (AuthService.MatchesPassword(account, request.NewPassword))
            throw ApiException.BadRequest("Mật khẩu mới trùng mật khẩu hiện tại. Vui lòng chọn mật khẩu khác.", "PASSWORD_UNCHANGED");

        account.PasswordHash = AuthService.HashPassword(account, request.NewPassword);
        account.UpdatedAt = now;
        otp.ConsumedAt = now;
        await sessions.RevokeAllAsync(account.AccountId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string ReadCodeHash(string hash) => hash.StartsWith(VerifiedPrefix, StringComparison.Ordinal)
        ? hash[VerifiedPrefix.Length..hash.LastIndexOf(':')]
        : hash;

    private static string? ReadVerificationHash(string hash) => hash.StartsWith(VerifiedPrefix, StringComparison.Ordinal)
        ? hash[(hash.LastIndexOf(':') + 1)..]
        : null;
}
