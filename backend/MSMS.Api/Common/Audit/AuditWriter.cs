using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Common.Audit;

/// <summary>Nhóm sự kiện của nhật ký. Giá trị là chữ tiếng Việt, lưu thẳng vào bản ghi (tối đa 30 ký tự).</summary>
public static class AuditCategories
{
    public const string Account = "Tài khoản";
    public const string TimetablePublish = "Phát hành thời khóa biểu";
    public const string ScheduleAdjust = "Điều chỉnh lịch";
    public const string Responsibility = "Trách nhiệm";
    public const string Attendance = "Điểm danh";

    public static readonly string[] All = [Account, TimetablePublish, ScheduleAdjust, Responsibility, Attendance];
}

/// <summary>Hành động ghi trong nhật ký. Giá trị là chữ tiếng Việt, lưu thẳng vào bản ghi (tối đa 50 ký tự).</summary>
public static class AuditActions
{
    public const string CreateAccount = "Tạo tài khoản";
    public const string BulkCreateAccounts = "Tạo tài khoản hàng loạt";
    public const string LockAccount = "Khóa tài khoản";
    public const string BulkLockAccounts = "Khóa tài khoản hàng loạt";
    public const string UnlockAccount = "Mở khóa tài khoản";
    public const string ResetPassword = "Cấp lại mật khẩu tạm";
    public const string ChangeAccountType = "Đổi loại tài khoản";

    public const string PublishTimetable = "Phát hành thời khóa biểu";

    public const string ChangeSessionTeacher = "Đổi giáo viên của tiết";
    public const string CancelSession = "Hủy tiết";
    public const string CancelSessionsForHoliday = "Hủy tiết do ngày nghỉ";
    public const string AddMakeUpSession = "Thêm tiết bù";
    public const string DeclareTeacherAbsence = "Khai báo giáo viên vắng";
    public const string DeleteTeacherAbsence = "Xóa khai báo giáo viên vắng";
    public const string ResolvePendingSubstitute = "Xử lý tiết chờ: dạy thay";
    public const string ResolvePendingCancel = "Xử lý tiết chờ: hủy tiết";
    public const string ProposeSession = "Tổ trưởng đề xuất xử lý tiết chờ";
    public const string ApproveAbsence = "Hiệu phó duyệt khai báo vắng";

    public const string AssignResponsibility = "Gán trách nhiệm";
    public const string ReplaceResponsibility = "Thay người đảm nhiệm";
    public const string EndResponsibility = "Kết thúc trách nhiệm";
    public const string CancelResponsibility = "Hủy lịch hẹn trách nhiệm";

    public const string CorrectAttendance = "Nhập bù hoặc sửa điểm danh";
}

/// <summary>Nhãn tiếng Việt cho các mã dùng trong nội dung nhật ký, để bản ghi không chứa mã tiếng Anh.</summary>
public static class AuditLabels
{
    public static string AccountType(string? type) => type switch
    {
        AccountTypes.SchoolAdmin => "Quản trị hệ thống",
        AccountTypes.Principal => "Hiệu trưởng",
        AccountTypes.VicePrincipal => "Hiệu phó",
        AccountTypes.Teacher => "Giáo viên",
        AccountTypes.Student => "Học sinh",
        AccountTypes.Parent => "Phụ huynh",
        _ => type ?? "",
    };

    public static string CloseReason(string? reason) => reason switch
    {
        EnrollmentCloseReasons.ClassTransfer => "Chuyển lớp",
        EnrollmentCloseReasons.SchoolTransfer => "Chuyển trường",
        EnrollmentCloseReasons.Graduated => "Tốt nghiệp",
        _ => reason ?? "",
    };

    public static string Attendance(string? status) => status switch
    {
        AttendanceStatuses.Present => "Có mặt",
        AttendanceStatuses.Late => "Muộn",
        AttendanceStatuses.Absent => "Vắng",
        _ => status ?? "",
    };
}

/// <summary>
/// Chỉ ghi thêm (BR-28): không có hàm sửa hay xóa. Nhóm, hành động, tên trường nội dung và giá trị đều ghi bằng tiếng Việt.
/// Hàm Add không gọi SaveChanges để bản ghi audit nằm cùng transaction với thao tác nghiệp vụ.
/// </summary>
public sealed class AuditWriter(MsmsDbContext db, CurrentUser user, IClock clock)
{
    // Giữ chữ có dấu nguyên dạng trong cột Detail thay vì \uXXXX.
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    /// <summary>Nội dung là các cặp (tên trường tiếng Việt, giá trị). Ngày được ghi dạng dd/MM/yyyy.</summary>
    public void Add(string category, string action, IEnumerable<int>? targetIds = null, params (string Key, object? Value)[] detail) =>
        AddLong(category, action, targetIds?.Select(i => (long)i), detail);

    /// <summary>Đích là tiết học (khóa bigint).</summary>
    public void AddLong(string category, string action, IEnumerable<long>? targetIds, params (string Key, object? Value)[] detail)
    {
        db.AuditLog.Add(new AuditLog
        {
            Category = category,
            Action = action,
            ActorPersonId = user.PersonId,
            TargetIds = targetIds is null ? null : JsonSerializer.Serialize(targetIds),
            Detail = detail.Length == 0 ? null : JsonSerializer.Serialize(detail.ToDictionary(d => d.Key, d => Normalize(d.Value)), Json),
            CreatedAt = clock.Now,
        });
    }

    private static object? Normalize(object? value) => value switch
    {
        DateOnly d => d.ToString("dd/MM/yyyy"),
        IEnumerable<DateOnly> ds => ds.Select(d => d.ToString("dd/MM/yyyy")).ToList(),
        _ => value,
    };
}
