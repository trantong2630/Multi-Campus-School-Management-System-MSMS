using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;

namespace MSMS.Tests.Modules.Sch;

/// <summary>Dựng dữ liệu và ngữ cảnh dùng chung cho test module SCH (không cần DB thật).</summary>
internal static class SchTestSupport
{
    public static readonly DateOnly Today = new(2026, 10, 20);
    public static DateOnly D(int y, int m, int d) => new(y, m, d);

    private sealed class FixedClock(DateOnly today) : IClock
    {
        public DateTime Now { get; } = today.ToDateTime(new TimeOnly(8, 0));
        public DateOnly Today { get; } = today;
    }

    private sealed class FixedAccessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }

    public static IClock ClockAt(DateOnly? today = null) => new FixedClock(today ?? Today);

    public static MsmsDbContext NewDb() => new(new DbContextOptionsBuilder<MsmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    public static CurrentUser UserOf(string accountType, int personId = 900)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(CurrentUser.AccountIdClaim, personId.ToString()), new Claim(CurrentUser.PersonIdClaim, personId.ToString()),
            new Claim(CurrentUser.TypeClaim, accountType),
        ], "Test");
        return new CurrentUser(new FixedAccessor(new DefaultHttpContext { User = new ClaimsPrincipal(identity) }));
    }

    public static Campus AddCampus(MsmsDbContext db, int id, string name, bool active = true)
    {
        var campus = new Campus { CampusId = id, Code = $"PH{id}", Name = name, IsActive = active, CreatedAt = DateTime.UtcNow };
        db.Campus.Add(campus);
        db.SaveChanges();
        return campus;
    }

    public static AcademicYear AddYear(MsmsDbContext db, int id, string name, DateOnly start, DateOnly end, bool current = false)
    {
        var year = new AcademicYear { AcademicYearId = id, Name = name, StartDate = start, EndDate = end, IsCurrent = current };
        db.AcademicYear.Add(year);
        db.SaveChanges();
        return year;
    }

    public static Class AddClass(MsmsDbContext db, int id, int campusId, int yearId, string name, byte grade = 6, bool active = true)
    {
        var cls = new Class { ClassId = id, CampusId = campusId, AcademicYearId = yearId, Name = name, GradeLevel = grade, IsActive = active, CreatedAt = DateTime.UtcNow };
        db.Class.Add(cls);
        db.SaveChanges();
        return cls;
    }

    public static void AddStaff(MsmsDbContext db, int id, string fullName, int campusId = 1)
    {
        db.Person.Add(new Person { PersonId = id, PersonType = "Staff", PersonCode = $"CB{id}", FullName = fullName, CreatedAt = DateTime.UtcNow });
        db.StaffProfile.Add(new StaffProfile { StaffId = id, PrimaryCampusId = campusId, IsActive = true });
        db.SaveChanges();
    }
}
