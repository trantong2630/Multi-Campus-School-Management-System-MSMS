using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Modules.Lrn.Dtos;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Tests.Modules.Lrn;

/// <summary>Độ khó, điểm theo phần và chấm theo trọng số (BR-20, BR-21).</summary>
public class DifficultyPointsTests
{
    private static Dictionary<string, int> Counts(int recognize = 0, int understand = 0, int apply = 0, int high = 0)
    {
        var d = new Dictionary<string, int>();
        if (recognize > 0) d[Difficulties.Recognize] = recognize;
        if (understand > 0) d[Difficulties.Understand] = understand;
        if (apply > 0) d[Difficulties.Apply] = apply;
        if (high > 0) d[Difficulties.HighApply] = high;
        return d;
    }

    private static List<(string, decimal)> Sections(decimal recognize = 0, decimal understand = 0, decimal apply = 0, decimal high = 0)
    {
        var l = new List<(string, decimal)>();
        if (recognize > 0) l.Add((Difficulties.Recognize, recognize));
        if (understand > 0) l.Add((Difficulties.Understand, understand));
        if (apply > 0) l.Add((Difficulties.Apply, apply));
        if (high > 0) l.Add((Difficulties.HighApply, high));
        return l;
    }

    [Fact]
    public void SplitPoints_SumsExactlyToSectionPoints()
    {
        var split = QuizRules.SplitPoints(3m, 7);
        Assert.Equal(7, split.Length);
        Assert.Equal(3m, split.Sum());
        Assert.True(split.Max() - split.Min() <= 0.01m);
        Assert.All(split, p => Assert.True(p > 0));
    }

    [Fact]
    public void SplitPoints_EvenSplitHasNoRemainder() => Assert.Equal([0.75m, 0.75m, 0.75m, 0.75m], QuizRules.SplitPoints(3m, 4));

    [Fact]
    public void SectionPoints_ValidWhenTheyAddUpToTen()
    {
        var result = QuizRules.CheckSectionPoints(Counts(5, 4, 3, 1), Sections(3, 3, 3, 1));
        Assert.Equal(10m, result.Values.Sum());
    }

    [Fact]
    public void SectionPoints_TotalNotTen_IsRefused()
    {
        Assert.Throws<ApiException>(() => QuizRules.CheckSectionPoints(Counts(5, 4), Sections(3, 3)));
        Assert.Throws<ApiException>(() => QuizRules.CheckSectionPoints(Counts(5, 4), Sections(6, 5)));
    }

    [Fact]
    public void SectionPoints_SectionWithQuestionsButNoPoints_IsRefused() =>
        Assert.Throws<ApiException>(() => QuizRules.CheckSectionPoints(Counts(5, 4), Sections(recognize: 10)));

    [Fact]
    public void SectionPoints_PointsForSectionWithoutQuestions_AreRefused() =>
        Assert.Throws<ApiException>(() => QuizRules.CheckSectionPoints(Counts(5), Sections(recognize: 6, understand: 4)));

    [Fact]
    public void SectionPoints_MoreThanTwoDecimals_AreRefused() =>
        Assert.Throws<ApiException>(() => QuizRules.CheckSectionPoints(Counts(1, 1), Sections(5.005m, 4.995m)));

    [Fact]
    public void SectionPoints_TooFewPointsForTheQuestionCount_AreRefused() =>
        Assert.Throws<ApiException>(() => QuizRules.CheckSectionPoints(Counts(1, 200), Sections(9.995m + 0.005m - 0.01m, 0.01m)));

    [Fact]
    public void SectionPoints_DuplicateOrUnknownDifficulty_IsRefused()
    {
        Assert.Throws<ApiException>(() => QuizRules.CheckSectionPoints(Counts(5), [(Difficulties.Recognize, 5m), (Difficulties.Recognize, 5m)]));
        Assert.Throws<ApiException>(() => QuizRules.CheckSectionPoints(Counts(5), [("Easy", 10m)]));
    }

    [Fact]
    public void Score_WeightedByPoints()
    {
        // Đúng 2 câu khó (mỗi câu 3 điểm) trong bài 10 điểm là 6 điểm, khác với cách chấm cùng trọng số.
        Assert.Equal(6m, QuizRules.ScoreOutOfTen(6m, 10m));
        Assert.Equal(3.33m, QuizRules.ScoreOutOfTen(1m, 3m));
        // Bài cũ mỗi câu 1 điểm: kết quả như cách chấm trước đây.
        Assert.Equal(6.67m, QuizRules.ScoreOutOfTen(2m, 3m));
    }

    [Fact]
    public void Difficulties_OrderEasyToHard_UnclassifiedLast()
    {
        var sorted = new string?[] { null, Difficulties.HighApply, Difficulties.Recognize, Difficulties.Apply }.OrderBy(Difficulties.Rank).ToList();
        Assert.Equal([Difficulties.Recognize, Difficulties.Apply, Difficulties.HighApply, null], sorted);
        Assert.True(Difficulties.IsValid(Difficulties.Understand));
        Assert.False(Difficulties.IsValid("Hard"));
        Assert.False(Difficulties.IsValid(null));
    }

    private const string Q =
        """{"content":"Số La Mã XIV là số nào?","difficulty":"Apply","explanation":"X + IV","options":[{"label":"A","content":"6","isCorrect":false},{"label":"B","content":"14","isCorrect":true},{"label":"C","content":"16","isCorrect":false},{"label":"D","content":"24","isCorrect":false}]}""";

    [Fact]
    public void AiDifficulty_IsReadWhenValid_AndIgnoredWhenNot()
    {
        var (valid, _) = QuestionDraftService.Parse($"[{Q}]", NullLogger.Instance);
        Assert.Equal(Difficulties.Apply, valid[0].Difficulty);
        var (bad, _) = QuestionDraftService.Parse($"[{Q.Replace("\"Apply\"", "\"Easy\"")}]", NullLogger.Instance);
        Assert.Null(bad[0].Difficulty);
    }

    private static QuestionDraftService.Candidate C(string? difficulty) =>
        new("Nội dung", null, [new("A", "a", true), new("B", "b", false), new("C", "c", false), new("D", "d", false)], difficulty);

    [Fact]
    public void AssignDifficulties_KeepsValidAiLevelAndFillsMissingFromRequest()
    {
        var levels = new List<DifficultyCountDto> { new(Difficulties.Recognize, 2), new(Difficulties.Apply, 1) };
        var result = QuestionDraftService.AssignDifficulties([C(Difficulties.Apply), C(null), C("Easy")], levels);
        Assert.Equal([Difficulties.Apply, Difficulties.Recognize, Difficulties.Recognize], result);
    }

    [Fact]
    public void AssignDifficulties_AiAnswersMoreThanRequestedOfOneLevel_KeepsTheHonestLabels()
    {
        // Nhãn phải đúng với câu: không ép câu thừa sang mức khác chỉ để đủ số lượng đã yêu cầu.
        var levels = new List<DifficultyCountDto> { new(Difficulties.Recognize, 1), new(Difficulties.HighApply, 1) };
        var result = QuestionDraftService.AssignDifficulties([C(Difficulties.Recognize), C(Difficulties.Recognize)], levels);
        Assert.Equal([Difficulties.Recognize, Difficulties.Recognize], result);
    }

    private static ExamPaperService.Paper SamplePaper(string? className) => new("Kiểm tra 15 phút - Chương 2", "Toán", className, "Làm bài cẩn thận.", 15,
        [new(1, "Hình nào có bốn góc vuông?", 1.5m, "Hình chữ nhật và hình vuông có bốn góc vuông.",
            [new("A", "Hình thoi", false), new("B", "Hình chữ nhật", true), new("C", "Hình vuông", true), new("D", "Hình thang", false)])]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExamDocx_IsValidWordFileWithAllText(bool withAnswers)
    {
        var bytes = ExamPaperService.RenderDocx(SamplePaper(null), withAnswers);
        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        Assert.Empty(new OpenXmlValidator().Validate(doc));
        var text = doc.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains("Hình chữ nhật", text);
        Assert.Contains("Kiểm tra 15 phút", text);
        Assert.Equal(withAnswers, text.Contains("ĐÁP ÁN VÀ GIẢI THÍCH"));
        Assert.Equal(withAnswers, text.Contains("bốn góc vuông."));
    }

    [Fact]
    public void ExamPaper_WithoutClass_RendersPdf() =>
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(ExamPaperService.Render(SamplePaper(null), true), 0, 4));

    [Fact]
    public void ExamPaper_RendersVietnameseAndAnswerPage()
    {
        var options = new List<ExamPaperService.PaperOption> { new("A", "Hình thoi", false), new("B", "Hình chữ nhật", true), new("C", "Hình vuông", true), new("D", "Hình thang", false) };
        var paper = new ExamPaperService.Paper("Kiểm tra 15 phút - Chương 2", "Toán", "7A", "Làm bài cẩn thận.", 15,
            [new(1, "Hình nào có bốn góc vuông?", 1.5m, "Hình chữ nhật và hình vuông có bốn góc vuông.", options)]);
        var withoutAnswers = ExamPaperService.Render(paper, false);
        var withAnswers = ExamPaperService.Render(paper, true);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(withoutAnswers, 0, 4));
        Assert.True(withAnswers.Length > withoutAnswers.Length);
    }
}
