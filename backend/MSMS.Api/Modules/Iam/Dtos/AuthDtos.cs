namespace MSMS.Api.Modules.Iam.Dtos;

public record LoginRequest(string Username, string Password);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UserInfo(
    int AccountId, int PersonId, string PersonCode, string FullName,
    string AccountType, string Status, bool MustChangePassword, string? AvatarPath);

/// <summary>Web gửi refresh token qua cookie nên body có thể trống; mobile gửi trong body.</summary>
public record RefreshRequest(string? RefreshToken);

public record LoginResponse(string Token, DateTime ExpiresAt, string? RefreshToken, UserInfo User);
