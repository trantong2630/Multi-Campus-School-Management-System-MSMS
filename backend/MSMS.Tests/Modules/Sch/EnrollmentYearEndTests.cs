using Microsoft.Extensions.Options;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>BR-09: ghi danh để trống ngày kết thúc nghĩa là tới hết năm học — không còn hiệu lực sau ngày cuối năm học.</summary>
public class EnrollmentYearEndTests
{
    private const int Campus1 = 1, Year1 = 1, Year2 = 2, ClassA = 11, ClassB = 21, StudentId = 101;
    private static readonly DateOnly Year1Start = new(2026, 9, 5), Year1End = new(2027, 5, 31), Year2Start = new(2027, 9, 5);

    private static (MsmsDbContext Db, EnrollmentService Enrollments, ClassService Classes) Create(DateOnly today)
    {
        var db = NewDb();
        var clock = ClockAt(today);
        var user = UserOf(AccountTypes.SchoolAdmin);
        AddCampus(db, Campus1, "Phân hiệu 1");
        AddYear(db, Year1, "2026-2027", Year1Start, Year1End, current: true);
        AddYear(db, Year2, "2027-2028", Year2Start, new DateOnly(2028, 5, 31));
        AddClass(db, ClassA, Campus1, Year1, "6A1");
        AddClass(db, ClassB, Campus1, Year2, "7A1", grade: 7);
        db.Person.Add(new Person { PersonId = StudentId, PersonType = "Student", PersonCode = "HS101", FullName = "Nguyễn Văn An", CreatedAt = DateTime.UtcNow });
        db.StudentProfile.Add(new StudentProfile { StudentId = StudentId, Status = "Studying" });
        db.Enrollment.Add(new Enrollment { StudentId = StudentId, ClassId = ClassA, StartDate = Year1Start, CreatedAt = DateTime.UtcNow });   // để trống ngày kết thúc
        db.SaveChanges();
        var enrollments = new EnrollmentService(db, clock, user, new ScopeResolver(db, user, clock), new AuditWriter(db, user, clock),
            new SessionService(db, clock, Options.Create(new JwtOptions())));
        return (db, enrollments, new ClassService(db, clock, user, new ScopeResolver(db, user, clock)));
    }

    [Fact]
    public async Task OpenEndedEnrollment_IsActiveThroughTheLastDayOfTheYear_AndNotAfter()
    {
        var (_, svc, _) = Create(Year1Start);

        Assert.Empty((await svc.GetRosterAsync(ClassA, Year1Start.AddDays(-1))).Students);
        Assert.Single((await svc.GetRosterAsync(ClassA, Year1Start)).Students);
        Assert.Single((await svc.GetRosterAsync(ClassA, Year1End)).Students);
        Assert.Empty((await svc.GetRosterAsync(ClassA, Year1End.AddDays(1))).Students);
    }

    [Fact]
    public async Task ExplicitEndDate_StillWinsOverTheYearEnd()
    {
        var (db, svc, _) = Create(Year1Start);
        var enrollment = db.Enrollment.Single();
        enrollment.EndDate = new DateOnly(2026, 12, 31);
        await db.SaveChangesAsync();

        Assert.Single((await svc.GetRosterAsync(ClassA, new DateOnly(2026, 12, 31))).Students);
        Assert.Empty((await svc.GetRosterAsync(ClassA, new DateOnly(2027, 1, 1))).Students);
    }

    [Fact]
    public async Task ClassList_StudentCount_StopsAtTheEndOfTheYear()
    {
        var (_, _, onLastDay) = Create(Year1End);
        var (_, _, dayAfter) = Create(Year1End.AddDays(1));

        Assert.Equal(1, (await onLastDay.ListAsync(Campus1, Year1, false)).Single().StudentCount);
        Assert.Equal(0, (await dayAfter.ListAsync(Campus1, Year1, false)).Single().StudentCount);
    }

    [Fact]
    public async Task DeactivateClass_BlockedWhileTheYearRuns_ButAllowedOnceItHasEnded()
    {
        var (_, _, during) = Create(new DateOnly(2027, 3, 1));
        var (_, _, after) = Create(new DateOnly(2027, 6, 15));

        await Assert.ThrowsAsync<MSMS.Api.Common.Errors.ApiException>(() => during.SetActiveAsync(ClassA, false));
        await after.SetActiveAsync(ClassA, false);   // không phải kết thúc từng ghi danh của năm đã qua
        Assert.Empty(await after.ListAsync(Campus1, Year1, includeInactive: false));
    }

    [Fact]
    public async Task NewYearEnrollment_MeansTheStudentIsOnlyInTheNewClassFromThatDay()
    {
        var (_, svc, _) = Create(Year2Start);

        await svc.EnrollAsync(ClassB, new EnrollRequest(StudentId, Year2Start));

        Assert.Empty((await svc.GetRosterAsync(ClassA, Year2Start)).Students);
        Assert.Single((await svc.GetRosterAsync(ClassB, Year2Start)).Students);
        Assert.Empty((await svc.GetRosterAsync(ClassB, Year2Start.AddDays(-1))).Students);
    }

    [Fact]
    public async Task Close_AfterTheYearHasEnded_RecordsTheLastDayOfTheYear_NotToday()
    {
        var (db, svc, _) = Create(new DateOnly(2027, 6, 15));

        await svc.CloseAsync(db.Enrollment.Single().EnrollmentId, new CloseEnrollmentRequest(EnrollmentCloseReasons.ClassTransfer));

        Assert.Equal(Year1End, db.Enrollment.Single().EndDate);
    }

    [Fact]
    public async Task Close_DuringTheYear_RecordsToday()
    {
        var today = new DateOnly(2027, 3, 1);
        var (db, svc, _) = Create(today);

        await svc.CloseAsync(db.Enrollment.Single().EnrollmentId, new CloseEnrollmentRequest(EnrollmentCloseReasons.ClassTransfer));

        Assert.Equal(today, db.Enrollment.Single().EndDate);
    }
}
