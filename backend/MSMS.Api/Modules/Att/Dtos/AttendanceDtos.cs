namespace MSMS.Api.Modules.Att.Dtos;

/// <summary>Số học sinh theo từng nhãn (spec 6.2). Tiết Đã hủy không bao giờ vào thống kê (4.5.14).</summary>
public record AttendanceCountsDto(int Present, int Late, int ExcusedAbsent, int UnexcusedAbsent, int NotMarked);

/// <summary>Một tiết trong danh sách điểm danh (4.5.1, 4.5.6).</summary>
public record AttendanceSessionDto(
    long SessionId,
    DateOnly Date,
    int PeriodSlotId,
    string SessionOfDay,
    int PeriodNo,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int ClassId,
    string ClassName,
    string CampusName,
    int SubjectId,
    string SubjectName,
    string? TeacherName,
    string Status,
    int RosterCount,
    int MarkedCount,
    /// <summary>Tiết Đã xếp lịch, ngày nhỏ hơn hôm nay, chưa có bản ghi nào (4.5.6).</summary>
    bool IsMissing,
    bool CanWrite);

/// <summary>Bảng điểm danh của một tiết (4.5.2).</summary>
public record AttendanceSheetDto(
    AttendanceSessionDto Session,
    /// <summary>School Admin đang nhập bù/sửa: giao diện hiện banner hổ phách (Design_Web §9.3).</summary>
    bool IsAdminMode,
    string? ReadOnlyReason,
    AttendanceCountsDto Counts,
    IReadOnlyList<AttendanceSheetRowDto> Students);

public record AttendanceSheetRowDto(
    int StudentId,
    string StudentCode,
    string FullName,
    /// <summary>Giá trị đã lưu: Present / Late / Absent; null nếu chưa điểm danh.</summary>
    string? Status,
    /// <summary>Nhãn cuối suy ra lúc đọc (BR-18).</summary>
    string Label,
    string? Note,
    bool IsBackfilled,
    string? RecordedByName,
    DateTime? RecordedAt,
    string? UpdatedByName,
    DateTime? UpdatedAt);

public record SaveAttendanceRequest(IReadOnlyList<SaveAttendanceItem> Items);

public record SaveAttendanceItem(int StudentId, string Status, string? Note);

/// <summary>Lớp được chọn trên màn hình điểm danh của School Admin, Hiệu trưởng, Hiệu phó, chủ nhiệm.</summary>
public record AttendanceClassOptionDto(int ClassId, string Name, int CampusId, string CampusName, bool IsHomeroom);

/// <summary>Chuyên cần của một học sinh theo tiết (4.5.11, 4.5.12).</summary>
public record StudentAttendanceDto(
    int StudentId,
    string FullName,
    string StudentCode,
    AttendanceCountsDto Counts,
    IReadOnlyList<StudentAttendanceItemDto> Items);

public record StudentAttendanceItemDto(
    long SessionId,
    DateOnly Date,
    int PeriodSlotId,
    string SessionOfDay,
    int PeriodNo,
    string ClassName,
    int SubjectId,
    string SubjectName,
    string? TeacherName,
    string Label,
    string? Note);

/// <summary>Chuyên cần của lớp theo ngày hoặc khoảng ngày (4.5.13).</summary>
public record ClassAttendanceDto(
    int ClassId,
    string ClassName,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AttendanceSessionDto> Sessions,
    IReadOnlyList<ClassAttendanceStudentDto> Students);

/// <summary>Dữ liệu cho file xuất điểm danh của lớp (ADM-03, 4.9.10): mỗi dòng một học sinh ở một tiết.</summary>
public record AttendanceExportDto(
    string ClassName,
    string CampusName,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AttendanceExportRowDto> Rows);

public record AttendanceExportRowDto(
    DateOnly Date,
    string SessionOfDay,
    int PeriodNo,
    string SubjectName,
    string? TeacherName,
    string StudentCode,
    string FullName,
    /// <summary>Nhãn cuối suy ra lúc đọc (BR-18).</summary>
    string Label,
    string? Note);

public record ClassAttendanceStudentDto(
    int StudentId,
    string StudentCode,
    string FullName,
    AttendanceCountsDto Counts,
    /// <summary>Nhãn theo SessionId; tiết mà học sinh không có ghi danh hiệu lực thì không có khóa.</summary>
    IReadOnlyDictionary<long, string> Labels);
