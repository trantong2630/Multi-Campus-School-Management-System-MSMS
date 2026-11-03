using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Services;
using MSMS.Api.Modules.Scd.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Adm;

/// <summary>ADM-01 mở rộng (4.9.4): mỗi dashboard chỉ tổng hợp dữ liệu trong phạm vi và quyền đọc của vai trò.</summary>
public class DashboardScopeTests
{
    private static DashboardService Leadership(AccountTestFixture f, string role, int personId)
    {
        var user = AccountTestFixture.User(role, personId);
        return new DashboardService(f.Db, user, new ScopeResolver(f.Db, user, f.Clock), f.Clock);
    }

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

    [Fact]
    public async Task Principal_GetsNoLeaveCount_VicePrincipalCountsOnlyLedCampus()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        f.AddAccount(2, AccountTypes.Principal);
        f.Db.CampusLeadership.Add(new CampusLeadership { StaffId = 1, CampusId = 1, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.SaveChanges();
        Enroll(f, 100, 1);
        Enroll(f, 101, 2);
        AddLeave(f, 100, 1);
        AddLeave(f, 101, 2);

        var vp = await Leadership(f, AccountTypes.VicePrincipal, 1).GetAsync(null);
        Assert.Equal(1, vp.Attention.PendingLeaveRequests);
        Assert.Equal(1, vp.CampusCount);
        Assert.Equal(1, vp.StudentCount);
        Assert.Equal("Nguyễn An 1", Assert.Single(Assert.Single(vp.Campuses).Leaders));

        var principal = await Leadership(f, AccountTypes.Principal, 2).GetAsync(null);
        Assert.Null(principal.Attention.PendingLeaveRequests);
        Assert.Equal(2, principal.CampusCount);
        Assert.Equal(2, principal.Attention.ClassesWithoutHomeroom);
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
    public async Task DepartmentBoard_ShowsLoadAndUnmarkedSessions_OnlyForTheHead()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(5);
        f.AddAccount(6);
        f.AddDuties(5);                              // giáo viên 5 là tổ trưởng tổ Toán
        f.Publish(1, 1, 6);                          // giáo viên 6 dạy môn Toán ở lớp 1
        f.AddLesson(6, 1, -1);                       // tiết đã qua, chưa điểm danh
        f.AddLesson(6, 1, 0);                        // tiết hôm nay

        DepartmentBoardService Board(int personId) => new(f.Db, AccountTestFixture.User(AccountTypes.Teacher, personId), f.Clock,
            new SubjectResponsibilityService(f.Db, f.Clock));

        var d = await Board(5).GetAsync();
        var member = Assert.Single(d.Members, m => m.StaffId == 6);
        Assert.Equal(1, member.PeriodsPerWeek);
        Assert.Equal(1, member.UnmarkedSessions);
        Assert.Equal("Toán", Assert.Single(member.Teaching).SubjectName);
        Assert.Equal(1, d.SessionsToday);
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

        var service = new HomeroomOverviewService(f.Db, AccountTestFixture.User(AccountTypes.Teacher, 5), f.Clock);
        var d = await service.GetAsync(null);
        Assert.Equal(1, d.ClassId);
        Assert.Equal(100, Assert.Single(d.Students).StudentId);
        Assert.Equal(1, d.PendingLeave);
        Assert.Equal(1, d.StudentsWithoutParent);

        Assert.Equal(403, (await Assert.ThrowsAsync<ApiException>(() => service.GetAsync(2))).StatusCode);
        var other = new HomeroomOverviewService(f.Db, AccountTestFixture.User(AccountTypes.Teacher, 6), f.Clock);
        Assert.Equal(403, (await Assert.ThrowsAsync<ApiException>(() => other.GetAsync(1))).StatusCode);
    }

    [Fact]
    public async Task Progress_ScopesByRole_SchoolCampusAndHomeroomClass()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        f.AddAccount(2, AccountTypes.Principal);
        f.AddAccount(5);
        f.AddAccount(6);
        f.Db.CampusLeadership.Add(new CampusLeadership { StaffId = 1, CampusId = 1, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.HomeroomAssignment.Add(new HomeroomAssignment { StaffId = 5, ClassId = 1, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.SaveChanges();
        Enroll(f, 100, 1);
        Enroll(f, 101, 2);
        AddAssignment(f, 1, 1, 5, -1, [100], [100]);
        AddAssignment(f, 2, 2, 6, -1, [101], []);

        ProgressService Progress(string role, int personId) =>
            new(f.Db, AccountTestFixture.User(role, personId), f.Clock, new SubjectResponsibilityService(f.Db, f.Clock));

        var principal = await Progress(AccountTypes.Principal, 2).GetAsync(null);
        Assert.Equal("School", principal.Scope);
        Assert.Equal(2, principal.Items.Count);
        Assert.Equal(2, Assert.Single((await Progress(AccountTypes.Principal, 2).GetAsync(2)).Items).ClassId);

        var vp = await Progress(AccountTypes.VicePrincipal, 1).GetAsync(null);
        Assert.Equal("Campus", vp.Scope);
        Assert.Equal(1, Assert.Single(vp.Items).ClassId);
        Assert.Equal(403, (await Assert.ThrowsAsync<ApiException>(() => Progress(AccountTypes.VicePrincipal, 1).GetAsync(2))).StatusCode);

        var homeroom = await Progress(AccountTypes.Teacher, 5).GetAsync(null);
        Assert.Equal("Class", homeroom.Scope);
        var item = Assert.Single(homeroom.Items);
        Assert.Equal(1, item.ClassId);
        Assert.Equal(1, item.SubmittedCount);
        Assert.Empty(homeroom.Campuses);
        Assert.Equal(403, (await Assert.ThrowsAsync<ApiException>(() => Progress(AccountTypes.Teacher, 6).GetAsync(null))).StatusCode);
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
            marked.AttendanceRecord.Add(new AttendanceRecord { StudentId = student, Status = status, RecordedByPersonId = 5, RecordedAt = f.Clock.Now });
        f.Db.LeaveRequest.Add(new LeaveRequest { StudentId = 102, ParentId = 900, ClassId = 1, FromDate = f.Clock.Today.AddDays(-2),
            ToDate = f.Clock.Today.AddDays(-2), SessionOfDay = "AllDay", Status = "Approved", CreatedAt = f.Clock.Now });
        f.Db.SaveChanges();

        var d = await Leadership(f, AccountTypes.Principal, 2).GetAsync(1);
        Assert.Equal(1, d.Attendance.Present);
        Assert.Equal(1, d.Attendance.AbsentExcused);
        Assert.Equal(1, d.Attendance.AbsentUnexcused);
        Assert.Equal(33.3, d.Attendance.Rate);
        Assert.Equal(1, d.Attendance.UnmarkedSessions);
        var row = Assert.Single(d.Classes);
        Assert.Equal(1, row.ClassId);
        Assert.Equal(33.3, row.AttendanceRate);
        Assert.Equal(1, row.UnmarkedSessions);
        Assert.Equal(3, row.StudentCount);
        Assert.Equal(33.3, Assert.Single(d.Campuses).AttendanceRate);
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
