using MSMS.Api.Common.Rules;

namespace MSMS.Tests.Rules;

public class LeaveRequestRulesTests
{
    [Fact]
    public void HasConflict_MorningAndAfternoon_SameDay_ReturnsNoConflict()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 23);
        var existingLeaves = new List<(DateOnly From, DateOnly To, string Session)>
        {
            (date, date, "Morning")
        };

        // Act
        var result = LeaveRequestRules.HasConflict(date, date, "Afternoon", existingLeaves);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasConflict_AllDayAndMorning_SameDay_ReturnsConflict()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 23);
        var existingLeaves = new List<(DateOnly From, DateOnly To, string Session)>
        {
            (date, date, "AllDay")
        };

        // Act
        var result = LeaveRequestRules.HasConflict(date, date, "Morning", existingLeaves);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasConflict_MorningAndAllDay_SameDay_ReturnsConflict()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 23);
        var existingLeaves = new List<(DateOnly From, DateOnly To, string Session)>
        {
            (date, date, "Morning")
        };

        // Act
        var result = LeaveRequestRules.HasConflict(date, date, "AllDay", existingLeaves);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasConflict_SameMorningSession_OverlappingDateRange_ReturnsConflict()
    {
        // Arrange
        var existingLeaves = new List<(DateOnly From, DateOnly To, string Session)>
        {
            (new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 25), "Morning")
        };

        // Act - Đơn mới từ 23-27 giao với đơn cũ 20-25
        var result = LeaveRequestRules.HasConflict(
            new DateOnly(2026, 10, 23),
            new DateOnly(2026, 10, 27),
            "Morning",
            existingLeaves);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasConflict_SameSession_NonOverlappingDateRange_ReturnsNoConflict()
    {
        // Arrange
        var existingLeaves = new List<(DateOnly From, DateOnly To, string Session)>
        {
            (new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 22), "Morning")
        };

        // Act - Đơn mới từ 23-25 không giao với đơn cũ 20-22
        var result = LeaveRequestRules.HasConflict(
            new DateOnly(2026, 10, 23),
            new DateOnly(2026, 10, 25),
            "Morning",
            existingLeaves);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasConflict_EmptyExistingLeaves_ReturnsNoConflict()
    {
        // Arrange
        var existingLeaves = Enumerable.Empty<(DateOnly From, DateOnly To, string Session)>();

        // Act
        var result = LeaveRequestRules.HasConflict(
            new DateOnly(2026, 10, 23),
            new DateOnly(2026, 10, 25),
            "Morning",
            existingLeaves);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasConflict_AllDayAndAllDay_SameDay_ReturnsConflict()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 23);
        var existingLeaves = new List<(DateOnly From, DateOnly To, string Session)>
        {
            (date, date, "AllDay")
        };

        // Act
        var result = LeaveRequestRules.HasConflict(date, date, "AllDay", existingLeaves);

        // Assert
        Assert.True(result);
    }
}