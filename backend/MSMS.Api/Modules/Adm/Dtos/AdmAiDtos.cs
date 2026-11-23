namespace MSMS.Api.Modules.Adm.Dtos;

/// <summary>Một lượt trong hội thoại trình duyệt gửi kèm: Role là "user" hoặc "ai"; câu của AI gửi lại đúng HistoryText đã nhận.</summary>
public record AiChatTurnDto(string Role, string Text);

/// <summary>Câu hỏi cho MSMS AI. Question có thể chứa mã HS#id, GV#id do ô chọn @ chèn vào. Hội thoại không lưu ở server.</summary>
public record AiChatRequest(string Question, List<AiChatTurnDto>? History);

public record AiChartRowDto(string Label, double? Value);

/// <summary>Biểu đồ do Backend dựng từ đúng số của một kết quả tool: Kind "bar" hoặc "line"; Unit "%" hoặc ""; Threshold là ngưỡng cảnh báo nếu có.</summary>
public record AiChartDto(string Title, string Kind, string Unit, double? Threshold, bool HigherIsBetter, double? Max, List<AiChartRowDto> Rows);

/// <summary>
/// Câu trả lời: Text đã đổi mã thành tên để hiển thị; HistoryText là bản còn mã giả, trình duyệt gửi lại ở các câu sau.
/// Steps là các nhóm số liệu MSMS AI đã xem.
/// </summary>
public record AiChatResponse(string Text, string HistoryText, List<AiChartDto> Charts, List<string> Steps);

/// <summary>Gợi ý người cho ô chọn @ của MSMS AI: Code là mã gửi đi, Name và Detail chỉ để hiển thị.</summary>
public record AiPersonDto(string Code, string Name, string Detail);
