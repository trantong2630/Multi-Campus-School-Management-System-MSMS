namespace MSMS.Api.Common.Ai;

/// <summary>Cấu hình nhà cung cấp AI (mục 3.1): khóa, model, địa chỉ gốc và thời gian chờ.</summary>
public sealed class AiOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public int TimeoutSeconds { get; set; } = 90;
    public int MaxOutputTokens { get; set; } = 8192;
}
