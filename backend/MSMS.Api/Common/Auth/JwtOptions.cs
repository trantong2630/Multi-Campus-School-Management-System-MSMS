namespace MSMS.Api.Common.Auth;

public sealed class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "msms";

    /// <summary>Access token sống ngắn; phiên dài hạn do refresh token giữ (BR-05).</summary>
    public int ExpiryMinutes { get; set; } = 15;

    public int RefreshDays { get; set; } = 7;
}
