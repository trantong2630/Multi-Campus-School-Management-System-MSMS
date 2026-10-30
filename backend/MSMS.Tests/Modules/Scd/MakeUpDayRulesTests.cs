using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Services;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;
using MSMS.Tests.Modules.Sch;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Scd;

/// <summary>
/// SCD-02, thêm tiết bù: các luật về NGÀY (Chủ nhật, ngoài học kỳ, ngày nghỉ, năm học của lớp). Test dựng service bằng DI nên không phụ thuộc
/// danh sách tham số của hàm khởi tạo; dùng để bảo đảm việc dùng chung quy tắc ngày học của SCH (4.2.20) không làm đổi hành vi.
/// </summary>
public class MakeUpDayRulesTests
{
    private const int Campus1 = 1, Campus2 = 2, Year1 = 1, Year2 = 2, ClassCurrent = 11, ClassOldYear = 13, Vice = 70, Teacher = 50, Subject = 1, Slot = 1;

    // Hôm nay 20/10/2026 (Thứ Ba). Học kỳ năm 1: 05/09/2026–18/01/2027 và 19/01/2027–31/05/2027; năm 2 bắt đầu 05/09/2027.
    private static readonly DateOnly Monday = D(2026, 10, 26), SchoolWideHoliday = D(2026, 10, 27), Campus1Holiday = D(2026, 10, 28), Campus2Holiday = D(2026, 10, 29);

    private static (MsmsDbContext Db, ScheduleAdjustmentService Service) Create()
    {
        var db = NewDb();
        var clock = ClockAt();
        var user = UserOf(AccountTypes.VicePrincipal, Vice);
        AddCampus(db, Campus1, "Phân hiệu 1");
        AddCampus(db, Campus2, "Phân hiệu 2");
        AddYear(db, Year1, "2026-2027", D(2026, 9, 5), D(2027, 5, 31), current: true);
        AddYear(db, Year2, "2027-2028", D(2027, 9, 5), D(2028, 5, 31));
        db.Semester.AddRange(
            new Semester { AcademicYearId = Year1, SemesterNo = 1, Name = "Học kỳ 1", StartDate = D(2026, 9, 5), EndDate = D(2027, 1, 18) },
            new Semester { AcademicYearId = Year1, SemesterNo = 2, Name = "Học kỳ 2", StartDate = D(2027, 1, 19), EndDate = D(2027, 5, 31) },
            new Semester { AcademicYearId = Year2, SemesterNo = 1, Name = "Học kỳ 1", StartDate = D(2027, 9, 5), EndDate = D(2028, 1, 18) });
        AddClass(db, ClassCurrent, Campus1, Year1, "6A");
        AddClass(db, ClassOldYear, Campus1, Year1, "6C");
        db.Holiday.AddRange(
            new Holiday { AcademicYearId = Year1, CampusId = null, HolidayDate = SchoolWideHoliday, CreatedAt = DateTime.UtcNow },
            new Holiday { AcademicYearId = Year1, CampusId = Campus1, HolidayDate = Campus1Holiday, CreatedAt = DateTime.UtcNow },
            new Holiday { AcademicYearId = Year1, CampusId = Campus2, HolidayDate = Campus2Holiday, CreatedAt = DateTime.UtcNow });
        db.CampusLeadership.Add(new CampusLeadership { CampusId = Campus1, StaffId = Vice, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow });
        AddStaff(db, Vice, "Hiệu phó A");
        AddStaff(db, Teacher, "Giáo viên Toán");
        db.Account.Add(new Account
        {
            PersonId = Teacher, Username = "gv50", PasswordHash = "x", AccountType = AccountTypes.Teacher, Status = AccountStatuses.Active, CreatedAt = DateTime.UtcNow,
        });
        db.SubjectDepartment.Add(new SubjectDepartment { DepartmentId = 1, Name = "Tổ Toán", IsActive = true });
        db.Subject.Add(new Subject { SubjectId = Subject, DepartmentId = 1, Name = "Toán", Abbreviation = "TOAN", IsActive = true });
        db.PeriodSlot.Add(new PeriodSlot { PeriodSlotId = Slot, SessionOfDay = "Morning", PeriodNo = 1, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(7, 45), IsActive = true });
        db.SaveChanges();

        var provider = new ServiceCollection()
            .AddSingleton(db).AddSingleton(user).AddSingleton(clock)
            .AddScoped<ScopeResolver>().AddScoped<AuditWriter>().AddScoped<NotificationSender>().AddScoped<ScheduleSupport>().AddScoped<SchoolDayService>()
            .BuildServiceProvider();
        return (db, ActivatorUtilities.CreateInstance<ScheduleAdjustmentService>(provider));
    }

    private static MakeUpRequest On(DateOnly date, int classId = ClassCurrent) => new(classId, date, Slot, Subject, Teacher);

    private static async Task<string> RejectionOf(ScheduleAdjustmentService svc, MakeUpRequest request) =>
        (await Assert.ThrowsAsync<ApiException>(() => svc.AddMakeUpAsync(request))).Message;

    [Fact]
    public async Task OrdinarySchoolDay_IsAccepted_AndCreatesAManualScheduledSession()
    {
        var (db, svc) = Create();

        var result = await svc.AddMakeUpAsync(On(Monday));

        var session = await db.TeachingSession.SingleAsync(s => s.SessionId == result.SessionId);
        Assert.Equal("Scheduled", session.Status);
        Assert.Equal("Manual", session.Source);
        Assert.Equal("Tiết bù", session.ChangeNote);
        Assert.Equal(Monday, session.SessionDate);
    }

    [Fact]
    public async Task Sunday_IsRejectedAsSunday()
    {
        var (_, svc) = Create();
        Assert.Contains("Chủ nhật", await RejectionOf(svc, On(D(2026, 11, 1))));
    }

    [Fact]
    public async Task SundayOutsideEverySemester_IsStillReportedAsSunday_NotAsOutsideSemester()
    {
        var (_, svc) = Create();
        Assert.Contains("Chủ nhật", await RejectionOf(svc, On(D(2027, 7, 4))));   // Chủ nhật giữa hè
    }

    [Fact]
    public async Task WeekdayOutsideEverySemester_IsRejectedAsOutsideSemester()
    {
        var (_, svc) = Create();
        Assert.Contains("ngoài học kỳ", await RejectionOf(svc, On(D(2027, 6, 7))));   // Thứ Hai sau khi học kỳ 2 kết thúc
    }

    [Fact]
    public async Task ClassOfAnOlderYear_CannotGetAMakeUpInsideTheNextYearsSemester()
    {
        var (_, svc) = Create();
        // 06/09/2027 là Thứ Hai trong học kỳ 1 của năm sau, nhưng lớp 6C thuộc năm học 2026-2027.
        Assert.Contains("ngoài học kỳ", await RejectionOf(svc, On(D(2027, 9, 6), ClassOldYear)));
    }

    [Fact]
    public async Task WholeSchoolHoliday_IsRejected()
    {
        var (_, svc) = Create();
        Assert.Contains("ngày nghỉ", await RejectionOf(svc, On(SchoolWideHoliday)));
    }

    [Fact]
    public async Task HolidayOfTheClassOwnCampus_IsRejected()
    {
        var (_, svc) = Create();
        Assert.Contains("ngày nghỉ", await RejectionOf(svc, On(Campus1Holiday)));
    }

    [Fact]
    public async Task HolidayOfAnotherCampus_DoesNotBlockThisCampus()
    {
        var (db, svc) = Create();

        var result = await svc.AddMakeUpAsync(On(Campus2Holiday));

        Assert.Equal(Campus2Holiday, (await db.TeachingSession.SingleAsync(s => s.SessionId == result.SessionId)).SessionDate);
    }

    [Fact]
    public async Task PastDate_IsRejected_BeforeAnyCalendarCheck()
    {
        var (_, svc) = Create();
        Assert.Contains("từ hôm nay", await RejectionOf(svc, On(D(2026, 10, 19))));
    }
}
