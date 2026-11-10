namespace MSMS.Api.Modules.Adm.Dtos;

// ---------- Dùng chung giữa các dashboard ----------

public sealed record GradeCount(int GradeLevel, int ClassCount, int StudentCount, double? AttendanceRate = null);

/// <summary>Chuyên cần của một thứ trong tuần (1 = Thứ Hai … 6 = Thứ Bảy) trong 4 tuần gần nhất.</summary>
public sealed record WeekdayRate(int Day, double? Rate, int Absent);

/// <summary>Chuyên cần trong 4 tuần gần nhất: số lượt Có mặt, Muộn, Vắng có phép, Vắng không phép; tiết đã qua chưa điểm danh; tiết hôm nay và số tiết hôm nay đã có điểm danh. Rate null khi chưa có lượt nào.</summary>
public sealed record AttendanceSummary(
    double? Rate, int Present, int Late, int AbsentExcused, int AbsentUnexcused, int UnmarkedSessions, int TodaySessions, int TodayMarkedSessions);

/// <summary>Một lớp trong phạm vi: sĩ số, chủ nhiệm, chuyên cần 4 tuần và tiết đã qua chưa điểm danh. Xếp chuyên cần thấp nhất lên đầu.</summary>
public sealed record DashboardClassRow(
    int ClassId, string ClassName, int GradeLevel, int CampusId, string CampusName, string? Homeroom, int StudentCount, double? AttendanceRate, int UnmarkedSessions);

public sealed record NamedId(int Id, string Name);

// ---------- Hiệu trưởng, hiệu phó (ADM-01) ----------

/// <summary>Số liệu của một phân hiệu; hàng của bảng so sánh khi Principal xem toàn trường.</summary>
public sealed record DashboardCampusRow(
    int CampusId, string Name, int ClassCount, int StudentCount, int StaffCount, int SessionsToday,
    List<string> Leaders, double? AttendanceRate = null);

/// <summary>Việc cần chú ý trong phạm vi. PendingLeaveRequests trống với Principal (không đọc đơn nghỉ, mục 2.3).</summary>
public sealed record DashboardAttention(
    int PendingSessions, int TeacherAbsencesToday, int? PendingLeaveRequests, int ClassesWithoutHomeroom);

/// <summary>
/// Dashboard cấp quản lý theo phạm vi: chỉ số liệu vận hành (hôm nay, 8 tuần, nộp bài, việc cần chú ý).
/// Cấu trúc tổ chức (tổ, môn, phân hiệu chi tiết) nằm trên các màn Cơ cấu nhà trường.
/// CampusId trống = toàn trường (Principal) hoặc mọi phân hiệu đang phụ trách (Vice Principal).
/// </summary>
public sealed record DashboardDto(
    int? CampusId, string? AcademicYearName, string? SemesterName,
    int CampusCount, int GradeCount, int ClassCount, int StudentCount, int StaffCount, int SessionsToday,
    DashboardAttention Attention,
    List<GradeCount> ByGrade, List<DashboardCampusRow> Campuses, List<AttendanceWeek> Weeks,
    AttendanceSummary Attendance, List<DashboardClassRow> Classes, List<WeekdayRate> Weekdays, List<int> GradeOptions, int? GradeLevel = null);

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

// ---------- Tiến độ bài tập rèn luyện (Hiệu trưởng, Hiệu phó, chủ nhiệm) ----------

public sealed record ProgressClassRow(int ClassId, string ClassName, int GradeLevel, int CampusId);

/// <summary>Giáo viên có phụ trách chuyên môn trong phạm vi (theo thời khóa biểu đã phát hành); Duties là số cặp (lớp, môn).</summary>
public sealed record ProgressTeacherRow(int TeacherId, string Name, int CampusId, int? DepartmentId, string? DepartmentName, int Duties);

/// <summary>Một bài tập đã giao, chỉ số liệu tổng hợp: số người nhận và số đã nộp, không có bài nộp hay điểm từng học sinh.</summary>
public sealed record ProgressItemRow(
    int AssignmentId, string Title, int CampusId, string CampusName, int DepartmentId, string DepartmentName,
    int ClassId, string ClassName, int GradeLevel, int SubjectId, string SubjectName, int TeacherId, string TeacherName,
    DateTime OpenAt, DateTime DueAt, bool IsClosed, int RecipientCount, int SubmittedCount);

/// <summary>Scope: School (Hiệu trưởng), Campus (Hiệu phó), Class (chủ nhiệm). Campuses để chọn phạm vi, trống với chủ nhiệm.</summary>
public sealed record ProgressDto(
    string Scope, string? AcademicYearName, string? SemesterName, DateTime Now,
    List<NamedId> Campuses, List<ProgressClassRow> Classes, List<ProgressTeacherRow> Teachers, List<ProgressItemRow> Items);

// ---------- Lớp chủ nhiệm ----------

public sealed record HomeroomTeacherRow(string SubjectName, string TeacherName);

/// <summary>Hàng học sinh: hồ sơ, đơn nghỉ và phụ huynh (phạm vi chủ nhiệm, mục 2.3). Không có điểm hay bài nộp từng em.</summary>
public sealed record HomeroomStudentRow(
    int StudentId, string PersonCode, string FullName, string? Gender, DateOnly? DateOfBirth,
    int LeaveCount, int PendingLeave, int ParentCount, string? ParentName, string? ParentPhone, bool ParentHasAccount,
    int AbsentExcused = 0, int AbsentUnexcused = 0);

public sealed record AttendanceWeek(DateOnly WeekStart, double? Rate, int Absent);

public sealed record HomeroomOverviewDto(
    int ClassId, string ClassName, int GradeLevel, string CampusName, string? AcademicYearName, List<NamedId> Classes,
    int StudentCount, int MaleCount, int FemaleCount, int PendingLeave, int LeaveLast30Days,
    int StudentsWithoutParent, int ParentsWithoutAccount,
    List<HomeroomTeacherRow> Teachers, List<HomeroomStudentRow> Students,
    AttendanceSummary Attendance, List<AttendanceWeek> AttendanceWeeks);

// ---------- Tình hình tổ chuyên môn ----------

public sealed record DepartmentTeachingRow(string SubjectName, List<string> ClassNames);

/// <summary>Thầy cô thuộc tổ hoặc dạy môn của tổ: tải dạy theo thời khóa biểu đã phát hành, vắng hôm nay, tiết đã qua chưa điểm danh.</summary>
public sealed record DepartmentMemberRow(
    int StaffId, string Name, string? CampusName, bool InDepartment, int PeriodsPerWeek, bool AbsentToday, int UnmarkedSessions,
    List<DepartmentTeachingRow> Teaching);

/// <summary>Số liệu vận hành của tổ chuyên môn: tải dạy và điểm danh các tiết thuộc môn của tổ. Không có số liệu bài tập (xem Tiến độ bài tập rèn luyện).</summary>
public sealed record DepartmentBoardDto(
    List<NamedId> Departments, List<string> SubjectNames, string? SemesterName,
    int SessionsToday, int PendingSessions, int AbsentToday,
    AttendanceSummary Attendance, List<AttendanceWeek> AttendanceWeeks, List<DepartmentMemberRow> Members);
