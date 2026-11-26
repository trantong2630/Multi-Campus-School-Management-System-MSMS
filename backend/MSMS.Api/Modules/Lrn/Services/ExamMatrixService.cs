using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Text;
using MSMS.Api.Data;
using MSMS.Api.Modules.Lrn.Dtos;

namespace MSMS.Api.Modules.Lrn.Services;

/// <summary>
/// Dựng đề theo ma trận bài × độ khó (BR-20): đếm câu Sẵn dùng của ngân hàng theo chủ đề và độ khó, rồi bốc ngẫu nhiên số câu
/// giáo viên điền ở từng ô. Chỉ đọc ngân hàng, không ghi gì: câu bốc được chỉ là gợi ý để giáo viên xem lại rồi mới giao bài.
/// </summary>
public sealed class ExamMatrixService(MsmsDbContext db, CurrentUser user)
{
    /// <summary>Câu người khác dùng được: Sẵn dùng, không ẩn, đã có độ khó và chủ đề, đúng môn và khối.</summary>
    private IQueryable<Data.Entities.Question> Usable(int subjectId, byte gradeLevel) => db.Question.AsNoTracking().Where(q =>
        q.SubjectId == subjectId && q.GradeLevel == gradeLevel && q.Status == QuestionStatuses.Ready && !q.IsHidden
        && q.Difficulty != null && q.Topic != null);

    public async Task<IReadOnlyList<MatrixRowDto>> MatrixAsync(int subjectId, byte gradeLevel)
    {
        CheckGrade(gradeLevel);
        var groups = await Usable(subjectId, gradeLevel)
            .GroupBy(q => new { q.Topic, q.Difficulty })
            .Select(g => new { g.Key.Topic, g.Key.Difficulty, Count = g.Count() })
            .ToListAsync();
        // Gộp theo chủ đề không phân biệt hoa thường, phòng khi CSDL so sánh có phân biệt.
        var rows = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in groups)
        {
            var topic = TextNormalizer.Normalize(g.Topic);
            if (topic.Length == 0) continue;
            var counts = rows.TryGetValue(topic, out var c) ? c : rows[topic] = new int[Difficulties.All.Length];
            counts[Array.IndexOf(Difficulties.All, g.Difficulty)] += g.Count;
        }
        // Thứ tự bài trong sách (tập rồi thứ tự trong tập) cho chủ đề trùng tên bài; chủ đề tự gõ không có bài thì xếp sau, theo thứ tự tự nhiên.
        var lessons = await db.TextbookLesson.AsNoTracking()
            .Where(l => l.Book.SubjectId == subjectId && l.Book.GradeLevel == gradeLevel)
            .Select(l => new { l.Title, l.Book.Volume, l.SortOrder }).ToListAsync();
        var bookOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in lessons) bookOrder.TryAdd(TextNormalizer.Normalize(l.Title), l.Volume * 10000 + l.SortOrder);
        return rows.OrderBy(r => bookOrder.TryGetValue(r.Key, out var o) ? o : int.MaxValue)
            .ThenBy(r => r.Key, NaturalComparer.Instance)
            .Select(r => new MatrixRowDto(r.Key, r.Value[0], r.Value[1], r.Value[2], r.Value[3])).ToList();
    }

    /// <summary>So sánh chữ có số theo giá trị số ("Bài 2" trước "Bài 10"), không phân biệt hoa thường.</summary>
    internal sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            x ??= ""; y ??= "";
            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    var si = i; var sj = j;
                    while (i < x.Length && char.IsDigit(x[i])) i++;
                    while (j < y.Length && char.IsDigit(y[j])) j++;
                    var a = x[si..i].TrimStart('0'); var b = y[sj..j].TrimStart('0');
                    var byLength = a.Length.CompareTo(b.Length);
                    if (byLength != 0) return byLength;
                    var byDigits = string.CompareOrdinal(a, b);
                    if (byDigits != 0) return byDigits;
                }
                else
                {
                    var c = string.Compare(x[i].ToString(), y[j].ToString(), StringComparison.CurrentCultureIgnoreCase);
                    if (c != 0) return c;
                    i++; j++;
                }
            }
            return (x.Length - i).CompareTo(y.Length - j);
        }
    }

    public async Task<PickQuestionsResultDto> PickAsync(PickQuestionsDto dto)
    {
        CheckGrade(dto.GradeLevel);
        var cells = (dto.Cells ?? []).Where(c => c.Count > 0).ToList();
        if (cells.Count == 0) throw ApiException.BadRequest("Điền số câu vào ít nhất một ô của ma trận.");
        if (cells.Sum(c => c.Count) > QuizRules.MaxQuestionsPerAssignment)
            throw ApiException.BadRequest($"Một bài tối đa {QuizRules.MaxQuestionsPerAssignment} câu hỏi. Giảm số câu trong ma trận.");
        var seen = new HashSet<(string, string)>();
        foreach (var c in cells)
        {
            if (!Difficulties.IsValid(c.Difficulty) || TextNormalizer.Normalize(c.Topic).Length == 0)
                throw ApiException.BadRequest("Ma trận có ô không hợp lệ. Tải lại trang rồi thử lại.");
            if (!seen.Add((TextNormalizer.Normalize(c.Topic).ToLowerInvariant(), c.Difficulty)))
                throw ApiException.BadRequest("Ma trận có ô bị lặp. Tải lại trang rồi thử lại.");
        }

        var picked = new List<int>();
        var shortages = new List<PickShortageDto>();
        foreach (var c in cells)
        {
            var topic = TextNormalizer.Normalize(c.Topic);
            var ids = await Usable(dto.SubjectId, dto.GradeLevel).Where(q => q.Topic == topic && q.Difficulty == c.Difficulty)
                .Select(q => q.QuestionId).ToListAsync();
            var chosen = ids.OrderBy(_ => Random.Shared.Next()).Take(c.Count).ToList();
            if (chosen.Count < c.Count) shortages.Add(new PickShortageDto(topic, c.Difficulty, c.Count, ids.Count));
            picked.AddRange(chosen);
        }

        var items = await QuestionService.ProjectItems(db.Question.AsNoTracking().Where(q => picked.Contains(q.QuestionId)), user.PersonId).ToListAsync();
        // Dễ đến khó, trong cùng độ khó thì theo chủ đề: đề đọc từ dễ đến khó.
        var ordered = items.OrderBy(i => Difficulties.Rank(i.Difficulty)).ThenBy(i => i.Topic, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(i => i.QuestionId).ToList();
        return new PickQuestionsResultDto(ordered, shortages);
    }

    private static void CheckGrade(byte gradeLevel)
    {
        if (gradeLevel is < QuizRules.MinGrade or > QuizRules.MaxGrade) throw ApiException.BadRequest("Khối phải từ 6 đến 9. Chọn lại khối.");
    }
}
