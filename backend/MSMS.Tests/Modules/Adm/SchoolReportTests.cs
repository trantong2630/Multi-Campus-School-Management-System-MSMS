using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Adm;

/// <summary>Báo cáo toàn trường (ADM-01): so sánh phân hiệu và lớp, cảnh báo theo ngưỡng cố định, chỉ Hiệu trưởng đọc.</summary>
public class SchoolReportTests
{
    private static SchoolReportService Report(AccountTestFixture f) => new(f.Db, f.Clock);

    private static void Enroll(AccountTestFixture f, int studentId, int classId)
    {
        f.AddPerson(studentId, "Student");
        f.Db.Enrollment.Add(new Enrollment { StudentId = studentId, ClassId = classId, StartDate = f.Clock.Today.AddDays(-60) });
        f.Db.SaveChanges();
    }

    private static void Mark(AccountTestFixture f, TeachingSession lesson, int studentId, string status)
    {
        lesson.AttendanceRecord.Add(new AttendanceRecord { StudentId = studentId, Status = status, RecordedByPersonId = 5, RecordedAt = f.Clock.Now });
        f.Db.SaveChanges();
    }

    [Fact]
    public async Task Report_ComparesCampuses_AndRaisesAlertsOnlyOverTheThresholds()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(5);
        Enroll(f, 100, 1);
        Enroll(f, 101, 1);
        Enroll(f, 103, 2);
        f.Db.HomeroomAssignment.Add(new HomeroomAssignment { StaffId = 5, ClassId = 2, StartDate = f.Clock.Today.AddDays(-30) });
        f.Db.SaveChanges();

        // Lớp 6A (phân hiệu 1): chuyên cần 50%, 5 tiết đã qua chưa điểm danh, bài tập nộp 1/3; chưa có chủ nhiệm nhưng báo cáo không cảnh báo việc đó.
        var marked = f.AddLesson(5, 1, -2);
        Mark(f, marked, 100, "Present");
        Mark(f, marked, 101, "Absent");
        for (var d = -7; d <= -3; d++) f.AddLesson(5, 1, d);
        f.Db.Assignment.Add(NewAssignment(f, 1, 1, recipients: [100, 101, 102], submitted: [100]));

        // Lớp 6B (phân hiệu 2): chuyên cần 100%, có chủ nhiệm, một tiết bị hủy, bài tập nộp đủ.
        var fine = f.AddLesson(5, 2, -1);
        Mark(f, fine, 103, "Present");
        f.AddLesson(5, 2, -4, status: "Cancelled");
        f.Db.Assignment.Add(NewAssignment(f, 2, 2, recipients: [103], submitted: [103]));
        f.Db.SaveChanges();

        var r = await Report(f).GetAsync(null, null, null, null);

        var one = Assert.Single(r.Campuses, c => c.CampusId == 1);
        Assert.Equal(50, one.AttendanceRate);
        Assert.Equal(5, one.UnmarkedSessions);
        Assert.Equal(1, one.AssignmentCount);
        Assert.Equal(33.3, one.SubmissionRate);
        var two = Assert.Single(r.Campuses, c => c.CampusId == 2);
        Assert.Equal(100, two.AttendanceRate);
        Assert.Equal(1, two.CancelledSessions);
        Assert.Equal(100, two.SubmissionRate);

        Assert.Equal("6A", r.Classes[0].ClassName);   // chuyên cần thấp nhất lên đầu
        var kinds = r.Alerts.Where(a => a.ClassId == 1).Select(a => a.Kind).ToList();
        Assert.Equal([SchoolReportService.AlertKinds.LowAttendance, SchoolReportService.AlertKinds.ManyUnmarked,
            SchoolReportService.AlertKinds.LowSubmission], kinds);
        Assert.DoesNotContain(r.Alerts, a => a.ClassId == 2);
    }

    private static Assignment NewAssignment(AccountTestFixture f, int id, int classId, int[] recipients, int[] submitted)
    {
        var a = new Assignment { AssignmentId = id, ClassId = classId, SubjectId = 1, Title = $"Bài {id}", AssignedByPersonId = 5,
            OpenAt = f.Clock.Now.AddDays(-9), DueAt = f.Clock.Now.AddDays(-3), CreatedAt = f.Clock.Now };
        foreach (var s in recipients) a.AssignmentRecipient.Add(new AssignmentRecipient { StudentId = s });
        foreach (var s in submitted) a.Submission.Add(new Submission { StudentId = s, StartedAt = f.Clock.Now.AddDays(-5), SubmittedAt = f.Clock.Now.AddDays(-4) });
        return a;
    }

    [Fact]
    public async Task Report_FiltersByCampus_AndIgnoresAssignmentsNotYetDue()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(5);
        Enroll(f, 100, 1);
        var open = NewAssignment(f, 1, 1, recipients: [100], submitted: []);
        open.DueAt = f.Clock.Now.AddDays(2);   // chưa hết hạn: chưa tính vào tỷ lệ nộp
        f.Db.Assignment.Add(open);
        f.Db.SaveChanges();

        var r = await Report(f).GetAsync(null, null, campusId: 1, gradeLevel: null);

        Assert.Equal(1, Assert.Single(r.Campuses).CampusId);
        Assert.Equal(0, r.Campuses[0].AssignmentCount);
        Assert.Null(r.Campuses[0].SubmissionRate);
        Assert.DoesNotContain(r.Alerts, a => a.Kind == SchoolReportService.AlertKinds.LowSubmission);
    }

    [Fact]
    public async Task Report_RejectsBadRangeAndUnknownCampus()
    {
        using var f = new AccountTestFixture();
        var today = f.Clock.Today;

        Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() => Report(f).GetAsync(today, today.AddDays(-1), null, null))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<ApiException>(() =>
            Report(f).GetAsync(today.AddDays(-SchoolReportService.MaxDays), today, null, null))).StatusCode);
        Assert.Equal(404, (await Assert.ThrowsAsync<ApiException>(() => Report(f).GetAsync(null, null, 99, null))).StatusCode);
        await Report(f).GetAsync(today.AddDays(-(SchoolReportService.MaxDays - 1)), today, null, null);
    }

    [Theory]
    [InlineData(AccountTypes.SchoolAdmin, false)]
    [InlineData(AccountTypes.Principal, true)]
    [InlineData(AccountTypes.VicePrincipal, false)]
    [InlineData(AccountTypes.Teacher, false)]
    [InlineData(AccountTypes.Student, false)]
    [InlineData(AccountTypes.Parent, false)]
    public async Task SchoolReportPolicy_OnlyPrincipal(string role, bool expected)
    {
        await using var provider = new ServiceCollection().AddLogging().AddAuthorization(Policies.Register).BuildServiceProvider();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CurrentUser.TypeClaim, role)], "Test"));
        Assert.Equal(expected, (await auth.AuthorizeAsync(principal, Policies.SchoolReport)).Succeeded);
    }
}
