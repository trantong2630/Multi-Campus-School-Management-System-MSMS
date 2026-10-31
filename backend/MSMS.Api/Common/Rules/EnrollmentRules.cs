using System.Linq.Expressions;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Common.Rules;

/// <summary>Lý do kết thúc ghi danh (SCH-05, 4.2.32).</summary>
public static class EnrollmentCloseReasons
{
    public const string ClassTransfer = "ClassTransfer";
    public const string SchoolTransfer = "SchoolTransfer";
    public const string Graduated = "Graduated";

    public static readonly string[] All = [ClassTransfer, SchoolTransfer, Graduated];
}

/// <summary>
/// Quy tắc ghi danh dùng chung (BR-09): danh sách học sinh hiệu lực tại một ngày và kiểm khoảng ghi danh không chồng nhau.
/// Điểm danh, giao bài và sổ liên lạc lấy danh sách lớp qua đây, không tự viết lại điều kiện.
/// </summary>
public static class EnrollmentRules
{
    /// <summary>
    /// Ghi danh hiệu lực tại ngày D: ngày bắt đầu ≤ D và ngày kết thúc ≥ D, trong đó ngày kết thúc để trống nghĩa là
    /// tới hết năm học của lớp (BR-09). Khác `EffectivePeriod.EffectiveAt` ở chỗ có chặn theo cuối năm học.
    /// </summary>
    public static Expression<Func<Enrollment, bool>> ActiveAt(DateOnly date) =>
        e => e.StartDate <= date && (e.EndDate ?? e.Class.AcademicYear.EndDate) >= date;

    /// <summary>
    /// Ngày dùng để xem sĩ số và danh sách mặc định của một lớp: hôm nay, hoặc ngày bắt đầu năm học nếu năm học của lớp chưa bắt đầu
    /// (ghi danh của năm sau chỉ có hiệu lực từ ngày đó nên xem theo hôm nay sẽ thấy lớp trống).
    /// </summary>
    public static DateOnly DefaultViewDate(DateOnly today, DateOnly yearStart) => today < yearStart ? yearStart : today;

    /// <summary>Giống <see cref="ActiveAt"/> nhưng ngày xét là <see cref="DefaultViewDate"/> của năm học của lớp. Dùng cho sĩ số hiển thị.</summary>
    public static Expression<Func<Enrollment, bool>> ActiveAsOf(DateOnly today) => e =>
        e.StartDate <= (today < e.Class.AcademicYear.StartDate ? e.Class.AcademicYear.StartDate : today)
        && (e.EndDate ?? e.Class.AcademicYear.EndDate) >= (today < e.Class.AcademicYear.StartDate ? e.Class.AcademicYear.StartDate : today);

    /// <summary>
    /// Ghi danh chưa kết thúc tính đến hôm nay: đang hiệu lực hoặc sắp bắt đầu (ngày kết thúc, hoặc cuối năm học nếu để trống, chưa qua).
    /// Dùng để chặn ngừng hoạt động lớp và để đóng hết ghi danh còn lại khi học sinh rời trường.
    /// </summary>
    public static Expression<Func<Enrollment, bool>> OpenOrUpcoming(DateOnly today) =>
        e => (e.EndDate ?? e.Class.AcademicYear.EndDate) >= today;

    /// <summary>Học sinh đang học của lớp tại ngày D (xem <see cref="ActiveAt"/>).</summary>
    public static IQueryable<Enrollment> RosterAt(this IQueryable<Enrollment> enrollments, int classId, DateOnly date) =>
        enrollments.Where(e => e.ClassId == classId).Where(ActiveAt(date));

    /// <summary>Ngày kết thúc thực tế của ghi danh: để trống nghĩa là tới hết năm học.</summary>
    public static DateOnly EffectiveEnd(DateOnly? endDate, DateOnly yearEnd) => endDate ?? yearEnd;

    /// <summary>Hai khoảng [startA, endA] và [startB, endB] có chung ít nhất một ngày không (đầu mút tính cả hai phía).</summary>
    public static bool Overlaps(DateOnly startA, DateOnly endA, DateOnly startB, DateOnly endB) =>
        startA <= endB && startB <= endA;
}
