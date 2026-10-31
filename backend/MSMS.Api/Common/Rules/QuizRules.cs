using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Text;

namespace MSMS.Api.Common.Rules;

/// <summary>Một lựa chọn của câu hỏi khi soạn hoặc kiểm tra (BR-20).</summary>
public sealed record OptionInput(string Label, string Content, bool IsCorrect);

/// <summary>
/// Quy tắc câu hỏi trắc nghiệm và chấm điểm dùng chung (BR-20, BR-21): soạn thủ công, nháp AI và chấm tự động
/// cùng đi qua đây, không tự viết lại điều kiện ở nơi khác.
/// </summary>
public static class QuizRules
{
    public static readonly string[] OptionLabels = ["A", "B", "C", "D"];
    public const int MinCorrect = 1;
    public const int MaxCorrect = 3;
    public const int ContentMaxLength = 2000;
    public const int OptionMaxLength = 500;
    public const byte MinGrade = 6;
    public const byte MaxGrade = 9;
    public const int MaxQuestionsPerAssignment = 50;
    public const decimal MaxPoints = 10m;

    /// <summary>Lỗi của bộ lựa chọn, null nếu hợp lệ: đúng 4 lựa chọn A–D, nội dung không trống, từ 1 đến 3 đáp án đúng.</summary>
    public static string? CheckOptions(IReadOnlyList<OptionInput>? options)
    {
        if (options is null || options.Count != OptionLabels.Length)
            return "Câu hỏi phải có đúng 4 lựa chọn A, B, C, D.";
        for (var i = 0; i < OptionLabels.Length; i++)
        {
            if (options[i].Label != OptionLabels[i])
                return "Các lựa chọn phải theo thứ tự A, B, C, D.";
            var content = TextNormalizer.Normalize(options[i].Content);
            if (content.Length == 0) return $"Lựa chọn {OptionLabels[i]} chưa có nội dung. Nhập nội dung rồi lưu lại.";
            if (content.Length > OptionMaxLength) return $"Lựa chọn {OptionLabels[i]} tối đa {OptionMaxLength} ký tự. Rút gọn rồi lưu lại.";
        }
        var correct = options.Count(o => o.IsCorrect);
        if (correct < MinCorrect) return "Chọn ít nhất 1 đáp án đúng.";
        if (correct > MaxCorrect) return $"Một câu có tối đa {MaxCorrect} đáp án đúng.";
        return null;
    }

    /// <summary>Một câu chỉ được điểm khi chọn đúng và đủ các đáp án đúng, không chọn thừa (BR-21).</summary>
    public static bool IsAnswerCorrect(IReadOnlyCollection<int> selectedOptionIds, IReadOnlyCollection<int> correctOptionIds)
    {
        var selected = selectedOptionIds.Distinct().ToList();
        return selected.Count > 0 && selected.Count == correctOptionIds.Count && selected.All(correctOptionIds.Contains);
    }

    /// <summary>Điểm = tổng điểm câu đúng / tổng điểm bài × 10, làm tròn 2 chữ số thập phân; 0,005 làm tròn lên (không làm tròn về số chẵn).</summary>
    public static decimal ScoreOutOfTen(decimal earnedPoints, decimal totalPoints)
    {
        if (totalPoints <= 0) throw ApiException.BadRequest("Tổng điểm của bài phải lớn hơn 0.");
        return Math.Round(earnedPoints / totalPoints * 10m, 2, MidpointRounding.AwayFromZero);
    }
}
