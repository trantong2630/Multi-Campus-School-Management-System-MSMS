using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Tests.Modules.Sch;

/// <summary>4.2.34: khoảng hiệu lực ghi danh (BR-09) — không chồng, không hở, dữ liệu lớp cũ vẫn đọc đúng.</summary>
public class EnrollmentTests
{
    private const int Year1 = 1, Year2 = 2, Campus = 1, ClassA = 11, ClassB = 12, ClassNextYear = 21, Student = 101, Admin = 900;
    private static readonly DateOnly YearStart = new(2026, 9, 1), YearEnd = new(2027, 5, 31), Today = new(2026, 10, 20);

    private sealed class FakeClock(DateOnly today) : IClock
    {
        public DateTime Now { get; } = today.ToDateTime(new TimeOnly(8, 0));
        public DateOnly Today { get; } = today;
    }

    private sealed class FakeAccessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }

    private static CurrentUser UserOf(int personId, string type)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(CurrentUser.AccountIdClaim, personId.ToString()), new Claim(CurrentUser.PersonIdClaim, personId.ToString()),
            new Claim(CurrentUser.TypeClaim, type),
        ], "Test");
        return new CurrentUser(new FakeAccessor(new DefaultHttpContext { User = new ClaimsPrincipal(identity) }));
    }

    private static (MsmsDbContext Db, EnrollmentService Service) Create(string accountType = AccountTypes.SchoolAdmin, int personId = Admin)
    {
        var db = new MsmsDbContext(new DbContextOptionsBuilder<MsmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var clock = new FakeClock(Today);
        var user = UserOf(personId, accountType);

        db.Campus.Add(new Campus { CampusId = Campus, Code = "C1", Name = "Cơ sở 1", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.AcademicYear.AddRange(
            new AcademicYear { AcademicYearId = Year1, Name = "2026-2027", StartDate = YearStart, EndDate = YearEnd, IsCurrent = true },
            new AcademicYear { AcademicYearId = Year2, Name = "2027-2028", StartDate = new DateOnly(2027, 9, 1), EndDate = new DateOnly(2028, 5, 31) });
        db.Class.AddRange(
            new Class { ClassId = ClassA, CampusId = Campus, AcademicYearId = Year1, Name = "6A", GradeLevel = 6, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = ClassB, CampusId = Campus, AcademicYearId = Year1, Name = "6B", GradeLevel = 6, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = ClassNextYear, CampusId = Campus, AcademicYearId = Year2, Name = "7A", GradeLevel = 7, IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Person.Add(new Person { PersonId = Student, PersonType = "Student", PersonCode = "HS101", FullName = "Nguyễn Văn An", CreatedAt = DateTime.UtcNow });
        db.StudentProfile.Add(new StudentProfile { StudentId = Student, Status = "Studying", AdmissionDate = YearStart });
        db.SaveChanges();

        var service = new EnrollmentService(db, clock, user, new ScopeResolver(db, user, clock),
            new AuditWriter(db, user, clock), new SessionService(db, clock, Options.Create(new JwtOptions())));
        return (db, service);
    }

    [Fact]
    public async Task Enroll_ThenEnrollAgainSameYear_IsRejected()
    {
        var (db, svc) = Create();
        await svc.EnrollAsync(ClassA, new EnrollRequest(Student, YearStart));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.EnrollAsync(ClassB, new EnrollRequest(Student, Today)));

        Assert.Contains("đã có ghi danh", ex.Message);
        Assert.Equal(1, await db.Enrollment.CountAsync());
    }

    [Fact]
    public async Task Enroll_StartDateOutsideYear_IsRejected()
    {
        var (_, svc) = Create();
        await Assert.ThrowsAsync<ApiException>(() => svc.EnrollAsync(ClassA, new EnrollRequest(Student, YearStart.AddDays(-1))));
        await Assert.ThrowsAsync<ApiException>(() => svc.EnrollAsync(ClassA, new EnrollRequest(Student, YearEnd.AddDays(1))));
    }

    [Fact]
    public async Task Transfer_ClosesOldTheDayBefore_AndOpensNew_WithNoGapOrOverlap()
    {
        var (db, svc) = Create();
        await svc.EnrollAsync(ClassA, new EnrollRequest(Student, YearStart));
        var oldId = (await db.Enrollment.SingleAsync()).EnrollmentId;
        var effective = new DateOnly(2026, 11, 3);

        await svc.TransferAsync(oldId, new TransferRequest(ClassB, effective));

        var old = await db.Enrollment.SingleAsync(e => e.ClassId == ClassA);
        var next = await db.Enrollment.SingleAsync(e => e.ClassId == ClassB);
        Assert.Equal(effective.AddDays(-1), old.EndDate);
        Assert.Equal(effective, next.StartDate);
        Assert.Null(next.EndDate);
        Assert.False(EnrollmentRules.Overlaps(old.StartDate, old.EndDate!.Value, next.StartDate, YearEnd));
    }

    [Fact]
    public async Task Roster_ReadsOldClassBeforeTransferDate_AndNewClassFromIt()
    {
        var (_, svc) = Create();
        await svc.EnrollAsync(ClassA, new EnrollRequest(Student, YearStart));
        var effective = new DateOnly(2026, 11, 3);
        await svc.TransferAsync((await svc.GetRosterAsync(ClassA, Today)).Students.Single().EnrollmentId, new TransferRequest(ClassB, effective));

        Assert.Single((await svc.GetRosterAsync(ClassA, effective.AddDays(-1))).Students);
        Assert.Empty((await svc.GetRosterAsync(ClassA, effective)).Students);
        Assert.Empty((await svc.GetRosterAsync(ClassB, effective.AddDays(-1))).Students);
        Assert.Single((await svc.GetRosterAsync(ClassB, effective)).Students);
    }

    [Fact]
    public async Task Transfer_EffectiveOnOrBeforeStart_IsRejected_AndNothingChanges()
    {
        var (db, svc) = Create();
        await svc.EnrollAsync(ClassA, new EnrollRequest(Student, new DateOnly(2026, 10, 1)));
        var id = (await db.Enrollment.SingleAsync()).EnrollmentId;

        await Assert.ThrowsAsync<ApiException>(() => svc.TransferAsync(id, new TransferRequest(ClassB, new DateOnly(2026, 10, 1))));

        Assert.Null((await db.Enrollment.SingleAsync()).EndDate);
    }

    [Fact]
    public async Task Transfer_ToOtherYearOrSameClass_IsRejected()
    {
        var (db, svc) = Create();
        await svc.EnrollAsync(ClassA, new EnrollRequest(Student, YearStart));
        var id = (await db.Enrollment.SingleAsync()).EnrollmentId;

        await Assert.ThrowsAsync<ApiException>(() => svc.TransferAsync(id, new TransferRequest(ClassNextYear, Today)));
        await Assert.ThrowsAsync<ApiException>(() => svc.TransferAsync(id, new TransferRequest(ClassA, Today)));
    }

    [Fact]
    public async Task Close_SetsEndDateToToday_AndCannotCloseTwice()
    {
        var (db, svc) = Create();
        await svc.EnrollAsync(ClassA, new EnrollRequest(Student, YearStart));
        var id = (await db.Enrollment.SingleAsync()).EnrollmentId;

        await svc.CloseAsync(id, new CloseEnrollmentRequest(EnrollmentCloseReasons.ClassTransfer));

        var e = await db.Enrollment.SingleAsync();
        Assert.Equal(Today, e.EndDate);
        Assert.Equal(EnrollmentCloseReasons.ClassTransfer, e.Reason);
        Assert.Equal("Studying", (await db.StudentProfile.SingleAsync()).Status);
        await Assert.ThrowsAsync<ApiException>(() => svc.CloseAsync(id, new CloseEnrollmentRequest(EnrollmentCloseReasons.ClassTransfer)));
    }

    [Fact]
    public async Task Close_AsSchoolTransfer_MarksStudentTransferred()
    {
        var (db, svc) = Create();
        await svc.EnrollAsync(ClassA, new EnrollRequest(Student, YearStart));
        var id = (await db.Enrollment.SingleAsync()).EnrollmentId;

        await svc.CloseAsync(id, new CloseEnrollmentRequest(EnrollmentCloseReasons.SchoolTransfer));

        var profile = await db.StudentProfile.SingleAsync();
        Assert.Equal("Transferred", profile.Status);
        Assert.Equal(Today, profile.LeftDate);
    }

    [Fact]
    public async Task Close_WithUnknownReason_IsRejected()
    {
        var (db, svc) = Create();
        await svc.EnrollAsync(ClassA, new EnrollRequest(Student, YearStart));
        var id = (await db.Enrollment.SingleAsync()).EnrollmentId;

        await Assert.ThrowsAsync<ApiException>(() => svc.CloseAsync(id, new CloseEnrollmentRequest("Other")));
    }

    [Fact]
    public async Task Roster_TeacherWhoIsNotHomeroom_IsForbidden()
    {
        var (_, svc) = Create(AccountTypes.Teacher, personId: 50);
        await Assert.ThrowsAsync<ApiException>(() => svc.GetRosterAsync(ClassA, Today));
    }

    [Fact]
    public async Task Candidates_ExcludeStudentsAlreadyEnrolledInYear()
    {
        var (_, svc) = Create();
        Assert.Single(await svc.FindCandidatesAsync(Year1, "An"));

        await svc.EnrollAsync(ClassA, new EnrollRequest(Student, YearStart));

        Assert.Empty(await svc.FindCandidatesAsync(Year1, null));
        Assert.Single(await svc.FindCandidatesAsync(Year2, null));
    }

    [Theory]
    [InlineData("2026-09-01", "2026-09-30", "2026-09-30", "2026-10-31", true)]    // chạm đúng một ngày
    [InlineData("2026-09-01", "2026-09-29", "2026-09-30", "2026-10-31", false)]   // liền kề, không chạm
    [InlineData("2026-09-10", "2026-09-20", "2026-09-01", "2026-12-31", true)]    // nằm gọn bên trong
    public void Overlaps_TreatsBothEndpointsAsInclusive(string a1, string a2, string b1, string b2, bool expected) =>
        Assert.Equal(expected, EnrollmentRules.Overlaps(DateOnly.Parse(a1), DateOnly.Parse(a2), DateOnly.Parse(b1), DateOnly.Parse(b2)));
}
