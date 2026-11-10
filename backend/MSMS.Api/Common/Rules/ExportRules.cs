using MSMS.Api.Common.Errors;

namespace MSMS.Api.Common.Rules;

/// <summary>
/// ADM-03 Xuất dữ liệu (spec 5.2): giới hạn khoảng ngày chung cho các mục xuất Excel. Rộng hơn màn hình (62 ngày)
/// để xuất được trọn một học kỳ trong một file.
/// </summary>
public static class ExportRules
{
    public const int MaxRangeDays = 186;

    public static void CheckRange(DateOnly from, DateOnly to)
    {
        if (to < from) throw ApiException.BadRequest("Ngày kết thúc phải sau ngày bắt đầu.");
        if (to.DayNumber - from.DayNumber > MaxRangeDays)
            throw ApiException.BadRequest($"Chỉ xuất tối đa {MaxRangeDays} ngày mỗi lần. Chọn khoảng ngắn hơn.");
    }
}
