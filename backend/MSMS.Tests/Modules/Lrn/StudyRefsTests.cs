using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Tests.Modules.Lrn;

/// <summary>LRN-04 (4.6.20): mục "Xem lại trong sách" ghép từ bài nguồn của các câu sai, không do AI sinh.</summary>
public class StudyRefsTests
{
    private static readonly TextbookBook Book = new() { BookId = 1, Code = "toan6-t1", Title = "Toán 6 – tập một" };

    private static Dictionary<int, TextbookLesson> Lessons() => new()
    {
        [10] = new TextbookLesson { LessonId = 10, Book = Book, Title = "Bài 2. Cách ghi số tự nhiên", StartPage = 9, EndPage = 12 },
        [20] = new TextbookLesson { LessonId = 20, Book = Book, Title = "Bài tập cuối chương IV", StartPage = 97, EndPage = 97 },
    };

    [Fact]
    public void WrongQuestionsOfSameLesson_AreGroupedWithSortedNumbers()
    {
        var refs = ResultService.BuildStudyRefs([(5, 10), (2, 10), (2, 10)], Lessons());
        var r = Assert.Single(refs);
        Assert.Equal(10, r.LessonId);
        Assert.Equal([2, 5], r.QuestionNos);
        Assert.Equal("Toán 6 – tập một", r.BookTitle);
        Assert.Equal((9, 12), (r.StartPage, r.EndPage));
    }

    [Fact]
    public void Lessons_AreOrderedByTheirEarliestWrongQuestion()
    {
        var refs = ResultService.BuildStudyRefs([(7, 10), (1, 20), (3, 10)], Lessons());
        Assert.Equal([20, 10], refs.Select(r => r.LessonId));
    }

    [Fact]
    public void QuestionsWithoutSourceLesson_AreIgnored()
    {
        Assert.Empty(ResultService.BuildStudyRefs([(1, null), (2, null)], Lessons()));
    }

    [Fact]
    public void UnknownLesson_IsIgnoredRatherThanThrowing()
    {
        Assert.Empty(ResultService.BuildStudyRefs([(1, 999)], Lessons()));
    }

    [Fact]
    public void NoWrongQuestions_GivesNoRefs()
    {
        Assert.Empty(ResultService.BuildStudyRefs([], Lessons()));
    }
}
