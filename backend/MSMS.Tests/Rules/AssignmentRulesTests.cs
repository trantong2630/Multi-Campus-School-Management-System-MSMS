using MSMS.Api.Common.Rules;

namespace MSMS.Tests.Rules;

/// <summary>Hạn thực tế, dung sai nộp bài và chấm điểm của bài tập rèn luyện (BR-21).</summary>
public class AssignmentRulesTests
{
    private static readonly DateTime Due = new(2026, 11, 1, 21, 0, 0);

    [Fact]
    public void EffectiveDeadline_NoDuration_IsDueDate()
    {
        Assert.Equal(Due, AssignmentRules.EffectiveDeadline(Due, null, new DateTime(2026, 11, 1, 10, 0, 0)));
    }

    [Fact]
    public void EffectiveDeadline_NotStarted_IsDueDate()
    {
        Assert.Equal(Due, AssignmentRules.EffectiveDeadline(Due, 30, null));
    }

    [Fact]
    public void EffectiveDeadline_StartPlusDuration_WhenEarlierThanDue()
    {
        var start = new DateTime(2026, 11, 1, 10, 0, 0);
        Assert.Equal(start.AddMinutes(30), AssignmentRules.EffectiveDeadline(Due, 30, start));
    }

    [Fact]
    public void EffectiveDeadline_CappedByDueDate_WhenStartedLate()
    {
        var start = new DateTime(2026, 11, 1, 20, 50, 0);
        Assert.Equal(Due, AssignmentRules.EffectiveDeadline(Due, 30, start));
    }

    [Fact]
    public void IsPastDeadline_AllowsNetworkGrace()
    {
        Assert.False(AssignmentRules.IsPastDeadline(Due, Due));
        Assert.False(AssignmentRules.IsPastDeadline(Due, Due.AddSeconds(10)));
        Assert.True(AssignmentRules.IsPastDeadline(Due, Due.AddSeconds(11)));
    }

    [Fact]
    public void IsClosed_OnlyAfterDueDate()
    {
        Assert.False(AssignmentRules.IsClosed(Due, Due));
        Assert.True(AssignmentRules.IsClosed(Due, Due.AddSeconds(1)));
    }
}

public class QuizRulesGradingTests
{
    [Fact]
    public void IsAnswerCorrect_RequiresExactSet()
    {
        Assert.True(QuizRules.IsAnswerCorrect([1, 2], [2, 1]));
        Assert.False(QuizRules.IsAnswerCorrect([1], [1, 2]));
        Assert.False(QuizRules.IsAnswerCorrect([1, 2, 3], [1, 2]));
        Assert.False(QuizRules.IsAnswerCorrect([], [1]));
    }

    [Fact]
    public void ScoreOutOfTen_RoundsHalfUpToTwoDecimals()
    {
        Assert.Equal(3.33m, QuizRules.ScoreOutOfTen(1m, 3m));
        Assert.Equal(6.67m, QuizRules.ScoreOutOfTen(2m, 3m));
        Assert.Equal(10m, QuizRules.ScoreOutOfTen(5m, 5m));
        Assert.Equal(0m, QuizRules.ScoreOutOfTen(0m, 4m));
    }

    [Fact]
    public void ScoreOutOfTen_ZeroTotal_Throws()
    {
        Assert.ThrowsAny<Exception>(() => QuizRules.ScoreOutOfTen(0m, 0m));
    }
}
