using Microsoft.Extensions.Logging.Abstractions;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Tests.Modules.Lrn;

/// <summary>LRN-01 (4.6.5): đọc kết quả AI, giữ câu hợp lệ theo BR-20 và không sập khi AI trả dạng lạ.</summary>
public class QuestionDraftParseTests
{
    private const string Question =
        """{"content":"Số La Mã XIV là số nào?","explanation":"X + IV","options":[{"label":"A","content":"6","isCorrect":false},{"label":"B","content":"14","isCorrect":true},{"label":"C","content":"16","isCorrect":false},{"label":"D","content":"24","isCorrect":false}]}""";

    private static (List<QuestionDraftService.Candidate> Valid, int Discarded) Parse(string raw) =>
        QuestionDraftService.Parse(raw, NullLogger.Instance);

    [Fact]
    public void ObjectWithQuestions_IsRead()
    {
        var (valid, discarded) = Parse($$"""{"questions":[{{Question}}]}""");
        Assert.Single(valid);
        Assert.Equal(0, discarded);
    }

    [Fact]
    public void BareArray_IsReadToo()
    {
        var (valid, discarded) = Parse($"[{Question}]");
        Assert.Single(valid);
        Assert.Equal(0, discarded);
    }

    [Theory]
    [InlineData("\"chỉ là chuỗi\"")]
    [InlineData("42")]
    [InlineData("""{"khac":[]}""")]
    [InlineData("không phải JSON")]
    public void UnexpectedShapes_YieldNothing_WithoutThrowing(string raw)
    {
        var (valid, discarded) = Parse(raw);
        Assert.Empty(valid);
        Assert.Equal(0, discarded);
    }

    [Fact]
    public void QuestionWithoutCorrectOption_IsDiscarded()
    {
        var noCorrect = Question.Replace("\"isCorrect\":true", "\"isCorrect\":false");
        var (valid, discarded) = Parse($"[{Question},{noCorrect}]");
        Assert.Single(valid);
        Assert.Equal(1, discarded);
    }
}
