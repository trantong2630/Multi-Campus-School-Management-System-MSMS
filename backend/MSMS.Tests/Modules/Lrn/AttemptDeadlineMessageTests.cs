using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Tests.Modules.Lrn;

/// <summary>LRN-02 (4.6.12): thông báo khi học sinh không làm được nữa phải nói đúng lý do: hết giờ làm bài hay quá hạn nộp.</summary>
public class AttemptDeadlineMessageTests
{
    private static readonly DateTime Open = new(2026, 11, 10, 8, 0, 0);
    private static readonly DateTime Due = new(2026, 11, 10, 20, 0, 0);

    private static Assignment Assign(short? minutes) => new() { OpenAt = Open, DueAt = Due, DurationMinutes = minutes };

    private static string Message(Assignment a, Submission? s, DateTime now) =>
        Assert.Throws<ApiException>(() => AttemptService.EnsureCanWork(a, s, now)).Message;

    [Fact]
    public void TimedAttempt_TimeLimitReachedBeforeDue_SaysOutOfTime()
    {
        var started = new Submission { StartedAt = new DateTime(2026, 11, 10, 9, 0, 0) };   // 30 phút, hết 09:30, hạn nộp 20:00
        Assert.Contains("hết giờ làm bài", Message(Assign(30), started, new DateTime(2026, 11, 10, 9, 31, 0)));
    }

    [Fact]
    public void TimedAttempt_DueReachedBeforeTimeLimit_SaysPastDue()
    {
        var started = new Submission { StartedAt = new DateTime(2026, 11, 10, 19, 50, 0) };  // 30 phút sẽ hết 20:20, nhưng hạn nộp 20:00
        Assert.Contains("quá hạn nộp", Message(Assign(30), started, new DateTime(2026, 11, 10, 20, 1, 0)));
    }

    [Fact]
    public void TimedAssignmentNeverStarted_PastDue_SaysPastDue()
    {
        Assert.Contains("quá hạn nộp", Message(Assign(30), null, new DateTime(2026, 11, 10, 20, 1, 0)));
    }

    [Fact]
    public void UntimedAssignment_PastDue_SaysPastDue()
    {
        var started = new Submission { StartedAt = new DateTime(2026, 11, 10, 9, 0, 0) };
        Assert.Contains("quá hạn nộp", Message(Assign(null), started, new DateTime(2026, 11, 10, 20, 1, 0)));
    }

    [Fact]
    public void WithinDeadline_DoesNotThrow()
    {
        var started = new Submission { StartedAt = new DateTime(2026, 11, 10, 9, 0, 0) };
        AttemptService.EnsureCanWork(Assign(30), started, new DateTime(2026, 11, 10, 9, 10, 0));
    }
}
