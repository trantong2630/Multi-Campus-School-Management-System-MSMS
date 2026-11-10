using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Tests.Modules.Iam;

internal sealed class AccountTestFixture : IDisposable
{
    public MsmsDbContext Db { get; } = new(new DbContextOptionsBuilder<MsmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    public AuthTestFixture.TestClock Clock { get; } = new();
    public IOptions<JwtOptions> Jwt { get; } = Options.Create(new JwtOptions
    {
        Key = "account-lifecycle-test-signing-key-with-at-least-thirty-two-bytes",
        Issuer = "msms-tests", ExpiryMinutes = 15, RefreshDays = 7,
    });
    public SessionService Sessions => new(Db, Clock, Jwt);
    public AccountService Accounts => new(Db, User(AccountTypes.SchoolAdmin, 900), Clock,
        new AuditWriter(Db, User(AccountTypes.SchoolAdmin, 900), Clock), Sessions, new NotificationSender(Db, Clock));

    public AccountTestFixture()
    {
        Db.Campus.AddRange(
            new Campus { CampusId = 1, Code = "PH1", Name = "Phân hiệu 1", IsActive = true },
            new Campus { CampusId = 2, Code = "PH2", Name = "Phân hiệu 2", IsActive = true });
        Db.AcademicYear.Add(new AcademicYear { AcademicYearId = 1, Name = "2026-2027", IsCurrent = true,
            StartDate = new(2026, 9, 1), EndDate = new(2027, 5, 31) });
        Db.Semester.Add(new Semester { SemesterId = 1, AcademicYearId = 1, SemesterNo = 1, Name = "Học kỳ 1",
            StartDate = new(2026, 9, 1), EndDate = new(2027, 1, 15) });
        Db.Class.AddRange(new Class { ClassId = 1, CampusId = 1, AcademicYearId = 1, Name = "6A", IsActive = true },
            new Class { ClassId = 2, CampusId = 2, AcademicYearId = 1, Name = "6B", IsActive = true });
        Db.SubjectDepartment.Add(new SubjectDepartment { DepartmentId = 1, Name = "Toán", IsActive = true });
        Db.Subject.Add(new Subject { SubjectId = 1, DepartmentId = 1, Name = "Toán", Abbreviation = "TOAN", IsActive = true });
        Db.PeriodSlot.Add(new PeriodSlot { PeriodSlotId = 1, SessionOfDay = "Morning", PeriodNo = 1,
            StartTime = new(15, 0), EndTime = new(15, 45), IsActive = true });
        Db.SaveChanges();
    }

    public static ClaimsPrincipal Principal(string role, int personId, int accountId, Guid? sessionId = null) =>
        new(new ClaimsIdentity([
            new Claim(CurrentUser.TypeClaim, role), new Claim(CurrentUser.PersonIdClaim, personId.ToString()),
            new Claim(CurrentUser.AccountIdClaim, accountId.ToString()),
            new Claim(CurrentUser.SessionIdClaim, (sessionId ?? Guid.NewGuid()).ToString()),
        ], "Test"));

    public static CurrentUser User(string role, int personId) => new(new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext { User = Principal(role, personId, personId) },
    });

    public Person AddPerson(int id, string type = "Staff", string? name = null, int campusId = 1, bool active = true)
    {
        var person = new Person { PersonId = id, PersonType = type, FullName = name ?? $"Nguyễn An {id}",
            PersonCode = $"{(type == "Staff" ? "CB" : type == "Student" ? "HS" : "PH")}{id:D6}", CreatedAt = Clock.Now };
        Db.Person.Add(person);
        if (type == "Staff") Db.StaffProfile.Add(new StaffProfile { StaffId = id, PrimaryCampusId = campusId, IsActive = active });
        if (type == "Student") Db.StudentProfile.Add(new StudentProfile { StudentId = id, Status = "Studying" });
        Db.SaveChanges();
        return person;
    }

    public Account AddAccount(int id, string role = AccountTypes.Teacher, string status = AccountStatuses.Active, int campusId = 1)
    {
        var person = AddPerson(id, role == AccountTypes.Student ? "Student" : role == AccountTypes.Parent ? "Parent" : "Staff", campusId: campusId);
        var account = new Account { AccountId = id, Person = person, Username = $"user{id}",
            AccountType = role, Status = status, CreatedAt = Clock.Now };
        account.PasswordHash = AuthService.HashPassword(account, "OldPassword123!");
        Db.Account.Add(account);
        Db.SaveChanges();
        return account;
    }

    public TeachingSession AddLesson(int teacherId, int classId, int days = 0, string status = "Scheduled", bool locked = false)
    {
        var lesson = new TeachingSession { TeacherId = teacherId, ClassId = classId, SubjectId = 1, PeriodSlotId = 1,
            SessionDate = Clock.Today.AddDays(days), Status = status, Source = "Timetable", CreatedAt = Clock.Now };
        Db.TeachingSession.Add(lesson);
        if (locked) lesson.AttendanceRecord.Add(new AttendanceRecord { Status = "Present", RecordedAt = Clock.Now });
        Db.SaveChanges();
        return lesson;
    }

    public void AddDuties(int staffId)
    {
        Db.CampusLeadership.Add(new CampusLeadership { CampusId = 1, StaffId = staffId, StartDate = Clock.Today.AddDays(-10) });
        Db.DepartmentHead.Add(new DepartmentHead { DepartmentId = 1, StaffId = staffId, StartDate = Clock.Today.AddDays(-10) });
        Db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = 1, StaffId = staffId, StartDate = Clock.Today.AddDays(-10) });
        Db.SaveChanges();
    }

    public void Publish(int versionId, int classId, int teacherId, int daysAgo = 1, string status = "Published")
    {
        var version = new TimetableVersion { VersionId = versionId, SemesterId = 1, CampusId = classId,
            Status = status, PublishedAt = Clock.Now.AddDays(-daysAgo), ImportedAt = Clock.Now };
        version.TimetableVersionClass.Add(new TimetableVersionClass { ClassId = classId });
        version.TimetableEntry.Add(new TimetableEntry { ClassId = classId, TeacherId = teacherId, SubjectId = 1, PeriodSlotId = 1, DayOfWeek = 2 });
        Db.TimetableVersion.Add(version);
        Db.SaveChanges();
    }

    public void Dispose() => Db.Dispose();
}
