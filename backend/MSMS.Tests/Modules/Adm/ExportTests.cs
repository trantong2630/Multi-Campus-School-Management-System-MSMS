using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Services;
using MSMS.Api.Modules.Att.Services;
using MSMS.Api.Modules.Scd.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Adm;

/// <summary>ADM-03 (4.9.12, 4.9.13, 4.9.15): mỗi mục xuất chỉ đúng phạm vi người gọi; điểm danh kèm nhãn suy ra, thời khóa biểu đọc từ lịch dạy.</summary>
public class ExportTests
{
    private static AttendanceService Attendance(AccountTestFixture f, string role, int personId)
    {
        var user = AccountTestFixture.User(role, personId);
        return new AttendanceService(f.Db, user, new ScopeResolver(f.Db, user, f.Clock), new AccountScopeResolver(f.Db, f.Clock), f.Clock);
    }

    // TimetablePublishService không dùng tới khi đọc lịch dạy để xuất.
    private static ScheduleQueryService Schedule(AccountTestFixture f, string role, int personId)
    {
        var user = AccountTestFixture.User(role, personId);
        return new ScheduleQueryService(f.Db, user, new ScopeResolver(f.Db, user, f.Clock), null!);
    }

    private static ExportService Exports(AccountTestFixture f, string role, int personId) =>
        new(Attendance(f, role, personId), Schedule(f, role, personId));

    private static void Enroll(AccountTestFixture f, int studentId, int classId, int startDaysAgo = 30)
    {
        f.AddPerson(studentId, "Student");
        f.Db.Enrollment.Add(new Enrollment { StudentId = studentId, ClassId = classId, StartDate = f.Clock.Today.AddDays(-startDaysAgo) });
        f.Db.SaveChanges();
    }

    private static void LeadCampus(AccountTestFixture f, int staffId, int campusId)
    {
        f.Db.CampusLeadership.Add(new CampusLeadership { StaffId = staffId, CampusId = campusId, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.SaveChanges();
    }

    private static void Homeroom(AccountTestFixture f, int staffId, int classId)
    {
        f.Db.HomeroomAssignment.Add(new HomeroomAssignment { StaffId = staffId, ClassId = classId, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.SaveChanges();
    }

    private static int Status(Func<Task> act) => Assert.ThrowsAsync<ApiException>(act).Result.StatusCode;

    [Fact]
    public async Task Attendance_HasDerivedLabels_UnmarkedPastSessions_AndNoFutureOrCancelledSessions()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(5);
        Homeroom(f, 5, 1);
        Enroll(f, 100, 1);
        Enroll(f, 101, 1);
        Enroll(f, 102, 1);
        var marked = f.AddLesson(5, 1, -2);
        marked.AttendanceRecord.Add(new AttendanceRecord { StudentId = 100, Status = "Late", Note = "Trễ xe", RecordedByPersonId = 5, RecordedAt = f.Clock.Now });
        marked.AttendanceRecord.Add(new AttendanceRecord { StudentId = 101, Status = "Absent", RecordedByPersonId = 5, RecordedAt = f.Clock.Now });
        marked.AttendanceRecord.Add(new AttendanceRecord { StudentId = 102, Status = "Absent", RecordedByPersonId = 5, RecordedAt = f.Clock.Now });
        f.Db.LeaveRequest.Add(new LeaveRequest { StudentId = 102, ParentId = 900, ClassId = 1, FromDate = f.Clock.Today.AddDays(-2),
            ToDate = f.Clock.Today.AddDays(-2), SessionOfDay = "AllDay", Status = "Approved", CreatedAt = f.Clock.Now });
        f.Db.SaveChanges();
        f.AddLesson(5, 1, -1);                         // đã qua, chưa điểm danh
        f.AddLesson(5, 1, -3, status: "Cancelled");    // đã hủy: không vào file
        f.AddLesson(5, 1, 2);                          // chưa tới ngày: không vào file

        var data = await Attendance(f, AccountTypes.Teacher, 5).ExportAsync(1, f.Clock.Today.AddDays(-10), f.Clock.Today.AddDays(5));

        Assert.Equal(6, data.Rows.Count);
        var first = data.Rows.Where(r => r.Date == f.Clock.Today.AddDays(-2)).ToDictionary(r => r.StudentCode);
        Assert.Equal(AttendanceLabels.Late, first["HS000100"].Label);
        Assert.Equal("Trễ xe", first["HS000100"].Note);
        Assert.Equal(AttendanceLabels.UnexcusedAbsent, first["HS000101"].Label);
        Assert.Equal(AttendanceLabels.ExcusedAbsent, first["HS000102"].Label);
        Assert.All(data.Rows.Where(r => r.Date == f.Clock.Today.AddDays(-1)), r => Assert.Equal(AttendanceLabels.NotMarked, r.Label));
        Assert.DoesNotContain(data.Rows, r => r.Date > f.Clock.Today || r.Date == f.Clock.Today.AddDays(-3));
    }

    [Fact]
    public async Task Attendance_StudentAppearsOnlyOnDaysEnrolled()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(5);
        Homeroom(f, 5, 1);
        Enroll(f, 100, 1);
        Enroll(f, 101, 1, startDaysAgo: 1);            // vào lớp hôm qua
        f.AddLesson(5, 1, -2);
        f.AddLesson(5, 1, -1);

        var rows = (await Attendance(f, AccountTypes.Teacher, 5).ExportAsync(1, f.Clock.Today.AddDays(-5), f.Clock.Today)).Rows;

        Assert.Equal(3, rows.Count);
        Assert.Equal(f.Clock.Today.AddDays(-1), Assert.Single(rows, r => r.StudentCode == "HS000101").Date);
    }

    [Fact]
    public async Task Attendance_OnlyHomeroomClassOrLedCampus_AdminAndPrincipalRejected()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        f.AddAccount(2, AccountTypes.Principal);
        f.AddAccount(5);
        f.AddAccount(6);
        f.AddAccount(900, AccountTypes.SchoolAdmin);
        LeadCampus(f, 1, 1);
        Homeroom(f, 5, 1);
        DateOnly from = f.Clock.Today.AddDays(-7), to = f.Clock.Today;

        await Attendance(f, AccountTypes.VicePrincipal, 1).ExportAsync(1, from, to);
        await Attendance(f, AccountTypes.Teacher, 5).ExportAsync(1, from, to);

        Assert.Equal(403, Status(() => Attendance(f, AccountTypes.VicePrincipal, 1).ExportAsync(2, from, to)));   // phân hiệu khác
        Assert.Equal(403, Status(() => Attendance(f, AccountTypes.Teacher, 5).ExportAsync(2, from, to)));         // lớp không chủ nhiệm
        Assert.Equal(403, Status(() => Attendance(f, AccountTypes.Teacher, 6).ExportAsync(1, from, to)));         // không chủ nhiệm lớp nào
        Assert.Equal(403, Status(() => Attendance(f, AccountTypes.Principal, 2).ExportAsync(1, from, to)));
        Assert.Equal(403, Status(() => Attendance(f, AccountTypes.SchoolAdmin, 900).ExportAsync(1, from, to)));
    }

    [Fact]
    public async Task Range_LongerThanLimit_IsRejected()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        LeadCampus(f, 1, 1);
        var from = f.Clock.Today.AddDays(-ExportRules.MaxRangeDays - 1);

        Assert.Equal(400, Status(() => Attendance(f, AccountTypes.VicePrincipal, 1).ExportAsync(1, from, f.Clock.Today)));
        Assert.Equal(400, Status(() => Schedule(f, AccountTypes.VicePrincipal, 1).GetCampusSessionsForExportAsync(1, from, f.Clock.Today)));
        await Schedule(f, AccountTypes.VicePrincipal, 1).GetCampusSessionsForExportAsync(1, from.AddDays(1), f.Clock.Today);
    }

    [Fact]
    public async Task Timetable_ReadsTeachingSessionsOfLedCampus_IncludingAdjustments()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        f.AddAccount(2, AccountTypes.Principal);
        f.AddAccount(5);
        LeadCampus(f, 1, 1);
        f.AddLesson(5, 1, 1);
        f.AddLesson(5, 1, 2, status: "Cancelled");
        var makeUp = f.AddLesson(5, 1, 3);
        makeUp.Source = "Manual";
        f.AddLesson(5, 2, 1);                          // phân hiệu 2: không vào file
        f.Db.SaveChanges();
        DateOnly from = f.Clock.Today, to = f.Clock.Today.AddDays(7);

        var (campus, sessions) = await Schedule(f, AccountTypes.VicePrincipal, 1).GetCampusSessionsForExportAsync(1, from, to);

        Assert.Equal("Phân hiệu 1", campus);
        Assert.Equal(3, sessions.Count);
        Assert.All(sessions, s => Assert.Equal("6A", s.ClassName));
        Assert.Contains(sessions, s => s.Status == "Cancelled");
        Assert.Contains(sessions, s => s.Source == "Manual");

        Assert.Equal(403, Status(() => Schedule(f, AccountTypes.VicePrincipal, 1).GetCampusSessionsForExportAsync(2, from, to)));
        Assert.Equal(403, Status(() => Schedule(f, AccountTypes.Principal, 2).GetCampusSessionsForExportAsync(1, from, to)));
        Assert.Equal(403, Status(() => Schedule(f, AccountTypes.Teacher, 5).GetCampusSessionsForExportAsync(1, from, to)));
    }

    [Fact]
    public async Task AttendanceFile_HasVietnameseLabels_FilterAndSafeFileName()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        LeadCampus(f, 1, 1);
        f.Db.Class.Find(1)!.Name = "6/A Đặc biệt";
        f.Db.SaveChanges();
        Enroll(f, 100, 1);
        f.AddLesson(5, 1, -1);
        DateOnly from = new(2026, 9, 1), to = f.Clock.Today;

        var file = await Exports(f, AccountTypes.VicePrincipal, 1).AttendanceAsync(1, from, to);

        Assert.Equal($"MSMS_Diem_danh_6_A_Dac_biet_20260901_{to:yyyyMMdd}.xlsx", file.FileDownloadName);
        using var wb = new XLWorkbook(new MemoryStream(file.FileContents));
        var ws = wb.Worksheet(1);
        Assert.Equal("Ngày", ws.Cell(2, 1).GetString());
        Assert.Equal("Điểm danh", ws.Cell(2, 9).GetString());
        Assert.Equal("Chưa điểm danh", ws.Cell(3, 9).GetString());
        Assert.Equal("Sáng", ws.Cell(3, 3).GetString());
        Assert.Equal(f.Clock.Today.AddDays(-1).ToDateTime(TimeOnly.MinValue), ws.Cell(3, 1).GetDateTime());
        Assert.NotNull(ws.AutoFilter);
    }

    [Theory]
    [InlineData(AccountTypes.SchoolAdmin, false)]
    [InlineData(AccountTypes.Principal, false)]
    [InlineData(AccountTypes.VicePrincipal, true)]
    [InlineData(AccountTypes.Teacher, true)]
    [InlineData(AccountTypes.Student, false)]
    [InlineData(AccountTypes.Parent, false)]
    public async Task DataExporterPolicy_OnlyVicePrincipalAndTeacher(string role, bool expected)
    {
        await using var provider = new ServiceCollection().AddLogging().AddAuthorization(Policies.Register).BuildServiceProvider();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CurrentUser.TypeClaim, role)], "Test"));
        Assert.Equal(expected, (await auth.AuthorizeAsync(principal, Policies.DataExporter)).Succeeded);
    }
}
