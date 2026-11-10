using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Lrn;

/// <summary>LRN-04 (4.6.20): học sinh và phụ huynh chỉ xem được trang sách của bài nguồn trong bài mình đã nộp, không duyệt được cả cuốn sách.</summary>
public class SourcePageAccessTests
{
    private const int Assignment = 1, Student = 10, Lesson = 100, OtherLesson = 200;

    private static AccountTestFixture Setup(bool submitted, int? lessonOfQuestion = Lesson)
    {
        var f = new AccountTestFixture();
        f.Db.Question.Add(new Question
        {
            QuestionId = 1, SubjectId = 1, Content = "Câu 1", Status = "Ready", LessonId = lessonOfQuestion, CreatedByPersonId = 1, CreatedAt = f.Clock.Now,
        });
        f.Db.Assignment.Add(new Assignment
        {
            AssignmentId = Assignment, SubjectId = 1, ClassId = 1, Title = "Bài", OpenAt = f.Clock.Now, DueAt = f.Clock.Now.AddDays(1),
            AssignedByPersonId = 1, CreatedAt = f.Clock.Now,
        });
        f.Db.AssignmentQuestion.Add(new AssignmentQuestion { AssignmentQuestionId = 1, AssignmentId = Assignment, QuestionId = 1, OrderNo = 1 });
        f.Db.Submission.Add(new Submission
        {
            SubmissionId = 1, AssignmentId = Assignment, StudentId = Student, StartedAt = f.Clock.Now,
            SubmittedAt = submitted ? f.Clock.Now : null,
        });
        f.Db.SaveChanges();
        return f;
    }

    private static Task Check(AccountTestFixture f, int student, int lesson, int assignment = Assignment) =>
        ResultService.EnsureCanViewSourceAsync(f.Db, assignment, student, lesson);

    [Fact]
    public async Task SubmittedStudent_CanViewSourceLessonOfTheAssignment()
    {
        using var f = Setup(submitted: true);
        await Check(f, Student, Lesson);
    }

    [Fact]
    public async Task StudentWhoHasNotSubmitted_CannotView()
    {
        using var f = Setup(submitted: false);
        await Assert.ThrowsAsync<ApiException>(() => Check(f, Student, Lesson));
    }

    [Fact]
    public async Task LessonThatIsNotTheSourceOfAnyQuestionInTheAssignment_IsRefused()
    {
        using var f = Setup(submitted: true);
        await Assert.ThrowsAsync<ApiException>(() => Check(f, Student, OtherLesson));
    }

    [Fact]
    public async Task AnotherStudent_CannotViewThroughSomeoneElsesSubmission()
    {
        using var f = Setup(submitted: true);
        await Assert.ThrowsAsync<ApiException>(() => Check(f, student: 11, Lesson));
    }

    [Fact]
    public async Task QuestionsWithoutSourceLesson_GiveNoAccessToAnyLesson()
    {
        using var f = Setup(submitted: true, lessonOfQuestion: null);
        await Assert.ThrowsAsync<ApiException>(() => Check(f, Student, Lesson));
    }

    [Fact]
    public async Task UnknownAssignment_IsRefused()
    {
        using var f = Setup(submitted: true);
        await Assert.ThrowsAsync<ApiException>(() => Check(f, Student, Lesson, assignment: 999));
    }
}
