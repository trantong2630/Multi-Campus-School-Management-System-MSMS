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
    Task<LoginResponse> LoginAsync(LoginRequest request, string? userAgent);
    Task<LoginResponse> RefreshAsync(RefreshRequest request);
    Task LogoutAsync(Guid sessionId);
    Task<UserInfo> GetMeAsync(int accountId);
    Task ChangePasswordAsync(int accountId, Guid? sessionId, ChangePasswordRequest request);
}

public sealed class AuthService(MsmsDbContext db, IClock clock, IOptions<JwtOptions> jwt, SessionService sessions,
    AccountScopeResolver? scope = null) : IAuthService
{
    private readonly AccountScopeResolver _scope = scope ?? new AccountScopeResolver(db, clock);
    private static readonly PasswordHasher<Account> Hasher = new();
    private static readonly Account UnknownAccount = new();
    private static readonly string UnknownPasswordHash = Hasher.HashPassword(UnknownAccount, Guid.NewGuid().ToString("N"));
    private const int MinPasswordLength = 8;

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? userAgent)
    {
        var username = TextNormalizer.Normalize(request.Username);
        var account = await db.Account.Include(a => a.Person)
            .FirstOrDefaultAsync(a => a.Username == username);

        // Luôn kiểm tra một bản băm, kể cả khi không tìm thấy tên đăng nhập.
        var verification = Hasher.VerifyHashedPassword(
            account ?? UnknownAccount, account?.PasswordHash ?? UnknownPasswordHash, request.Password ?? "");

        // Sai thông tin và tài khoản bị khóa cùng trả một lỗi trung tính (BR-01, BR-05).
        if (account is null || verification == PasswordVerificationResult.Failed || account.Status == AccountStatuses.Locked)
            throw ApiException.Unauthorized("Tên đăng nhập hoặc mật khẩu không đúng. Kiểm tra lại rồi thử đăng nhập tiếp.", ErrorCodes.InvalidCredentials);

        account.LastLoginAt = clock.Now;

        // Lưu thời điểm đăng nhập cùng phiên mới trong một lần SaveChanges.
        var (session, refreshToken) = await sessions.CreateAsync(account.AccountId, userAgent);
        return await IssueAsync(account, session.SessionId, refreshToken);
    }

    public async Task<LoginResponse> RefreshAsync(RefreshRequest request)
    {
        var result = await sessions.RefreshAsync(request.RefreshToken);
        if (result.Outcome != RefreshOutcome.Ok)
            throw ApiException.Unauthorized("Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Đăng nhập lại để tiếp tục.");

        var account = await db.Account.Include(a => a.Person).AsNoTracking()
            .FirstAsync(a => a.AccountId == result.Session!.AccountId);
        if (account.Status == AccountStatuses.Locked)
        {
            await sessions.RevokeAsync(result.Session!.SessionId);
            throw ApiException.Unauthorized("Tài khoản đã bị khóa. Liên hệ quản trị để được hỗ trợ.", ErrorCodes.AccountLocked);
        }
        return await IssueAsync(account, result.Session!.SessionId, result.RefreshToken);
    }

    public Task LogoutAsync(Guid sessionId) => sessions.RevokeAsync(sessionId);

    public async Task<UserInfo> GetMeAsync(int accountId)
    {
        var account = await db.Account.Include(a => a.Person).AsNoTracking().FirstAsync(a => a.AccountId == accountId);
        return await ToInfoAsync(account);
    }

    public async Task ChangePasswordAsync(int accountId, Guid? sessionId, ChangePasswordRequest request)
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
        await sessions.RevokeAllAsync(accountId, exceptSessionId: sessionId);   // thiết bị khác phải đăng nhập lại
    }

    public static string HashPassword(Account account, string password) => Hasher.HashPassword(account, password);

    private async Task<LoginResponse> IssueAsync(Account account, Guid sessionId, string? refreshToken)
    {
        var expires = DateTime.UtcNow.AddMinutes(jwt.Value.ExpiryMinutes);
        return new LoginResponse(IssueToken(account, sessionId, expires), expires, refreshToken, await ToInfoAsync(account));
    }

    private string IssueToken(Account account, Guid sessionId, DateTime expires)
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
                new Claim(CurrentUser.SessionIdClaim, sessionId.ToString()),
            ],
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<UserInfo> ToInfoAsync(Account a) => new(
        a.AccountId, a.PersonId, a.Person.PersonCode, a.Person.FullName,
        a.AccountType, a.Status, a.Status == AccountStatuses.PendingPasswordChange, a.Person.AvatarPath)
    {
        // Tính lại từ dữ liệu đang hiệu lực, không đóng băng phạm vi trong JWT hoặc phiên đăng nhập.
        Scope = await _scope.ResolveAsync(a.PersonId, a.AccountType),
    };
}
