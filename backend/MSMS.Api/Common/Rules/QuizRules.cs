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

    /// <summary>Tổng điểm tối đa của một bài: điểm từng phần cộng lại luôn bằng 10 (BR-21).</summary>
    public const decimal TotalPoints = 10m;

    /// <summary>
    /// Điểm bài = tổng điểm các câu đúng / tổng điểm tối đa của bài × 10, làm tròn 2 chữ số thập phân; 0,005 làm tròn lên
    /// (không làm tròn về số chẵn). Bài cũ mỗi câu 1 điểm nên kết quả vẫn là số câu đúng / số câu × 10.
    /// </summary>
    public static decimal ScoreOutOfTen(decimal earnedPoints, decimal totalPoints)
    {
        if (totalPoints <= 0) throw ApiException.BadRequest("Bài phải có ít nhất một câu hỏi.");
        return Math.Round(earnedPoints / totalPoints * 10m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Chia tổng điểm của một phần cho các câu trong phần theo đơn vị 0,01: các câu bằng nhau, phần dư chia mỗi câu thêm 0,01
    /// từ câu đầu, nên cộng lại luôn đúng bằng tổng điểm của phần.
    /// </summary>
    public static decimal[] SplitPoints(decimal sectionPoints, int questionCount)
    {
        var cents = (int)Math.Round(sectionPoints * 100m);
        var each = cents / questionCount;
        var extra = cents % questionCount;
        return Enumerable.Range(0, questionCount).Select(i => (each + (i < extra ? 1 : 0)) / 100m).ToArray();
    }

    /// <summary>
    /// Điểm từng câu của một đề: nhóm câu theo độ khó (<paramref name="difficultyOf"/>), kiểm điểm các phần rồi chia cho các câu trong phần
    /// theo thứ tự <paramref name="questionIds"/>. Giao bài và tạo bản in đề dùng chung nên hai nơi luôn cho cùng điểm.
    /// </summary>
    public static Dictionary<int, decimal> AssignPoints(
        IReadOnlyList<int> questionIds, IReadOnlyDictionary<int, string> difficultyOf, IReadOnlyList<(string Difficulty, decimal Points)>? sections)
    {
        var groups = questionIds.GroupBy(id => difficultyOf[id]).ToList();
        var sectionPoints = CheckSectionPoints(groups.ToDictionary(g => g.Key, g => g.Count()), sections);
        var result = new Dictionary<int, decimal>();
        foreach (var g in groups)
        {
            var split = SplitPoints(sectionPoints[g.Key], g.Count());
            var index = 0;
            foreach (var id in g) result[id] = split[index++];
        }
        return result;
    }

    /// <summary>
    /// Điểm các phần (mỗi độ khó là một phần) của bài: phần nào có câu thì phải có điểm lớn hơn 0 với tối đa 2 chữ số thập phân,
    /// đủ để mỗi câu được ít nhất 0,01; phần không có câu thì không được ghi điểm; cộng lại đúng 10. Trả điểm theo từng độ khó.
    /// </summary>
    public static Dictionary<string, decimal> CheckSectionPoints(IReadOnlyDictionary<string, int> questionCounts, IReadOnlyList<(string Difficulty, decimal Points)>? sections)
    {
        var result = new Dictionary<string, decimal>();
        foreach (var (difficulty, points) in sections ?? [])
        {
            if (!Difficulties.IsValid(difficulty)) throw ApiException.BadRequest("Phần điểm có độ khó không hợp lệ. Tải lại trang rồi giao lại.");
            if (!result.TryAdd(difficulty, points)) throw ApiException.BadRequest($"Phần {Difficulties.Label(difficulty)} bị ghi điểm hai lần.");
        }
        foreach (var (difficulty, count) in questionCounts)
        {
            if (!result.TryGetValue(difficulty, out var points))
                throw ApiException.BadRequest($"Nhập điểm cho phần {Difficulties.Label(difficulty)} ({count} câu).");
            if (points <= 0 || points != Math.Round(points, 2))
                throw ApiException.BadRequest($"Điểm phần {Difficulties.Label(difficulty)} phải lớn hơn 0 và tối đa 2 chữ số thập phân.");
            if (points * 100m < count)
                throw ApiException.BadRequest($"Phần {Difficulties.Label(difficulty)} có {count} câu nên cần ít nhất {count / 100m:0.##} điểm để mỗi câu được từ 0,01.");
        }
        if (result.Keys.Any(d => !questionCounts.ContainsKey(d)))
            throw ApiException.BadRequest("Có phần điểm không có câu hỏi nào. Bỏ điểm của phần đó.");
        var total = result.Values.Sum();
        if (total != TotalPoints)
            throw ApiException.BadRequest($"Tổng điểm các phần phải bằng {TotalPoints:0}, hiện là {total:0.##}. Điều chỉnh điểm rồi giao lại.");
        return result;
    }
}

/// <summary>Bốn mức độ nhận thức của câu hỏi (BR-20). Lưu dạng chữ; null ở câu hỏi là chưa phân loại.</summary>
public static class Difficulties
{
    public const string Recognize = "Recognize";
    public const string Understand = "Understand";
    public const string Apply = "Apply";
    public const string HighApply = "HighApply";

    /// <summary>Theo thứ tự từ dễ đến khó.</summary>
    public static readonly string[] All = [Recognize, Understand, Apply, HighApply];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);

    public static string Label(string? value) => value switch
    {
        Recognize => "Nhận biết",
        Understand => "Thông hiểu",
        Apply => "Vận dụng",
        HighApply => "Vận dụng cao",
        _ => "Chưa phân loại",
    };

    /// <summary>Thứ tự dễ đến khó để sắp xếp; chưa phân loại xếp cuối.</summary>
    public static int Rank(string? value) => value is null ? int.MaxValue : Array.IndexOf(All, value);
}
