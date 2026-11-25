using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Time;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Data;

/// <summary>
/// Tài khoản School Admin đầu tiên cho môi trường thật: mọi chức năng tạo hồ sơ và tài khoản đều cần School Admin đăng nhập,
/// nên cơ sở dữ liệu trống thì không ai vào được. Chỉ chạy khi chưa có tài khoản nào VÀ đã đặt Seed:AdminUsername, Seed:AdminPassword
/// (biến môi trường Seed__AdminUsername, Seed__AdminPassword hoặc user-secrets; không ghi vào file cấu hình trong mã nguồn).
/// Tài khoản ở trạng thái Chờ đổi mật khẩu, nên mật khẩu cấu hình chỉ dùng được cho lần đăng nhập đầu.
/// Chỉ tạo Person và Account, không tạo hồ sơ cán bộ vì hồ sơ cần một phân hiệu mà lúc này chưa có.
/// </summary>
public static class AdminSeeder
{
    public const string UsernameKey = "Seed:AdminUsername";
    public const string PasswordKey = "Seed:AdminPassword";
    public const string FullName = "Quản trị hệ thống";

    /// <param name="nextPersonNumber">Số kế tiếp của dãy mã hồ sơ; mặc định lấy từ dbo.seq_PersonCode (chỉ có trên SQL Server).</param>
    public static async Task RunAsync(MsmsDbContext db, IConfiguration config, IClock clock, ILogger logger,
        Func<MsmsDbContext, Task<int>>? nextPersonNumber = null)
    {
        if (await db.Account.AnyAsync()) return;

        var username = config[UsernameKey]?.Trim().ToLowerInvariant();
        var password = config[PasswordKey];
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            logger.LogWarning("Chưa có tài khoản nào và chưa đặt {UsernameKey}, {PasswordKey}: chưa ai đăng nhập được.", UsernameKey, PasswordKey);
            return;
        }
        if (password.Length < PasswordResetState.MinPasswordLength)
            throw new InvalidOperationException($"{PasswordKey} cần ít nhất {PasswordResetState.MinPasswordLength} ký tự.");

        var number = await (nextPersonNumber ?? DefaultNextPersonNumber)(db);
        var person = new Person { PersonType = "Staff", PersonCode = $"CB{number}", FullName = FullName, CreatedAt = clock.Now };
        var account = new Account
        {
            Person = person, Username = username, AccountType = AccountTypes.SchoolAdmin,
            Status = AccountStatuses.PendingPasswordChange, CreatedAt = clock.Now, PasswordHash = "",
        };
        account.PasswordHash = AuthService.HashPassword(account, password);
        db.Person.Add(person);
        db.Account.Add(account);
        await db.SaveChangesAsync();
        logger.LogInformation("Seed: đã tạo School Admin {Username}, đổi mật khẩu ở lần đăng nhập đầu.", username);
    }

    // NEXT VALUE FOR không dùng được trong subquery nên không gọi thêm toán tử LINQ nào sau SqlQuery.
    private static async Task<int> DefaultNextPersonNumber(MsmsDbContext db) =>
        (await db.Database.SqlQuery<int>($"SELECT NEXT VALUE FOR dbo.seq_PersonCode AS Value").ToListAsync())[0];
}
