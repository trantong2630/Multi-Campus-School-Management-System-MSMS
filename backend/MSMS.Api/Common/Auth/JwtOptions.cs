namespace MSMS.Api.Common.Auth;

public sealed class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "msms";
    public int ExpiryMinutes { get; set; } = 480;
}
