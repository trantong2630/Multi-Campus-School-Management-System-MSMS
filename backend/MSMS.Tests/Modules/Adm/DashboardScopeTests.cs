using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Services;
using MSMS.Api.Modules.Scd.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Adm;

/// <summary>ADM-01 mở rộng (4.9.6): mỗi màn Tình hình chỉ tổng hợp dữ liệu trong phạm vi và quyền đọc của vai trò.</summary>
public class DashboardScopeTests
{
    private static DashboardService Leadership(AccountTestFixture f, string role, int personId)
    {
        var user = AccountTestFixture.User(role, personId);
        return new DashboardService(f.Db, user, new ScopeResolver(f.Db, user, f.Clock), f.Clock);
    }

    private static HomeroomOverviewService Homeroom(AccountTestFixture f, int personId) =>
        new(f.Db, AccountTestFixture.User(AccountTypes.Teacher, personId), f.Clock);

    private static void Enroll(AccountTestFixture f, int studentId, int classId)
    {
        f.AddPerson(studentId, "Student");
        f.Db.Enrollment.Add(new Enrollment { StudentId = studentId, ClassId = classId, StartDate = f.Clock.Today.AddDays(-30) });
        f.Db.SaveChanges();
    }

    private static void AddAssignment(AccountTestFixture f, int id, int classId, int teacherId, int dueInDays, int[] recipients, int[] submitted)
    {
        var a = new Assignment { AssignmentId = id, ClassId = classId, SubjectId = 1, Title = $"Bài {id}", AssignedByPersonId = teacherId,
            OpenAt = f.Clock.Now.AddDays(-5), DueAt = f.Clock.Now.AddDays(dueInDays), CreatedAt = f.Clock.Now };
        foreach (var r in recipients) a.AssignmentRecipient.Add(new AssignmentRecipient { StudentId = r });
        foreach (var s in submitted) a.Submission.Add(new Submission { StudentId = s, StartedAt = f.Clock.Now.AddDays(-2), SubmittedAt = f.Clock.Now.AddDays(-1) });
        f.Db.Assignment.Add(a);
        f.Db.SaveChanges();
    }

    private static void AddLeave(AccountTestFixture f, int studentId, int classId, string status = "Pending")
    {
        f.Db.LeaveRequest.Add(new LeaveRequest { StudentId = studentId, ParentId = 900, ClassId = classId, FromDate = f.Clock.Today,
            ToDate = f.Clock.Today, SessionOfDay = "AllDay", Status = status, CreatedAt = f.Clock.Now });
        f.Db.SaveChanges();
    }

    private static void Mark(AccountTestFixture f, TeachingSession lesson, int studentId, string status) =>
        lesson.AttendanceRecord.Add(new AttendanceRecord { StudentId = studentId, Status = status, RecordedByPersonId = lesson.TeacherId ?? 5, RecordedAt = f.Clock.Now });

    [Fact]
    public async Task VicePrincipal_SeesOnlyLedCampus_OnlyPrincipalGetsClassesWithoutHomeroom()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        f.AddAccount(2, AccountTypes.Principal);
        f.Db.CampusLeadership.Add(new CampusLeadership { StaffId = 1, CampusId = 1, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.SaveChanges();
        Enroll(f, 100, 1);
        Enroll(f, 101, 2);

        var vp = await Leadership(f, AccountTypes.VicePrincipal, 1).GetAsync(null);
        Assert.Equal(1, vp.CampusCount);
        Assert.Equal(1, vp.StudentCount);
        Assert.Equal(1, Assert.Single(vp.Classes).ClassId);
        Assert.Null(vp.Alerts.ClassesWithoutHomeroom);

        var principal = await Leadership(f, AccountTypes.Principal, 2).GetAsync(null);
        Assert.Equal(2, principal.CampusCount);
        Assert.Equal(2, principal.Campuses.Count);
        Assert.Equal(2, principal.Alerts.ClassesWithoutHomeroom);
    }

    /// <summary>BR-09: ghi danh để trống ngày kết thúc là tới hết năm học; qua ngày cuối năm mà chưa đổi năm hiện hành thì không còn tính sĩ số.</summary>
    [Fact]
    public async Task AfterYearEnd_OpenEnrollmentNoLongerCounts()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(2, AccountTypes.Principal);
        Enroll(f, 100, 1);
        Assert.Equal(1, (await Leadership(f, AccountTypes.Principal, 2).GetAsync(null)).StudentCount);

        f.Db.AcademicYear.Single().EndDate = f.Clock.Today.AddDays(-1);
        f.Db.SaveChanges();

        Assert.Equal(0, (await Leadership(f, AccountTypes.Principal, 2).GetAsync(null)).StudentCount);
    }

    [Fact]
    public async Task VicePrincipal_CannotFilterAnotherCampus()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        f.Db.CampusLeadership.Add(new CampusLeadership { StaffId = 1, CampusId = 1, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.SaveChanges();
        var ex = await Assert.ThrowsAsync<ApiException>(() => Leadership(f, AccountTypes.VicePrincipal, 1).GetAsync(2));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task DepartmentBoard_ShowsLoadUnmarkedAndAssignments_OnlyForTheHead()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(5);
        f.AddAccount(6);
        f.AddDuties(5);                              // giáo viên 5 là tổ trưởng tổ Toán
        f.Publish(1, 1, 6);                          // giáo viên 6 dạy môn Toán ở lớp 1
        f.AddLesson(6, 1, -1);                       // tiết đã qua, chưa điểm danh
        f.AddLesson(6, 1, 0);                        // tiết hôm nay
        Enroll(f, 100, 1);
        AddAssignment(f, 1, 1, 6, -1, [100], [100]);

        DepartmentBoardService Board(int personId) => new(f.Db, AccountTestFixture.User(AccountTypes.Teacher, personId), f.Clock,
            new SubjectResponsibilityService(f.Db, f.Clock));

        var d = await Board(5).GetAsync();
        var member = Assert.Single(d.Members, m => m.StaffId == 6);
        Assert.Equal(1, member.PeriodsPerWeek);
        Assert.Equal(1, member.UnmarkedSessions);
        Assert.Equal(1, member.AssignmentCount);
        Assert.Equal(100, member.SubmissionRate);
        Assert.Equal("6A", Assert.Single(member.ClassNames));
        Assert.Equal(1, d.UnmarkedSessions);
        Assert.Equal(1, d.AveragePeriods);
        Assert.Equal(100, d.Submission.Value);
        Assert.Empty(d.AbsentToday);
        Assert.Equal(403, (await Assert.ThrowsAsync<ApiException>(() => Board(6).GetAsync())).StatusCode);
    }

    [Fact]
    public async Task Homeroom_RequiresCurrentHomeroomOfThatClass()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(5);
        f.AddAccount(6);
        f.Db.HomeroomAssignment.Add(new HomeroomAssignment { StaffId = 5, ClassId = 1, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.SaveChanges();
        Enroll(f, 100, 1);
        Enroll(f, 101, 2);
        AddLeave(f, 100, 1);
        AddLeave(f, 101, 2);

        var d = await Homeroom(f, 5).GetAsync(null);
        Assert.Equal(1, d.ClassId);
        Assert.Equal(1, d.StudentCount);
        Assert.Equal(1, d.PendingLeave);
        Assert.Null(d.PresentToday);

        Assert.Equal(403, (await Assert.ThrowsAsync<ApiException>(() => Homeroom(f, 5).GetAsync(2))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<ApiException>(() => Homeroom(f, 6).GetAsync(1))).StatusCode);
    }

    [Fact]
    public async Task Submission_CountsOnlyClosedAssignmentsInScope()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        f.AddAccount(5);
        f.AddAccount(6);
        f.Db.CampusLeadership.Add(new CampusLeadership { StaffId = 1, CampusId = 1, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.HomeroomAssignment.Add(new HomeroomAssignment { StaffId = 5, ClassId = 1, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.SaveChanges();
        Enroll(f, 100, 1);
        Enroll(f, 101, 1);
        Enroll(f, 102, 2);
        AddAssignment(f, 1, 1, 5, -1, [100, 101], [100]);   // đã hết hạn: 1 trên 2
        AddAssignment(f, 2, 1, 5, 3, [100, 101], []);        // chưa hết hạn: không tính
        AddAssignment(f, 3, 2, 6, -1, [102], []);           // phân hiệu khác

        var vp = await Leadership(f, AccountTypes.VicePrincipal, 1).GetAsync(null);
        Assert.Equal(50, vp.Submission.Value);
        Assert.Equal((1, 1, 0), (vp.Assignments.Open, vp.Assignments.DueSoon, vp.Assignments.LowCount));   // đúng 50% chưa tính là nộp thấp
        Assert.Equal(50, Assert.Single(vp.Classes, c => c.ClassId == 1).SubmissionRate);

        var subject = Assert.Single((await Homeroom(f, 5).GetAsync(null)).Subjects);
        Assert.Equal("Toán", subject.SubjectName);
        Assert.Equal(50, subject.Rate);
    }

    [Fact]
    public async Task Streak_CountsStudentsAbsentEveryPeriodForThreeSchoolDays()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(2, AccountTypes.Principal);
        f.AddAccount(5);
        f.Db.HomeroomAssignment.Add(new HomeroomAssignment { StaffId = 5, ClassId = 1, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.SaveChanges();
        Enroll(f, 100, 1);
        Enroll(f, 101, 1);
        foreach (var day in new[] { -1, -2, -3 })
        {
            var lesson = f.AddLesson(5, 1, day);
            Mark(f, lesson, 100, "Absent");
            Mark(f, lesson, 101, day == -2 ? "Present" : "Absent");   // có mặt một ngày giữa chừng: không đủ 3 ngày liên tiếp
        }
        f.Db.SaveChanges();

        var d = await Leadership(f, AccountTypes.Principal, 2).GetAsync(1);
        Assert.Equal(1, d.Alerts.StreakStudents);
        Assert.Equal(1, Assert.Single(d.Classes).StreakStudents);

        var homeroom = await Homeroom(f, 5).GetAsync(null);
        Assert.Equal(1, homeroom.StreakStudents);
        Assert.Equal((1, 5), (homeroom.Mix.Present, homeroom.Mix.AbsentExcused + homeroom.Mix.AbsentUnexcused));
    }

    [Fact]
    public async Task Affected_CountsSubstitutedCancelledAndPendingSessionsOfAbsentTeachers()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(2, AccountTypes.Principal);
        f.AddAccount(5);
        f.AddAccount(6);
        var absence = new TeacherAbsence { TeacherId = 5, FromDate = f.Clock.Today.AddDays(-1), ToDate = f.Clock.Today, CreatedAt = f.Clock.Now };
        f.Db.TeacherAbsence.Add(absence);
        f.Db.SaveChanges();
        var substituted = f.AddLesson(6, 1, -1);
        var cancelled = f.AddLesson(5, 1, -1, "Cancelled");
        var pending = f.AddLesson(5, 1, 0, "Pending");
        foreach (var s in new[] { substituted, cancelled, pending }) s.TeacherAbsenceId = absence.AbsenceId;
        f.AddLesson(5, 1, -2);                       // tiết bình thường, không tính
        f.Db.SaveChanges();

        var d = await Leadership(f, AccountTypes.Principal, 2).GetAsync(1);
        Assert.Equal(3, d.Affected.Value);
        var today = Assert.Single(d.Today);
        Assert.Equal("Pending", today.Outcome);
        Assert.Equal("Nguyễn An 5", today.AbsentTeacher);
        Assert.Equal(3, Assert.Single(d.Departments).AffectedSessions);
    }

    /// <summary>Tiết hủy vì ngày nghỉ là lịch nghỉ có kế hoạch, không phải sự cố: không vào "tiết bị ảnh hưởng" dù ngày nghỉ khai sau khi phát hành.</summary>
    [Fact]
    public async Task Affected_ExcludesSessionsCancelledForHolidays()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(2, AccountTypes.Principal);
        f.AddAccount(5);
        f.AddLesson(5, 1, -1, "Cancelled");          // rơi vào ngày nghỉ toàn trường: không tính
        f.AddLesson(5, 1, -3, "Cancelled");          // rơi vào ngày nghỉ của phân hiệu 1 (phân hiệu của lớp): không tính
        f.AddLesson(5, 1, -2, "Cancelled");          // hủy ngày thường: vẫn tính
        f.AddLesson(5, 1, -4, "Cancelled");          // ngày nghỉ của phân hiệu khác: vẫn tính
        f.Db.Holiday.AddRange(
            new Holiday { AcademicYearId = 1, CampusId = null, HolidayDate = f.Clock.Today.AddDays(-1), CreatedAt = f.Clock.Now },
            new Holiday { AcademicYearId = 1, CampusId = 1, HolidayDate = f.Clock.Today.AddDays(-3), CreatedAt = f.Clock.Now },
            new Holiday { AcademicYearId = 1, CampusId = 2, HolidayDate = f.Clock.Today.AddDays(-4), CreatedAt = f.Clock.Now });
        f.Db.SaveChanges();

        var d = await Leadership(f, AccountTypes.Principal, 2).GetAsync(1);

        Assert.Equal(2, d.Affected.Value);
    }

    /// <summary>Màn tổ đọc bài tập từ cùng mốc với màn toàn trường: bài hết hạn đầu kỳ 4 tuần liền trước vẫn vào tỷ lệ nộp kỳ trước.</summary>
    [Fact]
    public async Task DepartmentBoard_PreviousSubmissionRate_IncludesTheStartOfThePreviousPeriod()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(5);
        f.AddDuties(5);
        Enroll(f, 100, 1);
        var a = new Assignment { AssignmentId = 1, ClassId = 1, SubjectId = 1, Title = "Bài đầu kỳ trước", AssignedByPersonId = 5,
            OpenAt = f.Clock.Now.AddDays(-60), DueAt = f.Clock.Now.AddDays(-53), CreatedAt = f.Clock.Now };
        a.AssignmentRecipient.Add(new AssignmentRecipient { StudentId = 100 });
        a.Submission.Add(new Submission { StudentId = 100, StartedAt = f.Clock.Now.AddDays(-54), SubmittedAt = f.Clock.Now.AddDays(-54) });
        f.Db.Assignment.Add(a);
        f.Db.SaveChanges();

        var d = await new DepartmentBoardService(f.Db, AccountTestFixture.User(AccountTypes.Teacher, 5), f.Clock,
            new SubjectResponsibilityService(f.Db, f.Clock)).GetAsync();

        Assert.Equal(100, d.Submission.Previous);
    }

    [Fact]
    public async Task Attendance_SplitsExcusedFromUnexcused_AndCountsUnmarkedSessions()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(2, AccountTypes.Principal);
        f.AddAccount(5);
        Enroll(f, 100, 1);
        Enroll(f, 101, 1);
        Enroll(f, 102, 1);
        f.AddLesson(5, 1, -1);                       // đã qua, chưa điểm danh
        var marked = f.AddLesson(5, 1, -2);
        foreach (var (student, status) in new[] { (100, "Present"), (101, "Absent"), (102, "Absent") })
            Mark(f, marked, student, status);
        f.Db.LeaveRequest.Add(new LeaveRequest { StudentId = 102, ParentId = 900, ClassId = 1, FromDate = f.Clock.Today.AddDays(-2),
            ToDate = f.Clock.Today.AddDays(-2), SessionOfDay = "AllDay", Status = "Approved", CreatedAt = f.Clock.Now });
        f.Db.SaveChanges();

        var d = await Leadership(f, AccountTypes.Principal, 2).GetAsync(1);
        Assert.Equal(33.3, d.Attendance.Value);
        Assert.Equal(33.3, d.Unexcused.Value);       // 1 trên 3 lượt; vắng có phép không tính vào vắng không phép
        Assert.Equal(8, d.Attendance.Weeks.Count);
        Assert.Equal(1, d.Alerts.UnmarkedSessions);
        var row = Assert.Single(d.Classes);
        Assert.Equal(1, row.ClassId);
        Assert.Equal(33.3, row.Rate);
        Assert.Equal(3, row.StudentCount);
        Assert.Equal(33.3, row.UnexcusedRate);
        Assert.Equal((1, 0, 1, 1), (d.Mix.Present, d.Mix.Late, d.Mix.AbsentExcused, d.Mix.AbsentUnexcused));
        Assert.Single(d.Grades);
        Assert.Equal(6, d.Weekdays.Count);
    }

    [Fact]
    public async Task Admin_ReportsDataGapsAndAccounts()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(900, AccountTypes.SchoolAdmin);
        f.AddAccount(5);
        f.AddPerson(7);          // cán bộ chưa có tài khoản
        f.AddPerson(100, "Student");   // học sinh chưa ghi danh, chưa có phụ huynh

        var d = await new AdminDashboardService(f.Db, f.Clock).GetAsync();
        int Gap(string key) => d.Gaps.Single(g => g.Key == key).Count;
        Assert.Equal(2, Gap(AdminDashboardService.GapKeys.ClassesWithoutHomeroom));
        Assert.Equal(1, Gap(AdminDashboardService.GapKeys.StudentsWithoutClass));
        Assert.Equal(1, Gap(AdminDashboardService.GapKeys.StudentsWithoutParent));
        Assert.Equal(1, Gap(AdminDashboardService.GapKeys.StaffWithoutAccount));
        Assert.Equal(1, Gap(AdminDashboardService.GapKeys.TeachersWithoutDepartment));
        Assert.Equal(1, Gap(AdminDashboardService.GapKeys.DepartmentsWithoutHead));
        Assert.Equal(2, Gap(AdminDashboardService.GapKeys.CampusesWithoutLeader));
        Assert.Equal(1, d.Accounts.Single(a => a.AccountType == AccountTypes.Teacher).Total);
        Assert.Equal(14, d.Activity.Count);
    }
}
