using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Text;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Dtos;

namespace MSMS.Api.Modules.Iam.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<UserInfo> GetMeAsync(int accountId);
    Task ChangePasswordAsync(int accountId, ChangePasswordRequest request);
}

public sealed class AuthService(MsmsDbContext db, IClock clock, IOptions<JwtOptions> jwt) : IAuthService
{
    private static readonly PasswordHasher<Account> Hasher = new();
    private const int MinPasswordLength = 8;

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var username = TextNormalizer.Normalize(request.Username);
        var account = await db.Account.Include(a => a.Person)
            .FirstOrDefaultAsync(a => a.Username == username);

        // Không tiết lộ tài khoản có tồn tại hay không (BR-01, BR-05).
        if (account is null || Hasher.VerifyHashedPassword(account, account.PasswordHash, request.Password ?? "") == PasswordVerificationResult.Failed)
            throw ApiException.Unauthorized("Tên đăng nhập hoặc mật khẩu không đúng. Kiểm tra lại rồi thử đăng nhập tiếp.", ErrorCodes.InvalidCredentials);

        if (account.Status == AccountStatuses.Locked)
            throw ApiException.Unauthorized("Tài khoản đã bị khóa. Liên hệ quản trị để được hỗ trợ.", ErrorCodes.AccountLocked);

        account.LastLoginAt = clock.Now;
        await db.SaveChangesAsync();

        var expires = clock.Now.AddMinutes(jwt.Value.ExpiryMinutes);
        return new LoginResponse(IssueToken(account), expires, ToInfo(account));
    }

    public async Task<UserInfo> GetMeAsync(int accountId)
    {
        var account = await db.Account.Include(a => a.Person).AsNoTracking().FirstAsync(a => a.AccountId == accountId);
        return ToInfo(account);
    }

    public async Task ChangePasswordAsync(int accountId, ChangePasswordRequest request)
    {
        var account = await db.Account.FirstAsync(a => a.AccountId == accountId);

        if (Hasher.VerifyHashedPassword(account, account.PasswordHash, request.CurrentPassword ?? "") == PasswordVerificationResult.Failed)
            throw ApiException.BadRequest("Mật khẩu hiện tại không đúng. Nhập lại mật khẩu đang dùng.");
        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < MinPasswordLength)
            throw ApiException.BadRequest($"Mật khẩu mới cần ít nhất {MinPasswordLength} ký tự.");
        if (request.NewPassword == request.CurrentPassword)
            throw ApiException.BadRequest("Mật khẩu mới trùng mật khẩu cũ. Chọn mật khẩu khác.");

        account.PasswordHash = Hasher.HashPassword(account, request.NewPassword);
        account.Status = AccountStatuses.Active;   // đổi lần đầu xong thì Chờ đổi mật khẩu → Đang hoạt động (BR-04)
        account.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
    }

    public static string HashPassword(Account account, string password) => Hasher.HashPassword(account, password);

    private string IssueToken(Account account)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Key));
        var token = new JwtSecurityToken(
            issuer: jwt.Value.Issuer,
            audience: jwt.Value.Issuer,
            claims:
            [
                new Claim(CurrentUser.AccountIdClaim, account.AccountId.ToString()),
                new Claim(CurrentUser.PersonIdClaim, account.PersonId.ToString()),
                new Claim(CurrentUser.TypeClaim, account.AccountType),
            ],
            expires: DateTime.UtcNow.AddMinutes(jwt.Value.ExpiryMinutes),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserInfo ToInfo(Account a) => new(
        a.AccountId, a.PersonId, a.Person.PersonCode, a.Person.FullName,
        a.AccountType, a.Status, a.Status == AccountStatuses.PendingPasswordChange, a.Person.AvatarPath);
}
