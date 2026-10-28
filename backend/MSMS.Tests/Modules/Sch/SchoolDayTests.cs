using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>4.2.20: ngày dạy của phân hiệu = trong một học kỳ, không phải Chủ nhật, không trùng ngày nghỉ toàn trường hay của phân hiệu.</summary>
public class SchoolDayTests
{
    // 2026-10-26 là Thứ Hai. Học kỳ 1: 05/09/2026 – 10/01/2027, học kỳ 2: 19/01/2027 – 31/05/2027 (có khoảng trống giữa hai kỳ).
    private static readonly (DateOnly Start, DateOnly End)[] Semesters = [(D(2026, 9, 5), D(2027, 1, 10)), (D(2027, 1, 19), D(2027, 5, 31))];
    private static readonly HashSet<DateOnly> NoHolidays = [];
    private static readonly DateOnly Monday = D(2026, 10, 26);

    // ---- Quy tắc thuần ----
    [Fact]
    public void WeekdayAndSaturdayInsideASemester_AreSchoolDays()
    {
        Assert.Equal(SchoolDayStatus.SchoolDay, SchoolCalendarRules.Classify(Monday, Semesters, NoHolidays));
        Assert.Equal(SchoolDayStatus.SchoolDay, SchoolCalendarRules.Classify(Monday.AddDays(-2), Semesters, NoHolidays));   // Thứ 7 có học
    }

    [Fact]
    public void Sunday_IsNeverASchoolDay()
    {
        Assert.Equal(SchoolDayStatus.Sunday, SchoolCalendarRules.Classify(Monday.AddDays(-1), Semesters, NoHolidays));
    }

    [Fact]
    public void ADeclaredHoliday_IsNotASchoolDay()
    {
        HashSet<DateOnly> holidays = [Monday];

        Assert.Equal(SchoolDayStatus.Holiday, SchoolCalendarRules.Classify(Monday, Semesters, holidays));
        Assert.Equal(SchoolDayStatus.SchoolDay, SchoolCalendarRules.Classify(Monday.AddDays(1), Semesters, holidays));
    }

    [Fact]
    public void SemesterBoundaries_AreInclusive()
    {
        Assert.Equal(SchoolDayStatus.SchoolDay, SchoolCalendarRules.Classify(D(2026, 9, 5), Semesters, NoHolidays));    // ngày đầu kỳ 1 (Thứ 7)
        Assert.Equal(SchoolDayStatus.SchoolDay, SchoolCalendarRules.Classify(D(2027, 1, 19), Semesters, NoHolidays));   // ngày đầu kỳ 2
        Assert.Equal(SchoolDayStatus.SchoolDay, SchoolCalendarRules.Classify(D(2027, 5, 31), Semesters, NoHolidays));   // ngày cuối kỳ 2
        Assert.Equal(SchoolDayStatus.OutsideSemester, SchoolCalendarRules.Classify(D(2026, 9, 4), Semesters, NoHolidays));
        Assert.Equal(SchoolDayStatus.OutsideSemester, SchoolCalendarRules.Classify(D(2027, 6, 1), Semesters, NoHolidays));
    }

    [Fact]
    public void TheGapBetweenTwoSemesters_IsNotASchoolDay()
    {
        Assert.Equal(SchoolDayStatus.OutsideSemester, SchoolCalendarRules.Classify(D(2027, 1, 14), Semesters, NoHolidays));
    }

    [Fact]
    public void OutsideASemester_TakesPriorityOverSundayAndHoliday()
    {
        var sundayInSummer = D(2027, 7, 4);   // Chủ nhật, ngoài học kỳ
        HashSet<DateOnly> holidays = [sundayInSummer];

        Assert.Equal(SchoolDayStatus.OutsideSemester, SchoolCalendarRules.Classify(sundayInSummer, Semesters, holidays));
    }

    [Fact]
    public void SchoolDays_ListsOnlyTeachingDaysInTheRange()
    {
        HashSet<DateOnly> holidays = [D(2026, 10, 28)];   // Thứ Tư

        var days = SchoolCalendarRules.SchoolDays(Monday, Monday.AddDays(6), Semesters, holidays).ToList();

        Assert.Equal([D(2026, 10, 26), D(2026, 10, 27), D(2026, 10, 29), D(2026, 10, 30), D(2026, 10, 31)], days);   // bỏ Thứ Tư nghỉ và Chủ nhật
    }

    // ---- Service tra cứu theo DB ----
    private static (MsmsDbContext Db, SchoolDayService Service) Create()
    {
        var db = NewDb();
        AddCampus(db, 1, "Phân hiệu 1");
        AddCampus(db, 2, "Phân hiệu 2");
        AddYear(db, 1, "2026-2027", D(2026, 9, 5), D(2027, 5, 31), current: true);
        db.Semester.AddRange(
            new Semester { AcademicYearId = 1, SemesterNo = 1, Name = "Học kỳ 1", StartDate = Semesters[0].Start, EndDate = Semesters[0].End },
            new Semester { AcademicYearId = 1, SemesterNo = 2, Name = "Học kỳ 2", StartDate = Semesters[1].Start, EndDate = Semesters[1].End });
        db.SaveChanges();
        return (db, new SchoolDayService(db));
    }

    private static Holiday HolidayOn(DateOnly date, int? campusId) =>
        new() { AcademicYearId = 1, CampusId = campusId, HolidayDate = date, CreatedAt = DateTime.UtcNow };

    [Fact]
    public async Task WholeSchoolHoliday_AppliesToEveryCampus()
    {
        var (db, svc) = Create();
        db.Holiday.Add(HolidayOn(Monday, null));
        await db.SaveChangesAsync();

        Assert.Equal(SchoolDayStatus.Holiday, await svc.StatusAsync(1, Monday));
        Assert.Equal(SchoolDayStatus.Holiday, await svc.StatusAsync(2, Monday));
    }

    [Fact]
    public async Task CampusHoliday_AppliesOnlyToThatCampus()
    {
        var (db, svc) = Create();
        db.Holiday.Add(HolidayOn(Monday, 1));
        await db.SaveChangesAsync();

        Assert.False(await svc.IsSchoolDayAsync(1, Monday));
        Assert.True(await svc.IsSchoolDayAsync(2, Monday));
    }

    [Fact]
    public async Task SchoolDays_ForACampus_SkipItsOwnAndWholeSchoolHolidaysButNotOthers()
    {
        var (db, svc) = Create();
        db.Holiday.AddRange(
            HolidayOn(D(2026, 10, 27), null),   // toàn trường
            HolidayOn(D(2026, 10, 28), 1),      // riêng phân hiệu 1
            HolidayOn(D(2026, 10, 29), 2));     // riêng phân hiệu 2
        await db.SaveChangesAsync();

        var campus1 = await svc.SchoolDaysAsync(1, Monday, Monday.AddDays(5));
        var campus2 = await svc.SchoolDaysAsync(2, Monday, Monday.AddDays(5));

        Assert.Equal([D(2026, 10, 26), D(2026, 10, 29), D(2026, 10, 30), D(2026, 10, 31)], campus1);
        Assert.Equal([D(2026, 10, 26), D(2026, 10, 28), D(2026, 10, 30), D(2026, 10, 31)], campus2);
    }

    [Fact]
    public async Task OutsideSemestersAndSundays_AreReportedWithTheirReason()
    {
        var (_, svc) = Create();

        Assert.Equal(SchoolDayStatus.OutsideSemester, await svc.StatusAsync(1, D(2027, 1, 14)));
        Assert.Equal(SchoolDayStatus.OutsideSemester, await svc.StatusAsync(1, D(2027, 7, 1)));
        Assert.Equal(SchoolDayStatus.Sunday, await svc.StatusAsync(1, D(2026, 10, 25)));
    }

    [Fact]
    public async Task SchoolDays_WithReversedRange_IsEmpty()
    {
        var (_, svc) = Create();
        Assert.Empty(await svc.SchoolDaysAsync(1, Monday.AddDays(3), Monday));
    }

    [Fact]
    public async Task UnknownCampus_IsNotFound_InsteadOfSilentlyAnswering()
    {
        var (_, svc) = Create();

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.IsSchoolDayAsync(99, Monday));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task SemestersOfDifferentAcademicYears_AreBothRecognised()
    {
        var (db, svc) = Create();
        AddYear(db, 2, "2027-2028", D(2027, 9, 5), D(2028, 5, 31));
        db.Semester.Add(new Semester { AcademicYearId = 2, SemesterNo = 1, Name = "Học kỳ 1", StartDate = D(2027, 9, 5), EndDate = D(2028, 1, 18) });
        await db.SaveChangesAsync();

        Assert.True(await svc.IsSchoolDayAsync(1, D(2027, 9, 6)));          // Thứ Hai trong kỳ 1 năm sau
        Assert.False(await svc.IsSchoolDayAsync(1, D(2027, 8, 30)));        // hè giữa hai năm học
    }
}
