using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Tests.Modules.Sch;

/// <summary>SCH-03: hai năm học không được chồng khoảng thời gian.</summary>
public class AcademicYearTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime Now { get; } = new(2026, 10, 20, 8, 0, 0);
        public DateOnly Today { get; } = new(2026, 10, 20);
    }

    private sealed class FakeAccessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }

    private static CalendarService Create() => CreateWithDb().Service;

    private static (MsmsDbContext Db, CalendarService Service) CreateWithDb()
    {
        var db = new MsmsDbContext(new DbContextOptionsBuilder<MsmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var identity = new ClaimsIdentity(
        [
            new Claim(CurrentUser.AccountIdClaim, "1"), new Claim(CurrentUser.PersonIdClaim, "1"),
            new Claim(CurrentUser.TypeClaim, AccountTypes.SchoolAdmin),
        ], "Test");
        var user = new CurrentUser(new FakeAccessor(new DefaultHttpContext { User = new ClaimsPrincipal(identity) }));
        return (db, new CalendarService(db, user, new FakeClock()));
    }

    private static AcademicYearRequest Year(string name, string start, string end) =>
        new(name, DateOnly.Parse(start), DateOnly.Parse(end));

    [Fact]
    public async Task Create_OverlappingYear_IsRejected()
    {
        var svc = Create();
        await svc.CreateYearAsync(Year("2026-2027", "2026-09-05", "2027-05-31"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.CreateYearAsync(Year("2027-2028", "2026-09-05", "2027-10-05")));

        Assert.Contains("2026-2027", ex.Message);
        Assert.Single(await svc.ListYearsAsync());
    }

    [Fact]
    public async Task Create_YearTouchingByOneDay_IsRejected_AndNextDayIsAccepted()
    {
        var svc = Create();
        await svc.CreateYearAsync(Year("2026-2027", "2026-09-05", "2027-05-31"));

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateYearAsync(Year("2027-2028", "2027-05-31", "2028-05-31")));
        await svc.CreateYearAsync(Year("2027-2028", "2027-06-01", "2028-05-31"));

        Assert.Equal(2, (await svc.ListYearsAsync()).Count);
    }

    [Fact]
    public async Task Update_ToOverlapAnotherYear_IsRejected_ButKeepingOwnRangeIsFine()
    {
        var svc = Create();
        var first = await svc.CreateYearAsync(Year("2026-2027", "2026-09-05", "2027-05-31"));
        var second = await svc.CreateYearAsync(Year("2027-2028", "2027-09-05", "2028-05-31"));

        await svc.UpdateYearAsync(second.AcademicYearId, Year("2027-2028", "2027-09-05", "2028-06-15"));
        await Assert.ThrowsAsync<ApiException>(() => svc.UpdateYearAsync(second.AcademicYearId, Year("2027-2028", "2027-05-01", "2028-05-31")));
        await svc.UpdateYearAsync(first.AcademicYearId, Year("2026-2027", "2026-09-05", "2027-05-31"));
    }

    [Fact]
    public async Task Delete_EmptyYear_RemovesItAndItsSemesters()
    {
        var (db, svc) = CreateWithDb();
        await svc.CreateYearAsync(Year("2026-2027", "2026-09-05", "2027-05-31"));
        var mistake = await svc.CreateYearAsync(Year("2029-2030", "2028-09-05", "2029-05-31"));
        await svc.ConfigureSemestersAsync(mistake.AcademicYearId, new SemestersRequest(
            new SemesterRange(new DateOnly(2028, 9, 5), new DateOnly(2029, 1, 10)),
            new SemesterRange(new DateOnly(2029, 1, 15), new DateOnly(2029, 5, 20))));

        await svc.DeleteYearAsync(mistake.AcademicYearId);

        Assert.Equal("2026-2027", Assert.Single(await svc.ListYearsAsync()).Name);
        Assert.Equal(0, await db.Semester.CountAsync(s => s.AcademicYearId == mistake.AcademicYearId));
    }

    [Fact]
    public async Task Delete_YearWithClass_IsRejected()
    {
        var (db, svc) = CreateWithDb();
        var year = await svc.CreateYearAsync(Year("2026-2027", "2026-09-05", "2027-05-31"));
        db.Campus.Add(new Campus { CampusId = 1, Code = "C1", Name = "Cơ sở 1", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Class.Add(new Class { CampusId = 1, AcademicYearId = year.AcademicYearId, Name = "6A", GradeLevel = 6, IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.DeleteYearAsync(year.AcademicYearId));

        Assert.Contains("1 lớp", ex.Message);
        Assert.Single(await svc.ListYearsAsync());
    }

    [Fact]
    public async Task Delete_CurrentYearWhileOthersExist_IsRejected_ButOnlyYearCanBeDeleted()
    {
        var svc = Create();
        var first = await svc.CreateYearAsync(Year("2026-2027", "2026-09-05", "2027-05-31"));   // năm đầu tiên tự thành hiện hành
        var second = await svc.CreateYearAsync(Year("2027-2028", "2027-09-05", "2028-05-31"));

        await Assert.ThrowsAsync<ApiException>(() => svc.DeleteYearAsync(first.AcademicYearId));
        await svc.DeleteYearAsync(second.AcademicYearId);
        await svc.DeleteYearAsync(first.AcademicYearId);

        Assert.Empty(await svc.ListYearsAsync());
    }

    [Fact]
    public async Task Semesters_EndingAfterMay31OfTheEndYear_AreRejected_ButMay31IsAccepted()
    {
        var svc = Create();
        var year = await svc.CreateYearAsync(Year("2026-2027", "2026-09-05", "2027-06-30"));
        SemestersRequest Request(DateOnly end2) => new(
            new SemesterRange(new DateOnly(2026, 9, 5), new DateOnly(2027, 1, 15)), new SemesterRange(new DateOnly(2027, 1, 18), end2));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.ConfigureSemestersAsync(year.AcademicYearId, Request(new DateOnly(2027, 6, 15))));
        Assert.Contains("31/05/2027", ex.Message);

        var saved = await svc.ConfigureSemestersAsync(year.AcademicYearId, Request(new DateOnly(2027, 5, 31)));
        Assert.Equal(new DateOnly(2027, 5, 31), saved.Semesters.Single(s => s.SemesterNo == 2).EndDate);
    }

    [Fact]
    public async Task Semester1_EndingAfterJan18_IsRejected_ButJan18IsAccepted()
    {
        var svc = Create();
        var year = await svc.CreateYearAsync(Year("2026-2027", "2026-09-05", "2027-05-31"));
        SemestersRequest Request(DateOnly end1) => new(
            new SemesterRange(new DateOnly(2026, 9, 5), end1), new SemesterRange(end1.AddDays(1), new DateOnly(2027, 5, 31)));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.ConfigureSemestersAsync(year.AcademicYearId, Request(new DateOnly(2027, 1, 19))));
        Assert.Contains("18/01/2027", ex.Message);

        var saved = await svc.ConfigureSemestersAsync(year.AcademicYearId, Request(new DateOnly(2027, 1, 18)));
        Assert.Equal(new DateOnly(2027, 1, 19), saved.Semesters.Single(s => s.SemesterNo == 2).StartDate);
    }
}
