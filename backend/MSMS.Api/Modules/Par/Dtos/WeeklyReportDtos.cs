namespace MSMS.Api.Modules.Par.Dtos;

/// <summary>Một tuần (Thứ 2 - Chủ nhật) hợp lệ để xem sổ liên lạc của học sinh (BR-24).</summary>
public record ReportWeekDto(DateOnly WeekStart, DateOnly WeekEnd, int WeekNumber, string Label, bool IsCurrent);

/// <summary>5 chỉ số chuyên cần trong tuần, tính on-read từ tiết đã diễn ra (BR-24).</summary>
public record AttendanceSummaryDto(
    int TotalSessions,
    int PresentCount,
    int LateCount,
    int ExcusedCount,
    int UnexcusedCount,
    int UnmarkedCount);

/// <summary>Một bài tập có hạn nộp trong tuần, cùng trạng thái và kết quả (BR-21, BR-22, BR-23).</summary>
public record WeeklyAssignmentItemDto(
    int AssignmentId,
    string Title,
    string SubjectName,
    DateTime DueAt,
    string Status,
    decimal? Score,
    string? AiFeedback);

/// <summary>4 chỉ số thống kê bài tập trong tuần và danh sách chi tiết.</summary>
public record AssignmentSummaryDto(
    int AssignedCount,
    int SubmittedCount,
    int UnsubmittedCount,
    int MissedCount,
    IReadOnlyList<WeeklyAssignmentItemDto> Items);

/// <summary>Sổ liên lạc điện tử của một học sinh trong một tuần, tính trực tiếp khi mở (BR-24).</summary>
public record WeeklyReportDto(
    int StudentId,
    string StudentName,
    string StudentCode,
    string ClassName,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    AttendanceSummaryDto Attendance,
    AssignmentSummaryDto Assignments);

/// <summary>Học sinh thuộc lớp GVCN đang chủ nhiệm hiệu lực, dùng để chọn học sinh xem sổ liên lạc.</summary>
public record HomeroomStudentItemDto(int StudentId, string StudentCode, string FullName, string ClassName);
