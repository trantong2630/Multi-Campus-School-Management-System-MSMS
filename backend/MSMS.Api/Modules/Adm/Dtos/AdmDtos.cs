namespace MSMS.Api.Modules.Adm.Dtos;

// ---------- Dùng chung giữa các màn Tình hình ----------

public sealed record NamedId(int Id, string Name);

/// <summary>
/// Một chỉ số chính: giá trị 4 tuần gần nhất (28 ngày tính tới hôm nay), giá trị 4 tuần liền trước để so sánh,
/// và 8 giá trị theo tuần (tuần bắt đầu thứ Hai, tuần cuối là tuần này). Null khi chưa có dữ liệu để tính.
/// </summary>
public sealed record Kpi(double? Value, double? Previous, List<double?> Weeks);

/// <summary>
/// Một tiết bị ảnh hưởng: tiết của giáo viên vắng (đã dạy thay, đã hủy, hoặc chưa xử lý) hoặc tiết đã hủy vì lý do khác.
/// Outcome: Substituted, Cancelled, Pending. AbsentTeacher trống khi tiết hủy không do giáo viên vắng.
/// </summary>
public sealed record AffectedSessionRow(
    long SessionId, DateOnly Date, string SessionOfDay, int PeriodNo, TimeOnly StartTime,
    string ClassName, string SubjectName, string? AbsentTeacher, string? Teacher, string Outcome);

/// <summary>Cơ cấu lượt điểm danh trong 4 tuần: có mặt, muộn, vắng có phép, vắng không phép.</summary>
public sealed record AttendanceMix(int Present, int Late, int AbsentExcused, int AbsentUnexcused);

/// <summary>Một tuần của tiết bị ảnh hưởng: số giáo viên vắng có tiết bị ảnh hưởng, và số tiết theo kết quả xử lý.</summary>
public sealed record AbsenceWeek(DateOnly WeekStart, int Teachers, int Substituted, int Cancelled, int Pending);

/// <summary>
/// Tổng quan bài rèn luyện: Given là bài mở trong 4 tuần gần nhất, Open là bài đang mở, DueSoon là bài hết hạn trong 7 ngày tới,
/// LowCount là bài đã hết hạn trong 4 tuần mà dưới 50% người nhận nộp.
/// </summary>
public sealed record AssignmentSummary(int Given, int Open, int DueSoon, int LowCount);

/// <summary>Một nhóm (khối, buổi…) với tỷ lệ trong 4 tuần gần nhất.</summary>
public sealed record RateRow(int Id, string Name, double? Rate);

// ---------- Hiệu trưởng, hiệu phó (ADM-01) ----------

/// <summary>
/// Việc cần xử lý trong phạm vi. StreakStudents: học sinh vắng mọi tiết từ 3 ngày học liên tiếp trở lên;
/// UncoveredSessions: tiết Chờ xử lý chưa diễn ra; UnmarkedSessions: tiết đã qua chưa điểm danh trong 4 tuần;
/// ClassesWithoutHomeroom chỉ có với Hiệu trưởng (người phân công chủ nhiệm), trống với Hiệu phó.
/// </summary>
public sealed record LeadershipAlerts(int StreakStudents, int UncoveredSessions, int UnmarkedSessions, int? ClassesWithoutHomeroom);

/// <summary>Một lớp trong phạm vi, số liệu 4 tuần gần nhất; PreviousRate là chuyên cần 4 tuần liền trước.</summary>
public sealed record LeadershipClassRow(
    int ClassId, string ClassName, int GradeLevel, string CampusName, string? Homeroom, int StudentCount,
    double? Rate, double? PreviousRate, double? UnexcusedRate, int StreakStudents, int UnmarkedSessions, double? SubmissionRate);

/// <summary>
/// Một tổ chuyên môn trong 4 tuần gần nhất: số thầy cô của tổ (theo phân hiệu chính trong phạm vi), số thầy cô có ngày vắng,
/// tiết bị ảnh hưởng và tiết chưa điểm danh của các môn trong tổ, bài rèn luyện đã giao và tỷ lệ nộp của bài đã hết hạn.
/// </summary>
public sealed record LeadershipDepartmentRow(
    int DepartmentId, string Name, int TeacherCount, int AbsentTeachers, int AffectedSessions, int UnmarkedSessions,
    int AssignmentCount, double? SubmissionRate);

/// <summary>Một giáo viên có việc cần nhắc trong 4 tuần: số ngày vắng (trừ Chủ nhật) và tiết đã qua chưa điểm danh.</summary>
public sealed record LeadershipTeacherRow(int StaffId, string Name, string? DepartmentName, int AbsentDays, int UnmarkedSessions);

/// <summary>
/// Tình hình toàn trường (Principal) hoặc phân hiệu (Vice Principal). CampusId trống = toàn trường (Principal)
/// hoặc mọi phân hiệu đang phụ trách (Vice Principal). WeekStarts là 8 tuần của Kpi.Weeks.
/// Campuses, Grades: chuyên cần 4 tuần theo phân hiệu, theo khối (chỉ nhóm có học sinh); SubmissionGrades: tỷ lệ nộp 4 tuần theo khối;
/// Weekdays: chuyên cần 4 tuần theo thứ Hai tới thứ Bảy. Classes, Departments, Teachers là số liệu đầy đủ để phân tích sâu (MSMS AI), màn hình không liệt kê.
/// </summary>
public sealed record DashboardDto(
    int? CampusId, int? WeekNo, List<DateOnly> WeekStarts, int CampusCount, int ClassCount, int StudentCount,
    LeadershipAlerts Alerts, Kpi Attendance, Kpi Unexcused, Kpi Affected, Kpi Submission, AttendanceMix Mix,
    List<RateRow> Campuses, List<RateRow> Grades, List<double?> Weekdays, List<RateRow> SubmissionGrades, AssignmentSummary Assignments,
    List<AffectedSessionRow> Today, List<LeadershipClassRow> Classes, List<LeadershipDepartmentRow> Departments,
    List<LeadershipTeacherRow> Teachers, List<int> GradeOptions, int? GradeLevel = null);

// ---------- School Admin ----------

public sealed record AccountTypeRow(string AccountType, int Total, int Active, int PendingPasswordChange, int Locked, int NeverLoggedIn);

/// <summary>Một loại thiếu sót dữ liệu; Key do web dịch sang nhãn và đường dẫn màn hình sửa.</summary>
public sealed record DataGap(string Key, int Count);

public sealed record DayCount(DateOnly Date, int Count);

public sealed record CategoryCount(string Category, int Count);

public sealed record CampusTimetable(int CampusId, string Name, int ClassCount, int PublishedClassCount);

/// <summary>Thống kê hệ thống của School Admin: quy mô, tài khoản, dữ liệu cần bổ sung, hoạt động hệ thống, tiến độ thời khóa biểu. Chi tiết cấu trúc trường nằm trên các màn Cơ cấu nhà trường.</summary>
public sealed record AdminDashboardDto(
    string? AcademicYearName, string? SemesterName, DateOnly? SemesterStart, DateOnly? SemesterEnd,
    int CampusCount, int ClassCount, int StudentCount, int StaffCount,
    List<AccountTypeRow> Accounts, List<DataGap> Gaps, List<DayCount> Activity, List<CategoryCount> ActivityByCategory,
    List<CampusTimetable> Timetables);

// ---------- Lớp chủ nhiệm ----------

/// <summary>Tỷ lệ nộp bài rèn luyện của một môn trong lớp: bài đã hết hạn trong 4 tuần gần nhất.</summary>
public sealed record SubjectSubmissionRow(int SubjectId, string SubjectName, int AssignmentCount, double? Rate);

/// <summary>
/// Tình hình lớp chủ nhiệm. PresentToday: số học sinh có ít nhất một lượt Có mặt hoặc Muộn hôm nay, trống khi hôm nay chưa tiết nào được điểm danh.
/// Attendance.Weeks là chuyên cần 8 tuần của lớp, GradeWeeks của cả khối (cùng năm học).
/// </summary>
public sealed record HomeroomOverviewDto(
    int ClassId, string ClassName, int GradeLevel, int? WeekNo, List<NamedId> Classes,
    int StudentCount, int? PresentToday, int TodaySessions, int TodayMarkedSessions, int PendingLeave, int StreakStudents,
    Kpi Attendance, double? GradeRate, Kpi Submission, AttendanceMix Mix,
    List<DateOnly> WeekStarts, List<double?> GradeWeeks, AssignmentSummary Assignments, List<SubjectSubmissionRow> Subjects);

// ---------- Tình hình tổ chuyên môn ----------

/// <summary>
/// Thầy cô thuộc tổ hoặc dạy môn của tổ: tải dạy và các lớp theo thời khóa biểu đã phát hành, vắng hôm nay, số ngày vắng và tiết chưa điểm danh (4 tuần),
/// bài rèn luyện môn của tổ đã giao trong 4 tuần, bài đang mở và tỷ lệ nộp của bài đã hết hạn trong 4 tuần.
/// </summary>
public sealed record DepartmentMemberRow(
    int StaffId, string Name, bool InDepartment, int PeriodsPerWeek, List<string> ClassNames, bool AbsentToday, int AbsentDays,
    int UnmarkedSessions, int AssignmentCount, int OpenAssignments, double? SubmissionRate);

/// <summary>
/// Tình hình tổ chuyên môn. AbsenceWeeks: tiết của tổ bị ảnh hưởng theo tuần (kèm số giáo viên vắng);
/// Subjects: tỷ lệ nộp 4 tuần theo môn của tổ; Members: thầy cô của tổ (tải dạy, vắng, giao bài).
/// </summary>
public sealed record DepartmentBoardDto(
    List<NamedId> Departments, int? WeekNo, List<DateOnly> WeekStarts,
    double? AveragePeriods, Kpi Affected, int UnmarkedSessions, Kpi Submission, List<string> AbsentToday,
    List<AbsenceWeek> AbsenceWeeks, List<RateRow> Subjects, AssignmentSummary Assignments, List<DepartmentMemberRow> Members);
