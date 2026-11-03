using MSMS.Api.Common.Auth;

namespace MSMS.Api.Common.Rules;

/// <summary>Ba giá trị giáo viên gửi và được lưu (spec 6.1, BR-17).</summary>
public static class AttendanceStatuses
{
    public const string Present = "Present";
    public const string Late = "Late";
    public const string Absent = "Absent";

    public static readonly string[] All = [Present, Late, Absent];
}

/// <summary>Nhãn cuối hiển thị, luôn tính lúc đọc (spec 6.2): bốn nhãn và "Chưa điểm danh".</summary>
public static class AttendanceLabels
{
    public const string Present = "Present";
    public const string Late = "Late";
    public const string ExcusedAbsent = "ExcusedAbsent";
    public const string UnexcusedAbsent = "UnexcusedAbsent";
    public const string NotMarked = "NotMarked";
}

/// <summary>Đơn nghỉ đã duyệt, rút gọn đủ để suy nhãn có phép (BR-18).</summary>
public sealed record ApprovedLeave(int StudentId, DateOnly FromDate, DateOnly ToDate, string SessionOfDay, string? SlotIds);

/// <summary>
/// Quy tắc điểm danh dùng chung (BR-17, BR-18). Mọi màn hình và thống kê điểm danh đi qua đây, không tự viết lại điều kiện (spec 6.2).
/// </summary>
public static class AttendanceRules
{
    public const string SessionScheduled = "Scheduled";
    public const string LeaveApproved = "Approved";
    private const string AllDay = "AllDay";

    /// <summary>
    /// Giáo viên của tiết ghi hoặc sửa được: tiết Đã xếp lịch, đúng ngày của tiết (tới 23:59, giờ VN) — BR-17.
    /// Dựa vào tiết chứ không dựa vào phụ trách chuyên môn nên giáo viên dạy thay ghi được.
    /// </summary>
    public static bool TeacherCanWrite(string sessionStatus, int? sessionTeacherId, DateOnly sessionDate, int personId, DateOnly today) =>
        sessionStatus == SessionScheduled && sessionTeacherId == personId && sessionDate == today;

    /// <summary>School Admin nhập bù hoặc sửa: tiết Đã xếp lịch có ngày ≤ hôm nay (BR-02, BR-17).</summary>
    public static bool AdminCanWrite(string sessionStatus, DateOnly sessionDate, DateOnly today) =>
        sessionStatus == SessionScheduled && sessionDate <= today;

    /// <summary>
    /// Lý do không ghi được, null nếu ghi được. Trả câu nêu nguyên nhân kèm cách xử lý (Conventions §4.6).
    /// </summary>
    public static string? WriteBlockReason(string accountType, string sessionStatus, int? sessionTeacherId, DateOnly sessionDate, int personId, DateOnly today)
    {
        if (sessionStatus == "Pending") return "Tiết đang Chờ xử lý, chưa có giáo viên nên chưa điểm danh được. Hiệu phó cần xếp giáo viên dạy thay trước.";
        if (sessionStatus != SessionScheduled) return "Tiết đã hủy nên không điểm danh.";
        if (accountType == AccountTypes.SchoolAdmin)
            return sessionDate > today ? "Tiết chưa diễn ra. Chỉ nhập bù cho tiết có ngày từ hôm nay trở về trước." : null;
        if (accountType != AccountTypes.Teacher) return "Chỉ xem số liệu, không sửa điểm danh.";
        if (sessionTeacherId != personId) return "Chỉ giáo viên của tiết mới điểm danh được tiết này.";
        if (sessionDate > today) return "Tiết chưa tới ngày. Điểm danh vào đúng ngày của tiết.";
        if (sessionDate < today) return "Tiết đã qua ngày, giáo viên không sửa được nữa. Liên hệ quản trị hệ thống nếu cần sửa.";
        return null;
    }

    /// <summary>
    /// BR-18: Vắng + có đơn Đã duyệt phủ ngày của tiết và đúng buổi ("cả ngày" = mọi tiết) → có phép.
    /// Đơn theo tiết (PAR-01, SlotIds = số tiết trong buổi) chỉ phủ các tiết đã chọn; đơn để trống tiết phủ cả buổi.
    /// </summary>
    public static bool IsExcused(int studentId, DateOnly date, string sessionOfDay, int periodNo, IEnumerable<ApprovedLeave> leaves) =>
        leaves.Any(l => l.StudentId == studentId
            && l.FromDate <= date && l.ToDate >= date
            && (l.SessionOfDay == AllDay || l.SessionOfDay == sessionOfDay)
            && (string.IsNullOrEmpty(l.SlotIds) || l.SlotIds.Split(',').Contains(periodNo.ToString())));

    /// <summary>Nhãn cuối của một học sinh ở một tiết: không có bản ghi → Chưa điểm danh.</summary>
    public static string Label(string? status, bool excused) => status switch
    {
        null => AttendanceLabels.NotMarked,
        AttendanceStatuses.Present => AttendanceLabels.Present,
        AttendanceStatuses.Late => AttendanceLabels.Late,
        _ => excused ? AttendanceLabels.ExcusedAbsent : AttendanceLabels.UnexcusedAbsent,
    };
}
