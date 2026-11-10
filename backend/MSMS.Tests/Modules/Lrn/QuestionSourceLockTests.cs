using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Tests.Modules.Lrn;

/// <summary>LRN-01 (4.6.6): câu AI soạn từ một bài trong sách giữ môn và khối của bài đó; câu soạn tay thì không bị ràng buộc.</summary>
public class QuestionSourceLockTests
{
    private static Question Q(int? lessonId) => new() { SubjectId = 1, GradeLevel = 6, LessonId = lessonId };

    [Fact]
    public void AiQuestionFromBook_ChangingGrade_IsRejected() =>
        Assert.Throws<ApiException>(() => QuestionService.EnsureSourceFieldsUnchanged(Q(5), 1, 7));

    [Fact]
    public void AiQuestionFromBook_ChangingSubject_IsRejected() =>
        Assert.Throws<ApiException>(() => QuestionService.EnsureSourceFieldsUnchanged(Q(5), 2, 6));

    [Fact]
    public void AiQuestionFromBook_SameSubjectAndGrade_IsAllowed() =>
        QuestionService.EnsureSourceFieldsUnchanged(Q(5), 1, 6);

    [Fact]
    public void ManualQuestion_CanChangeSubjectAndGrade() =>
        QuestionService.EnsureSourceFieldsUnchanged(Q(null), 2, 9);
}
