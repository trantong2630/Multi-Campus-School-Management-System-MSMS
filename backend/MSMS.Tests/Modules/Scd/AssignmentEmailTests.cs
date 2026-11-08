using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Email;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Services;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Scd;

/// <summary>
/// Email phân công (lệch BR-26, đã được người dùng đồng ý): giáo viên được giao dạy thay hoặc dạy tiết bù, giáo viên cũ khi đổi giáo viên
/// và giáo viên của tiết bị hủy nhận một email theo Person.Email,
/// chỉ sau khi thao tác lưu thành công; thông báo trong ứng dụng vẫn giữ nguyên.
/// </summary>
public class AssignmentEmailTests
{
    private const int Campus1 = 1, Year1 = 1, Class6A = 11, Vice = 70, TeacherA = 50, TeacherB = 51, TeacherNoEmail = 52, Subject = 1, Slot = 1, Slot2 = 2;
    private const long ScheduledSession = 501, PendingSession = 502;
    private static readonly DateOnly Monday = D(2026, 10, 26);

    /// <summary>Ghi lại thư đã gửi; tín hiệu mỗi lần gửi để test chờ tác vụ gửi nền.</summary>
    private sealed class FakeSender : IEmailSender
    {
        public readonly ConcurrentQueue<(string To, string Subject, string Body)> Sent = new();
        public readonly SemaphoreSlim Signal = new(0);

        public Task<bool> SendAsync(string recipient, string subject, string body, CancellationToken ct = default)
        {
            Sent.Enqueue((recipient, subject, body));
            Signal.Release();
            return Task.FromResult(true);
        }

        public async Task<(string To, string Subject, string Body)> NextAsync()
        {
            Assert.True(await Signal.WaitAsync(TimeSpan.FromSeconds(5)), "Không có email nào được gửi.");
            Sent.TryDequeue(out var mail);
            return mail;
        }

        /// <summary>Chờ ngắn để chắc tác vụ gửi nền (nếu có) đã chạy, rồi khẳng định không có thư.</summary>
        public async Task AssertNoneAsync()
        {
            Assert.False(await Signal.WaitAsync(TimeSpan.FromMilliseconds(300)), "Không được gửi email.");
            Assert.Empty(Sent);
        }
    }

    private static (MsmsDbContext Db, ScheduleAdjustmentService Adjust, TeacherAbsenceService Absence, FakeSender Mail) Create()
    {
        var db = NewDb();
        var clock = ClockAt();
        var user = UserOf(AccountTypes.VicePrincipal, Vice);
        AddCampus(db, Campus1, "Phân hiệu 1");
        AddYear(db, Year1, "2026-2027", D(2026, 9, 5), D(2027, 5, 31), current: true);
        db.Semester.Add(new Semester { AcademicYearId = Year1, SemesterNo = 1, Name = "Học kỳ 1", StartDate = D(2026, 9, 5), EndDate = D(2027, 1, 18) });
        AddClass(db, Class6A, Campus1, Year1, "6A");
        db.CampusLeadership.Add(new CampusLeadership { CampusId = Campus1, StaffId = Vice, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow });
        AddStaff(db, Vice, "Hiệu phó A");
        foreach (var (id, name, email) in new[] { (TeacherA, "Nguyễn Văn A", "a@truong.edu.vn"), (TeacherB, "Trần Thị B", " b@truong.edu.vn "), (TeacherNoEmail, "Lê C", (string?)null) })
        {
            AddStaff(db, id, name);
            db.Person.Find(id)!.Email = email;
            db.Account.Add(new Account
            {
                PersonId = id, Username = $"gv{id}", PasswordHash = "x", AccountType = AccountTypes.Teacher, Status = AccountStatuses.Active, CreatedAt = DateTime.UtcNow,
            });
        }
        db.SubjectDepartment.Add(new SubjectDepartment { DepartmentId = 1, Name = "Tổ Toán", IsActive = true });
        db.Subject.Add(new Subject { SubjectId = Subject, DepartmentId = 1, Name = "Toán", Abbreviation = "TOAN", IsActive = true });
        db.PeriodSlot.AddRange(
            new PeriodSlot { PeriodSlotId = Slot, SessionOfDay = "Morning", PeriodNo = 1, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(7, 45), IsActive = true },
            new PeriodSlot { PeriodSlotId = Slot2, SessionOfDay = "Morning", PeriodNo = 2, StartTime = new TimeOnly(7, 50), EndTime = new TimeOnly(8, 35), IsActive = true });
        db.TeachingSession.AddRange(
            new TeachingSession
            {
                SessionId = ScheduledSession, ClassId = Class6A, SubjectId = Subject, TeacherId = TeacherA, SessionDate = Monday, PeriodSlotId = Slot,
                Status = "Scheduled", Source = "Timetable", CreatedAt = DateTime.UtcNow,
            },
            new TeachingSession
            {
                SessionId = PendingSession, ClassId = Class6A, SubjectId = Subject, TeacherId = null, SessionDate = Monday, PeriodSlotId = Slot2,
                Status = "Pending", Source = "Manual", CreatedAt = DateTime.UtcNow,
            });
        db.SaveChanges();

        var mail = new FakeSender();
        var provider = new ServiceCollection()
            .AddSingleton(db).AddSingleton(user).AddSingleton(clock).AddLogging().AddSingleton<IEmailSender>(mail)
            .AddScoped<ScopeResolver>().AddScoped<AuditWriter>().AddScoped<NotificationSender>().AddScoped<ScheduleSupport>().AddScoped<SchoolDayService>()
            .AddScoped<AssignmentEmailNotifier>()
            .BuildServiceProvider();
        return (db, ActivatorUtilities.CreateInstance<ScheduleAdjustmentService>(provider),
            ActivatorUtilities.CreateInstance<TeacherAbsenceService>(provider), mail);
    }

    [Fact]
    public async Task ChangeTeacher_EmailsTheNewTeacher_AndTheOldTeacher()
    {
        var (db, adjust, _, mail) = Create();

        await adjust.ChangeTeacherAsync(ScheduledSession, new ChangeSessionTeacherRequest(TeacherB));

        var toNew = await mail.NextAsync();
        Assert.Equal("b@truong.edu.vn", toNew.To);   // email trong hồ sơ được trim
        Assert.StartsWith("[MSMS] Phân công dạy thay: Toán lớp 6A", toNew.Subject);
        Assert.Contains("thay Nguyễn Văn A", toNew.Body);
        Assert.Contains("07:00–07:45", toNew.Body);

        var toOld = await mail.NextAsync();
        Assert.Equal("a@truong.edu.vn", toOld.To);
        Assert.StartsWith("[MSMS] Bạn không còn dạy tiết: Toán lớp 6A", toOld.Subject);
        Assert.Contains("chuyển cho giáo viên Trần Thị B", toOld.Body);

        await mail.AssertNoneAsync();
        Assert.True(await db.Notification.AnyAsync(n => n.RecipientPersonId == TeacherB && n.NotificationType == NotificationTypes.ScheduleChanged));
    }

    [Fact]
    public async Task CancelSession_EmailsTheSessionTeacher()
    {
        var (_, adjust, _, mail) = Create();

        await adjust.CancelAsync(ScheduledSession);

        var sent = await mail.NextAsync();
        Assert.Equal("a@truong.edu.vn", sent.To);
        Assert.StartsWith("[MSMS] Tiết đã hủy: Toán lớp 6A", sent.Subject);
        Assert.Contains("đã bị hủy", sent.Body);
        await mail.AssertNoneAsync();
    }

    [Fact]
    public async Task ResolvePendingWithSubstitute_EmailsTheSubstitute()
    {
        var (_, _, absence, mail) = Create();

        await absence.ResolveAsync(PendingSession, new ResolvePendingRequest("Substitute", TeacherB));

        var sent = await mail.NextAsync();
        Assert.Equal("b@truong.edu.vn", sent.To);
        Assert.StartsWith("[MSMS] Phân công dạy thay: Toán lớp 6A", sent.Subject);
        Assert.Contains("tiết 2", sent.Subject);
    }

    [Fact]
    public async Task ResolvePendingByCancelling_SendsNoEmail()
    {
        var (_, _, absence, mail) = Create();

        await absence.ResolveAsync(PendingSession, new ResolvePendingRequest("Cancel", null));

        await mail.AssertNoneAsync();
    }

    [Fact]
    public async Task AddMakeUp_EmailsTheMakeUpTeacher()
    {
        var (_, adjust, _, mail) = Create();

        await adjust.AddMakeUpAsync(new MakeUpRequest(Class6A, D(2026, 10, 27), Slot, Subject, TeacherA));

        var sent = await mail.NextAsync();
        Assert.Equal("a@truong.edu.vn", sent.To);
        Assert.StartsWith("[MSMS] Phân công dạy tiết bù: Toán lớp 6A", sent.Subject);
        Assert.Contains("27/10/2026", sent.Subject);
    }

    [Fact]
    public async Task TeacherWithoutEmail_StillGetsTheAssignment_AndOnlyTheOldTeacherIsEmailed()
    {
        var (db, adjust, _, mail) = Create();

        await adjust.ChangeTeacherAsync(ScheduledSession, new ChangeSessionTeacherRequest(TeacherNoEmail));

        Assert.Equal(TeacherNoEmail, (await db.TeachingSession.SingleAsync(s => s.SessionId == ScheduledSession)).TeacherId);
        Assert.Equal("a@truong.edu.vn", (await mail.NextAsync()).To);
        await mail.AssertNoneAsync();
    }

    [Fact]
    public async Task RejectedAssignment_SendsNoEmail()
    {
        var (_, adjust, _, mail) = Create();

        // Lớp 6A đã có tiết ở (Thứ Hai, tiết 1): tiết bù bị từ chối, không có email.
        await Assert.ThrowsAsync<ApiException>(() => adjust.AddMakeUpAsync(new MakeUpRequest(Class6A, Monday, Slot, Subject, TeacherB)));

        await mail.AssertNoneAsync();
    }
}
