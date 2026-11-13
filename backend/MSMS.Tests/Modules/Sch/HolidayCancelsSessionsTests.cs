using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>BR-08: khai ngày nghỉ hủy luôn các tiết chưa khóa trong ngày đó; tiết đã khóa không bị đụng tới.</summary>
public class HolidayCancelsSessionsTests
{
    private const int Year = 1, Teacher = 50, Student1 = 60, Student2 = 61;
    private const int SlotEnded = 1, SlotOpen = 2;   // đồng hồ giả chạy ở 08:00: tiết 1 (07:00–07:45) đã kết thúc, tiết 2 (09:00–09:45) chưa
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    private static (MsmsDbContext Db, CalendarService Svc) Create()
    {
        var db = NewDb();
        AddCampus(db, 1, "Phân hiệu 1");
        AddCampus(db, 2, "Phân hiệu 2");
        AddYear(db, Year, "2026-2027", D(2026, 9, 5), D(2027, 5, 31), current: true);
        AddClass(db, 10, 1, Year, "6A1");
        AddClass(db, 20, 2, Year, "6B1");
        AddStaff(db, Teacher, "Cô Lan");
        db.Subject.Add(new Subject { SubjectId = 1, DepartmentId = 1, Name = "Toán", Abbreviation = "TOAN", IsActive = true });
        db.PeriodSlot.AddRange(
            new PeriodSlot { PeriodSlotId = SlotEnded, SessionOfDay = "Morning", PeriodNo = 1, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(7, 45), IsActive = true },
            new PeriodSlot { PeriodSlotId = SlotOpen, SessionOfDay = "Morning", PeriodNo = 2, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 45), IsActive = true });
        db.Enrollment.AddRange(
            new Enrollment { StudentId = Student1, ClassId = 10, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow },
            new Enrollment { StudentId = Student2, ClassId = 20, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        return (db, NewService(db));
    }

    private static TeachingSession Session(MsmsDbContext db, int classId, DateOnly date, int slotId, string status = "Scheduled", int? teacher = Teacher)
    {
        var s = new TeachingSession
        {
            ClassId = classId, SubjectId = 1, TeacherId = status == "Pending" ? null : teacher, SessionDate = date, PeriodSlotId = slotId,
            Status = status, Source = "Auto", CreatedAt = DateTime.UtcNow,
        };
        db.TeachingSession.Add(s);
        db.SaveChanges();
        return s;
    }

    private static Task<HolidayDto> Declare(CalendarService svc, DateOnly date, int? campusId = null, string? reason = "Nghỉ đột xuất") =>
        svc.CreateHolidayAsync(new HolidayRequest(Year, campusId, date, reason));

    [Fact]
    public async Task WholeSchoolHoliday_CancelsUnlockedSessionsOfEveryCampus_KeepsTeacherAndMarksManual()
    {
        var (db, svc) = Create();
        var a = Session(db, 10, Tomorrow, SlotOpen);
        var b = Session(db, 20, Tomorrow, SlotOpen);

        var result = await Declare(svc, Tomorrow);

        Assert.Equal(2, result.CancelledSessions);
        foreach (var id in new[] { a.SessionId, b.SessionId })
        {
            var s = await db.TeachingSession.AsNoTracking().SingleAsync(x => x.SessionId == id);
            Assert.Equal("Cancelled", s.Status);
            Assert.Equal("Manual", s.Source);
            Assert.Equal(Teacher, s.TeacherId);
            Assert.Equal("Ngày nghỉ: Nghỉ đột xuất", s.ChangeNote);
        }
    }

    [Fact]
    public async Task CampusHoliday_OnlyCancelsSessionsOfThatCampus()
    {
        var (db, svc) = Create();
        var inCampus = Session(db, 10, Tomorrow, SlotOpen);
        var otherCampus = Session(db, 20, Tomorrow, SlotOpen);

        var result = await Declare(svc, Tomorrow, campusId: 1);

        Assert.Equal(1, result.CancelledSessions);
        Assert.Equal("Cancelled", (await db.TeachingSession.FindAsync(inCampus.SessionId))!.Status);
        Assert.Equal("Scheduled", (await db.TeachingSession.FindAsync(otherCampus.SessionId))!.Status);
    }

    [Fact]
    public async Task PendingSessions_AreCancelledToo()
    {
        var (db, svc) = Create();
        var pending = Session(db, 10, Tomorrow, SlotOpen, status: "Pending");

        var result = await Declare(svc, Tomorrow);

        Assert.Equal(1, result.CancelledSessions);
        Assert.Equal("Cancelled", (await db.TeachingSession.FindAsync(pending.SessionId))!.Status);
    }

    [Fact]
    public async Task LockedSessions_AreLeftAlone()
    {
        var (db, svc) = Create();
        var ended = Session(db, 10, Today, SlotEnded);              // đã quá giờ kết thúc
        var attended = Session(db, 20, Today, SlotOpen);            // chưa kết thúc nhưng đã có điểm danh
        db.AttendanceRecord.Add(new AttendanceRecord
        {
            SessionId = attended.SessionId, StudentId = Student2, Status = "Present", RecordedByPersonId = Teacher, RecordedAt = DateTime.UtcNow,
        });
        var open = Session(db, 10, Today, SlotOpen);
        db.SaveChanges();

        var result = await Declare(svc, Today);

        Assert.Equal(1, result.CancelledSessions);
        Assert.Equal("Scheduled", (await db.TeachingSession.FindAsync(ended.SessionId))!.Status);
        Assert.Equal("Scheduled", (await db.TeachingSession.FindAsync(attended.SessionId))!.Status);
        Assert.Equal("Cancelled", (await db.TeachingSession.FindAsync(open.SessionId))!.Status);
    }

    [Fact]
    public async Task OtherDatesAndAlreadyCancelledSessions_AreNotTouched()
    {
        var (db, svc) = Create();
        var nextDay = Session(db, 10, Tomorrow.AddDays(1), SlotOpen);
        var cancelled = Session(db, 10, Tomorrow, SlotEnded, status: "Cancelled");

        var result = await Declare(svc, Tomorrow);

        Assert.Equal(0, result.CancelledSessions);
        Assert.Equal("Scheduled", (await db.TeachingSession.FindAsync(nextDay.SessionId))!.Status);
        Assert.Equal("Cancelled", (await db.TeachingSession.FindAsync(cancelled.SessionId))!.Status);
        Assert.Empty(db.AuditLog);
        Assert.Empty(db.Notification);
    }

    [Fact]
    public async Task Cancelling_NotifiesStudentsAndTeacher_AndWritesOneAuditRecord()
    {
        var (db, svc) = Create();
        var a = Session(db, 10, Tomorrow, SlotOpen);
        var b = Session(db, 20, Tomorrow, SlotOpen);

        await Declare(svc, Tomorrow);

        var recipients = await db.Notification.AsNoTracking().Select(n => n.RecipientPersonId).ToListAsync();
        Assert.Contains(Student1, recipients);
        Assert.Contains(Student2, recipients);
        Assert.Equal(1, recipients.Count(r => r == Teacher));   // giáo viên có hai tiết vẫn chỉ nhận một thông báo
        Assert.All(db.Notification, n => Assert.Equal(NotificationTypes.ScheduleChanged, n.NotificationType));

        var audit = Assert.Single(db.AuditLog);
        Assert.Equal(AuditCategories.ScheduleAdjust, audit.Category);
        Assert.Equal("CancelSessionsForHoliday", audit.Action);
        Assert.Contains(a.SessionId.ToString(), audit.TargetIds);
        Assert.Contains(b.SessionId.ToString(), audit.TargetIds);
    }

    [Fact]
    public async Task Impact_CountsTheSameSessionsTheDeclarationWillCancel()
    {
        var (db, svc) = Create();
        Session(db, 10, Tomorrow, SlotOpen);
        Session(db, 20, Tomorrow, SlotOpen);
        Session(db, 10, Tomorrow.AddDays(1), SlotOpen);

        Assert.Equal(2, (await svc.HolidayImpactAsync(null, Tomorrow)).Sessions);
        Assert.Equal(1, (await svc.HolidayImpactAsync(2, Tomorrow)).Sessions);
        Assert.Equal(0, (await svc.HolidayImpactAsync(null, Tomorrow.AddDays(5))).Sessions);
    }

    [Fact]
    public async Task DeletingHoliday_DoesNotBringCancelledSessionsBack()
    {
        var (db, svc) = Create();
        var s = Session(db, 10, Tomorrow, SlotOpen);
        var holiday = await Declare(svc, Tomorrow);

        await svc.DeleteHolidayAsync(holiday.HolidayId);

        Assert.Equal("Cancelled", (await db.TeachingSession.FindAsync(s.SessionId))!.Status);
    }
}
