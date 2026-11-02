namespace MSMS.Api.Modules.Adm.Dtos;

// ---------- Dùng chung giữa các dashboard ----------

public sealed record GradeCount(int GradeLevel, int ClassCount, int StudentCount);

/// <summary>Tỷ lệ nộp tính trên các bài đã đóng (hạn đã qua): bài còn mở chưa tính vì học sinh vẫn còn thời gian. Null = chưa có bài đóng.</summary>
public sealed record SubjectRate(int SubjectId, string Name, int AssignmentCount, double? SubmissionRate);

public sealed record WeekPoint(DateOnly WeekStart, int Sessions, int Assignments, double? SubmissionRate);

public sealed record NamedId(int Id, string Name);

// ---------- Hiệu trưởng, hiệu phó (ADM-01) ----------

/// <summary>Số liệu của một phân hiệu; hàng của bảng so sánh khi Principal xem toàn trường.</summary>
public sealed record DashboardCampusRow(
    int CampusId, string Name, int ClassCount, int StudentCount, int StaffCount, int SessionsToday,
    List<string> Leaders, double? SubmissionRate);

/// <summary>Việc cần chú ý trong phạm vi. PendingLeaveRequests trống với Principal (không đọc đơn nghỉ, mục 2.3).</summary>
public sealed record DashboardAttention(
    int PendingSessions, int TeacherAbsencesToday, int? PendingLeaveRequests, int ClassesWithoutHomeroom);

/// <summary>
/// Dashboard cấp quản lý theo phạm vi: chỉ số liệu vận hành (hôm nay, 8 tuần, nộp bài, việc cần chú ý).
/// Cấu trúc tổ chức (tổ, môn, phân hiệu chi tiết) nằm trên các màn Thiết lập.
/// CampusId trống = toàn trường (Principal) hoặc mọi phân hiệu đang phụ trách (Vice Principal).
/// </summary>
public sealed record DashboardDto(
    int? CampusId, string? AcademicYearName, string? SemesterName,
    int CampusCount, int GradeCount, int ClassCount, int StudentCount, int StaffCount, int SessionsToday,
    DashboardAttention Attention,
    List<GradeCount> ByGrade, List<DashboardCampusRow> Campuses,
    List<SubjectRate> Subjects, List<WeekPoint> Weeks);

// ---------- School Admin ----------

public sealed record AccountTypeRow(string AccountType, int Total, int Active, int PendingPasswordChange, int Locked, int NeverLoggedIn);

/// <summary>Một loại thiếu sót dữ liệu; Key do web dịch sang nhãn và đường dẫn màn hình sửa.</summary>
public sealed record DataGap(string Key, int Count);

public sealed record DayCount(DateOnly Date, int Count);

public sealed record CategoryCount(string Category, int Count);

public sealed record CampusTimetable(int CampusId, string Name, int ClassCount, int PublishedClassCount);

/// <summary>Dashboard School Admin: tài khoản, dữ liệu cần bổ sung, hoạt động hệ thống, tiến độ thời khóa biểu. Cấu trúc trường nằm trên các màn Thiết lập.</summary>
public sealed record AdminDashboardDto(
    string? AcademicYearName, string? SemesterName, DateOnly? SemesterStart, DateOnly? SemesterEnd,
    List<AccountTypeRow> Accounts, List<DataGap> Gaps, List<DayCount> Activity, List<CategoryCount> ActivityByCategory,
    List<CampusTimetable> Timetables);

// ---------- Giáo viên ----------

public sealed record TeacherSessionItem(
    long SessionId, int PeriodNo, string SessionOfDay, TimeOnly StartTime, TimeOnly EndTime, int ClassId, string ClassName, string SubjectName);

public sealed record TeacherClassRow(int ClassId, string ClassName, int GradeLevel, List<string> Subjects, int StudentCount, int AssignmentCount, double? SubmissionRate);

public sealed record TeacherAssignmentItem(
    int AssignmentId, string Title, string ClassName, string SubjectName, DateTime DueAt, bool IsClosed, int RecipientCount, int SubmittedCount);

public sealed record TeacherDashboardDto(
    string FullName, string? AcademicYearName, string? SemesterName,
    int SessionsToday, int SessionsThisWeek, int ClassCount, int StudentCount, int SubjectCount,
    int OpenAssignments, int DueSoon, int AssignmentsWithMissing, double? SubmissionRate,
    List<TeacherSessionItem> Today, List<TeacherClassRow> Classes, List<TeacherAssignmentItem> Recent,
    List<NamedId> HomeroomClasses, List<NamedId> HeadDepartments);

// ---------- Lớp chủ nhiệm ----------

public sealed record HomeroomTeacherRow(string SubjectName, string TeacherName);

/// <summary>Hàng học sinh: hồ sơ, đơn nghỉ và phụ huynh (phạm vi chủ nhiệm, mục 2.3). Không có điểm hay bài nộp từng em.</summary>
public sealed record HomeroomStudentRow(
    int StudentId, string PersonCode, string FullName, string? Gender, DateOnly? DateOfBirth,
    int LeaveCount, int PendingLeave, int ParentCount, string? ParentName, string? ParentPhone, bool ParentHasAccount);

public sealed record HomeroomOverviewDto(
    int ClassId, string ClassName, int GradeLevel, string CampusName, string? AcademicYearName, List<NamedId> Classes,
    int StudentCount, int MaleCount, int FemaleCount, int PendingLeave, int LeaveLast30Days,
    int StudentsWithoutParent, int ParentsWithoutAccount, double? SubmissionRate,
    List<SubjectRate> Subjects, List<HomeroomTeacherRow> Teachers, List<HomeroomStudentRow> Students);
