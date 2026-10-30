namespace MSMS.Api.Common.Rules;

/// <summary>
/// Thời hạn của bài tập rèn luyện (BR-21, mục 6.2). Mọi truy vấn nghiệp vụ đi qua đây: làm bài, nộp bài, tự nộp khi hết giờ,
/// danh sách bài, kết quả. Không tự viết lại điều kiện hạn ở nơi khác.
/// </summary>
public static class AssignmentRules
{
    public const int MaxDurationMinutes = 600;

    /// <summary>Dung sai cho độ trễ mạng: bài gửi lên trong khoảng này sau hạn thực tế vẫn nhận, để tự nộp lúc đồng hồ về 0 không bị từ chối.</summary>
    public static readonly TimeSpan SubmitGrace = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Hạn thực tế của một học sinh: mốc sớm hơn giữa hạn nộp và (giờ bắt đầu + thời lượng).
    /// Bài không có thời lượng, hoặc học sinh chưa bấm Bắt đầu, thì hạn thực tế là hạn nộp.
    /// </summary>
    public static DateTime EffectiveDeadline(DateTime dueAt, short? durationMinutes, DateTime? startedAt)
    {
        if (durationMinutes is null || startedAt is null) return dueAt;
        var byClock = startedAt.Value.AddMinutes(durationMinutes.Value);
        return byClock < dueAt ? byClock : dueAt;
    }

    /// <summary>Đã đóng = thời điểm hiện tại > hạn nộp (mục 6.2).</summary>
    public static bool IsClosed(DateTime dueAt, DateTime now) => now > dueAt;

    /// <summary>Quá hạn thực tế, đã tính dung sai: từ lúc này Backend không nhận lưu hay nộp nữa và tự nộp bản nháp.</summary>
    public static bool IsPastDeadline(DateTime deadline, DateTime now) => now > deadline + SubmitGrace;
}
