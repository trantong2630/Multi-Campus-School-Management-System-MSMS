using MSMS.Api.Common.Rules;

namespace MSMS.Tests.Rules;

public class LeaveRequestRulesTests
{
    /// <summary>
    /// Testcase 1 (BR-19): Xin nghỉ Sáng và Chiều cùng 1 ngày bằng 2 đơn riêng biệt là HỢP LỆ.
    /// Kịch bản: Đã có đơn xin nghỉ buổi Sáng ngày 28/10, phụ huynh gửi tiếp đơn xin nghỉ buổi Chiều ngày 28/10.
    /// Kỳ vọng: Không xung đột (Returns false).
    /// </summary>
    [Fact]
    public void HasConflict_MorningAndAfternoon_SameDay_ReturnsNoConflict()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 28);
        var existingLeaves = new List<(DateOnly From, DateOnly To, string Session)>
        {
            (date, date, "Morning")
        };

        // Act
        var result = LeaveRequestRules.HasConflict(date, date, "Afternoon", existingLeaves);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Testcase 2 (BR-19): Đã có đơn Cả ngày thì không được xin thêm buổi Sáng cùng ngày.
    /// Kịch bản: Đã có đơn nghỉ "AllDay" ngày 28/10, phụ huynh gửi thêm đơn xin nghỉ "Morning" ngày 28/10.
    /// Kỳ vọng: Bị xung đột (Returns true) vì AllDay bao trùm cả Morning.
    /// </summary>
    [Fact]
    public void HasConflict_AllDayAndMorning_SameDay_ReturnsConflict()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 28);
        var existingLeaves = new List<(DateOnly From, DateOnly To, string Session)>
        {
            (date, date, "AllDay")
        };

        // Act
        var result = LeaveRequestRules.HasConflict(date, date, "Morning", existingLeaves);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Testcase 3 (BR-19): Đã có đơn buổi Sáng thì không được xin thêm đơn Cả ngày cùng ngày.
    /// Kịch bản: Đã có đơn nghỉ "Morning" ngày 28/10, phụ huynh gửi thêm đơn xin nghỉ "AllDay" ngày 28/10.
    /// Kỳ vọng: Bị xung đột (Returns true) vì AllDay chồng lấn lên buổi Morning đã có.
    /// </summary>
    [Fact]
    public void HasConflict_MorningAndAllDay_SameDay_ReturnsConflict()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 28);
        var existingLeaves = new List<(DateOnly From, DateOnly To, string Session)>
        {
            (date, date, "Morning")
        };

        // Act
        var result = LeaveRequestRules.HasConflict(date, date, "AllDay", existingLeaves);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Testcase 4 (BR-19): Hai đơn cùng buổi (Sáng) có khoảng thời gian giao nhau thì bị xung đột.
    /// Kịch bản: Đã có đơn nghỉ buổi Sáng từ ngày 26/10 đến 29/10. Gửi tiếp đơn nghỉ buổi Sáng từ 28/10 đến 31/10 (giao nhau đoạn 28-29/10).
    /// Kỳ vọng: Bị xung đột (Returns true) vì trùng cả ngày lẫn buổi.
    /// </summary>
    [Fact]
    public void HasConflict_SameMorningSession_OverlappingDateRange_ReturnsConflict()
    {
        // Arrange
        var existingLeaves = new List<(DateOnly From, DateOnly To, string Session)>
        {
            (new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 29), "Morning")
        };

        // Act - Đơn mới từ 28-31 giao với đơn cũ 26-29
        var result = LeaveRequestRules.HasConflict(
            new DateOnly(2026, 10, 28),
            new DateOnly(2026, 10, 31),
            "Morning",
            existingLeaves);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Testcase 5 (BR-19): Hai đơn cùng buổi nhưng các khoảng ngày hoàn toàn tách biệt thì hợp lệ.
    /// Kịch bản: Đã có đơn nghỉ buổi Sáng từ ngày 25/10 đến 27/10. Gửi tiếp đơn nghỉ buổi Sáng từ ngày 28/10 đến 30/10 (không giao ngày).
    /// Kỳ vọng: Không xung đột (Returns false).
    /// </summary>
    [Fact]
    public void HasConflict_SameSession_NonOverlappingDateRange_ReturnsNoConflict()
    {
        // Arrange
        var existingLeaves = new List<(DateOnly From, DateOnly To, string Session)>
        {
            (new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 27), "Morning")
        };

        // Act - Đơn mới từ 28-30 không giao với đơn cũ 25-27
        var result = LeaveRequestRules.HasConflict(
            new DateOnly(2026, 10, 28),
            new DateOnly(2026, 10, 30),
            "Morning",
            existingLeaves);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Testcase 6 (BR-19): Khi học sinh chưa từng có đơn xin nghỉ nào trước đó thì đơn mới luôn hợp lệ.
    /// Kịch bản: Danh sách đơn hiện có rỗng (Empty), phụ huynh gửi đơn xin nghỉ từ 28/10 đến 30/10.
    /// Kỳ vọng: Không xung đột (Returns false).
    /// </summary>
    [Fact]
    public void HasConflict_EmptyExistingLeaves_ReturnsNoConflict()
    {
        // Arrange
        var existingLeaves = Enumerable.Empty<(DateOnly From, DateOnly To, string Session)>();

        // Act
        var result = LeaveRequestRules.HasConflict(
            new DateOnly(2026, 10, 28),
            new DateOnly(2026, 10, 30),
            "Morning",
            existingLeaves);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Testcase 7 (BR-19): Hai đơn cùng xin nghỉ Cả ngày trên cùng 1 ngày thì bị xung đột.
    /// Kịch bản: Đã có đơn nghỉ "AllDay" ngày 28/10, phụ huynh gửi thêm đơn nghỉ "AllDay" ngày 28/10.
    /// Kỳ vọng: Bị xung đột (Returns true).
    /// </summary>
    [Fact]
    public void HasConflict_AllDayAndAllDay_SameDay_ReturnsConflict()
    {
        // Arrange
        var date = new DateOnly(2026, 10, 28);
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
