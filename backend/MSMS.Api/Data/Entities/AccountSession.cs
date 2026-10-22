using System;

namespace MSMS.Api.Data.Entities;

/// <summary>Một phiên đăng nhập trên một thiết bị (BR-05). Refresh token chỉ lưu dạng băm; thu hồi bằng RevokedAt.</summary>
public partial class AccountSession
{
    public Guid SessionId { get; set; }

    public int AccountId { get; set; }

    public string RefreshTokenHash { get; set; } = null!;

    /// <summary>Băm refresh token ngay trước khi xoay vòng; thấy lại nó nghĩa là token cũ bị dùng lại → thu hồi phiên.</summary>
    public string? PreviousRefreshTokenHash { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUsedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
