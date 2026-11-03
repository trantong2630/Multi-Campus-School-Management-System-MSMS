using Microsoft.EntityFrameworkCore;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Modules.Iam.Services;

/// <summary>IAM-03, BR-04: quy tắc OTP dùng chung cho khôi phục và cấp lại mật khẩu.</summary>
internal static class PasswordResetState
{
    public const int LifetimeMinutes = 10;
    public const int ResendSeconds = 60;
    public const int MaxAttempts = 5;
    public const int MinPasswordLength = 8;

    // Giữ khóa cập nhật đến khi transaction kết thúc: gửi lại, xác nhận và admin reset
    // cùng khóa một tài khoản, kể cả khi backend chạy nhiều instance. Không đổi schema.
    public static IQueryable<Account> AccountsForUpdate(MsmsDbContext db) => db.Database.IsSqlServer()
        ? db.Account.FromSqlRaw("SELECT * FROM [Account] WITH (UPDLOCK, HOLDLOCK)")
        : db.Account;

    public static async Task InvalidateAsync(MsmsDbContext db, int accountId, DateTime now,
        CancellationToken ct = default)
    {
        var query = db.PasswordResetOtp.Where(o => o.AccountId == accountId && o.ConsumedAt == null);
        if (db.Database.IsRelational())
            await query.ExecuteUpdateAsync(u => u.SetProperty(o => o.ConsumedAt, now), ct);
        else
            foreach (var otp in await query.ToListAsync(ct)) otp.ConsumedAt = now;
    }
}
