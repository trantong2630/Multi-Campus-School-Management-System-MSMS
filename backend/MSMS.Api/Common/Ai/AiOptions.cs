namespace MSMS.Api.Common.Ai;

/// <summary>Cấu hình nhà cung cấp AI (mục 3.1): khóa, model, địa chỉ gốc và thời gian chờ; nhóm Chat* dành cho MSMS AI của quản lý (ADM-01).</summary>
public sealed class AiOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public int TimeoutSeconds { get; set; } = 90;
    public int MaxOutputTokens { get; set; } = 8192;

    /// <summary>Model riêng cho chat của quản lý; bỏ trống thì dùng <see cref="Model"/>. Tách model để chat không tranh hạn mức với soạn câu hỏi và phân tích bài làm.</summary>
    public string ChatModel { get; set; } = "";

    /// <summary>Số lượt hỏi đáp gần nhất gửi kèm mỗi câu hỏi để AI hiểu câu hỏi nối tiếp.</summary>
    public int ChatHistoryTurns { get; set; } = 20;

    /// <summary>Số vòng gọi tool tối đa cho một câu hỏi; hết vòng thì AI phải trả lời bằng số đã có.</summary>
    public int ChatMaxToolRounds { get; set; } = 4;

    /// <summary>Số câu hỏi tối đa mỗi người trong một phút.</summary>
    public int ChatPerUserPerMinute { get; set; } = 6;

    public string EffectiveChatModel => string.IsNullOrWhiteSpace(ChatModel) ? Model : ChatModel;
}
