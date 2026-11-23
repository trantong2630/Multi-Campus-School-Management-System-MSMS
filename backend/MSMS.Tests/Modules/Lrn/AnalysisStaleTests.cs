using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Tests.Modules.Lrn;

/// <summary>BR-23: phân tích chỉ bị coi là kẹt khi đã quá 5 phút kể từ lúc gửi phân tích, không tính từ giờ nộp (bài tự nộp ghi giờ nộp lùi về hạn).</summary>
public class AnalysisStaleTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 22, 0, 0);

    [Fact(DisplayName = "LRN | Phân tích AI | Bài tự nộp: giờ nộp là hạn 2 tiếng trước, vừa gửi phân tích | chưa bị coi là kẹt")]
    public void AutoSubmittedLongAfterDeadline_JustQueued_IsNotStale() =>
        Assert.False(ResultService.IsStale(AnalysisStatuses.Pending, queuedAt: Now.AddSeconds(-10), submittedAt: Now.AddHours(-2), Now));

    [Fact(DisplayName = "LRN | Phân tích AI | Gửi phân tích đã quá 5 phút mà vẫn đang chạy | coi là kẹt")]
    public void QueuedMoreThanFiveMinutesAgo_IsStale() =>
        Assert.True(ResultService.IsStale(AnalysisStatuses.Pending, queuedAt: Now.AddMinutes(-6), submittedAt: Now.AddMinutes(-6), Now));

    [Fact(DisplayName = "LRN | Phân tích AI | Dữ liệu cũ chưa có giờ gửi | tính theo giờ nộp như trước")]
    public void LegacyRowWithoutQueuedAt_FallsBackToSubmittedAt()
    {
        Assert.True(ResultService.IsStale(AnalysisStatuses.Pending, queuedAt: null, submittedAt: Now.AddMinutes(-6), Now));
        Assert.False(ResultService.IsStale(AnalysisStatuses.Pending, queuedAt: null, submittedAt: Now.AddMinutes(-1), Now));
    }

    [Fact(DisplayName = "LRN | Phân tích AI | Đã xong hoặc đã lỗi | không đổi trạng thái")]
    public void FinishedAnalysis_IsNeverStale()
    {
        Assert.False(ResultService.IsStale(AnalysisStatuses.Done, Now.AddHours(-1), Now.AddHours(-1), Now));
        Assert.False(ResultService.IsStale(AnalysisStatuses.Failed, Now.AddHours(-1), Now.AddHours(-1), Now));
    }
}
