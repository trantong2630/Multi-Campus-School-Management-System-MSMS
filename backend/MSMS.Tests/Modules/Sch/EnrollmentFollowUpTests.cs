using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>
/// Rà soát SCH: (1) lớp của năm học chưa bắt đầu phải hiện đúng sĩ số và danh sách, (2) học sinh rời trường thì mọi ghi danh còn lại cũng kết thúc,
/// (3) lớp còn ghi danh sắp bắt đầu không được ngừng hoạt động. Cả ba đều phát sinh từ việc năm sau đã có ghi danh trước khi năm đó bắt đầu (kết chuyển, import).
/// </summary>
public class EnrollmentFollowUpTests
{
    private const int Campus1 = 1, Year1 = 1, Year2 = 2, ClassA = 11, ClassA2 = 12, ClassB = 21, Bao = 101, An = 102, Chi = 103;
    private static readonly DateOnly Year1Start = new(2026, 9, 5), Year1End = new(2027, 5, 31), Year2Start = new(2027, 9, 5), Year2End = new(2028, 5, 31);
    private static readonly DateOnly Autumn = new(2026, 10, 29), Summer = new(2027, 7, 15);

    private static (MsmsDbContext Db, EnrollmentService Enrollments, ClassService Classes) Create(DateOnly today)
    {
        var db = NewDb();
        var clock = ClockAt(today);
        var user = UserOf(AccountTypes.SchoolAdmin);
        AddCampus(db, Campus1, "Phân hiệu 1");
        AddYear(db, Year1, "2026-2027", Year1Start, Year1End, current: true);
        AddYear(db, Year2, "2027-2028", Year2Start, Year2End);
        AddClass(db, ClassA, Campus1, Year1, "6A1", 6);
        AddClass(db, ClassA2, Campus1, Year1, "6A2", 6);
        AddClass(db, ClassB, Campus1, Year2, "7A1", 7);
        foreach (var id in new[] { Bao, An, Chi })
        {
            db.Person.Add(new Person { PersonId = id, PersonType = "Student", PersonCode = $"HS{id}", FullName = $"Học sinh {id}", CreatedAt = DateTime.UtcNow });
            db.StudentProfile.Add(new StudentProfile { StudentId = id, Status = "Studying" });
        }
        db.SaveChanges();
        var enrollments = new EnrollmentService(db, clock, user, new ScopeResolver(db, user, clock), new AuditWriter(db, user, clock),
            new SessionService(db, clock, Options.Create(new JwtOptions())));
        return (db, enrollments, new ClassService(db, clock, user, new ScopeResolver(db, user, clock)));
    }

    private static Enrollment Row(int student, int classId, DateOnly start, DateOnly? end = null, string? reason = null) =>
        new() { StudentId = student, ClassId = classId, StartDate = start, EndDate = end, Reason = reason, CreatedAt = DateTime.UtcNow };

    private static async Task<Enrollment> Add(MsmsDbContext db, Enrollment e)
    {
        db.Enrollment.Add(e);
        await db.SaveChangesAsync();
        return e;
    }

    // ======== Lỗi 1: năm học chưa bắt đầu hiện sĩ số 0 ========
    [Fact]
    public async Task ClassOfAYearNotYetStarted_ShowsItsHeadcountAtTheYearStart_NotZero()
    {
        var (db, _, classes) = Create(Autumn);
        foreach (var s in new[] { Bao, An, Chi }) await Add(db, Row(s, ClassB, Year2Start));   // đã xếp cho năm sau, chưa có hiệu lực hôm nay

        var list = await classes.ListAsync(Campus1, Year2, includeInactive: false);

        Assert.Equal(3, list.Single().StudentCount);
    }

    [Fact]
    public async Task ClassOfTheCurrentYear_StillCountsOnlyWhoIsEnrolledToday()
    {
        var (db, _, classes) = Create(Autumn);
        await Add(db, Row(Bao, ClassA, Year1Start));
        await Add(db, Row(An, ClassA, Year1Start, end: Autumn.AddDays(-1)));    // đã rời lớp hôm qua
        await Add(db, Row(Chi, ClassA, Autumn.AddDays(1)));                     // mai mới vào

        Assert.Equal(1, (await classes.ListAsync(Campus1, Year1, false)).Single(c => c.ClassId == ClassA).StudentCount);
    }

    [Fact]
    public async Task RosterWithoutADate_OpensAtTheYearStartForAYearNotYetStarted_AndAtTodayOtherwise()
    {
        var (db, enrollments, _) = Create(Autumn);
        await Add(db, Row(Bao, ClassA, Year1Start));
        await Add(db, Row(An, ClassB, Year2Start));

        var future = await enrollments.GetRosterAsync(ClassB, null);
        var current = await enrollments.GetRosterAsync(ClassA, null);

        Assert.Equal(Year2Start, future.Date);
        Assert.Single(future.Students);
        Assert.Equal(Autumn, current.Date);
        Assert.Single(current.Students);
    }

    [Theory]
    [InlineData("2026-10-29", "2027-09-05", "2027-09-05")]   // chưa bắt đầu: xem ngày bắt đầu
    [InlineData("2027-09-05", "2027-09-05", "2027-09-05")]   // đúng ngày bắt đầu
    [InlineData("2028-01-10", "2027-09-05", "2028-01-10")]   // đã bắt đầu: xem hôm nay
    public void DefaultViewDate_IsTodayUnlessTheYearHasNotStarted(string today, string yearStart, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), EnrollmentRules.DefaultViewDate(DateOnly.Parse(today), DateOnly.Parse(yearStart)));

    [Fact]
    public void ActiveAsOf_IsExactlyActiveAtTheDefaultViewDate_ForEveryKindOfEnrollment()
    {
        var year = new AcademicYear { Name = "2027-2028", StartDate = Year2Start, EndDate = Year2End };
        var cls = new Class { AcademicYear = year };
        Enrollment[] rows =
        [
            new() { Class = cls, StartDate = Year2Start },                                           // để trống ngày kết thúc
            new() { Class = cls, StartDate = Year2Start, EndDate = new DateOnly(2027, 12, 31) },     // rời giữa năm
            new() { Class = cls, StartDate = new DateOnly(2028, 1, 10) },                            // vào giữa năm
        ];
        DateOnly[] todays = [new(2026, 10, 29), Year2Start.AddDays(-1), Year2Start, new(2027, 12, 31), new(2028, 1, 10), Year2End, Year2End.AddDays(1)];

        foreach (var today in todays)
        {
            var asOf = EnrollmentRules.ActiveAsOf(today).Compile();
            var reference = EnrollmentRules.ActiveAt(EnrollmentRules.DefaultViewDate(today, year.StartDate)).Compile();
            foreach (var row in rows) Assert.Equal(reference(row), asOf(row));
        }
    }

    // ======== Lỗi 3: ngừng hoạt động lớp còn ghi danh sắp bắt đầu ========
    [Fact]
    public async Task ClassWithOnlyUpcomingEnrollments_CannotBeDeactivated()
    {
        var (db, _, classes) = Create(Autumn);
        foreach (var s in new[] { Bao, An, Chi }) await Add(db, Row(s, ClassB, Year2Start));

        var ex = await Assert.ThrowsAsync<ApiException>(() => classes.SetActiveAsync(ClassB, false));

        Assert.Contains("3 học sinh", ex.Message);
        Assert.Contains("sắp bắt đầu", ex.Message);
        Assert.True((await db.Class.FindAsync(ClassB))!.IsActive);
    }

    [Fact]
    public async Task ClassCanBeDeactivated_OnceItsUpcomingEnrollmentsAreGone()
    {
        var (db, _, classes) = Create(Autumn);
        var upcoming = await Add(db, Row(Bao, ClassB, Year2Start));
        db.Enrollment.Remove(upcoming);
        await db.SaveChangesAsync();

        await classes.SetActiveAsync(ClassB, false);

        Assert.False((await db.Class.FindAsync(ClassB))!.IsActive);
    }

    [Fact]
    public async Task EnrollmentsThatAlreadyEndedDoNotBlockDeactivation()
    {
        var (db, _, classes) = Create(Autumn);
        await Add(db, Row(Bao, ClassA, Year1Start, end: Autumn.AddDays(-1), reason: EnrollmentCloseReasons.ClassTransfer));

        await classes.SetActiveAsync(ClassA, false);

        Assert.False((await db.Class.FindAsync(ClassA))!.IsActive);
    }

    // ======== Lỗi 2: học sinh rời trường còn ghi danh năm sau ========
    [Fact]
    public async Task Leaving_ByClosingTheCurrentEnrollment_EndsIt_AndRemovesTheUpcomingOne()
    {
        var (db, enrollments, _) = Create(Autumn);
        var current = await Add(db, Row(Bao, ClassA, Year1Start));
        await Add(db, Row(Bao, ClassB, Year2Start));

        await enrollments.CloseAsync(current.EnrollmentId, new CloseEnrollmentRequest(EnrollmentCloseReasons.SchoolTransfer));

        var left = await db.Enrollment.Where(e => e.StudentId == Bao).ToListAsync();
        var only = Assert.Single(left);
        Assert.Equal(ClassA, only.ClassId);
        Assert.Equal(Autumn, only.EndDate);
        var profile = await db.StudentProfile.SingleAsync(s => s.StudentId == Bao);
        Assert.Equal("Transferred", profile.Status);
        Assert.Equal(Autumn, profile.LeftDate);
    }

    [Fact]
    public async Task Leaving_ByClosingTheOldYearEnrollmentInSummer_AlsoRemovesTheNextYearOne()
    {
        var (db, enrollments, _) = Create(Summer);                                // hè: năm cũ đã hết, năm mới chưa bắt đầu
        var old = await Add(db, Row(Bao, ClassA, Year1Start));
        await Add(db, Row(Bao, ClassB, Year2Start));

        await enrollments.CloseAsync(old.EnrollmentId, new CloseEnrollmentRequest(EnrollmentCloseReasons.Graduated));

        Assert.Equal(ClassA, (await db.Enrollment.SingleAsync(e => e.StudentId == Bao)).ClassId);
        Assert.Equal(Year1End, (await db.Enrollment.SingleAsync(e => e.StudentId == Bao)).EndDate);
        Assert.Equal("Graduated", (await db.StudentProfile.SingleAsync(s => s.StudentId == Bao)).Status);
    }

    [Fact]
    public async Task Leaving_ByClosingTheUpcomingEnrollmentDirectly_RemovesItAndClosesTheCurrentOne()
    {
        var (db, enrollments, _) = Create(Autumn);
        await Add(db, Row(Bao, ClassA, Year1Start));
        var upcoming = await Add(db, Row(Bao, ClassB, Year2Start));

        await enrollments.CloseAsync(upcoming.EnrollmentId, new CloseEnrollmentRequest(EnrollmentCloseReasons.SchoolTransfer));

        var remaining = Assert.Single(await db.Enrollment.Where(e => e.StudentId == Bao).ToListAsync());
        Assert.Equal(ClassA, remaining.ClassId);
        Assert.Equal(Autumn, remaining.EndDate);
        var profile = await db.StudentProfile.SingleAsync(s => s.StudentId == Bao);
        Assert.Equal("Transferred", profile.Status);
        Assert.Equal(Autumn, profile.LeftDate);
    }

    [Fact]
    public async Task ClassTransfer_ClosesOnlyTheChosenEnrollment_AndKeepsTheStudentStudying()
    {
        var (db, enrollments, _) = Create(Autumn);
        var current = await Add(db, Row(Bao, ClassA, Year1Start));
        await Add(db, Row(Bao, ClassB, Year2Start));

        await enrollments.CloseAsync(current.EnrollmentId, new CloseEnrollmentRequest(EnrollmentCloseReasons.ClassTransfer));

        Assert.Equal(2, await db.Enrollment.CountAsync(e => e.StudentId == Bao));
        Assert.Null((await db.Enrollment.SingleAsync(e => e.StudentId == Bao && e.ClassId == ClassB)).EndDate);
        Assert.Equal("Studying", (await db.StudentProfile.SingleAsync(s => s.StudentId == Bao)).Status);
    }

    [Fact]
    public async Task Leaving_NeverTouchesEnrollmentsThatAlreadyEnded_OrOtherStudents()
    {
        var (db, enrollments, _) = Create(Autumn);
        var endedEarlier = await Add(db, Row(Bao, ClassA2, Year1Start, end: new DateOnly(2026, 10, 10), reason: EnrollmentCloseReasons.ClassTransfer));
        var current = await Add(db, Row(Bao, ClassA, new DateOnly(2026, 10, 11)));
        var otherStudent = await Add(db, Row(An, ClassB, Year2Start));

        await enrollments.CloseAsync(current.EnrollmentId, new CloseEnrollmentRequest(EnrollmentCloseReasons.Graduated));

        var before = await db.Enrollment.AsNoTracking().SingleAsync(e => e.EnrollmentId == endedEarlier.EnrollmentId);
        Assert.Equal(new DateOnly(2026, 10, 10), before.EndDate);
        Assert.Equal(EnrollmentCloseReasons.ClassTransfer, before.Reason);
        Assert.True(await db.Enrollment.AnyAsync(e => e.EnrollmentId == otherStudent.EnrollmentId));
        Assert.Equal("Studying", (await db.StudentProfile.SingleAsync(s => s.StudentId == An)).Status);
    }

    [Fact]
    public async Task AfterARollover_AStudentWhoTransfersOutInSummer_NoLongerAppearsInTheNewClass()
    {
        var (db, enrollments, _) = Create(Summer);
        var old = await Add(db, Row(Bao, ClassA, Year1Start));
        await Add(db, Row(An, ClassA, Year1Start));
        var rollover = new RolloverService(db, ClockAt(Summer));
        await rollover.MoveStudentsAsync(new RolloverMoveRequest([new RolloverMovePair(ClassA, ClassB)]));
        Assert.Equal(2, await db.Enrollment.CountAsync(e => e.ClassId == ClassB));

        await enrollments.CloseAsync(old.EnrollmentId, new CloseEnrollmentRequest(EnrollmentCloseReasons.SchoolTransfer));

        Assert.Equal([An], (await enrollments.GetRosterAsync(ClassB, Year2Start)).Students.Select(s => s.StudentId));
    }
}
