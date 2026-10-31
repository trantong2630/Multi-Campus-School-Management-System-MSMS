using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>Sửa năm học không để lại ngày nghỉ nằm ngoài khoảng mới; mốc kết thúc muộn nhất của học kỳ nằm ở một chỗ duy nhất và được trả cho giao diện.</summary>
public class YearRulesTests
{
    private static CalendarService Create()
    {
        var db = NewDb();
        AddCampus(db, 1, "Phân hiệu 1");
        return new CalendarService(db, UserOf(AccountTypes.SchoolAdmin), ClockAt());
    }

    private static AcademicYearRequest Year(string start, string end) => new("2026-2027", DateOnly.Parse(start), DateOnly.Parse(end));

    // ---- Sửa năm học và ngày nghỉ đã khai ----
    [Fact]
    public async Task ShrinkingTheYear_PastADeclaredHoliday_IsRejected_NamingTheCountAndTheEarliestDate()
    {
        var svc = Create();
        var year = await svc.CreateYearAsync(Year("2026-09-05", "2027-05-31"));
        await svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, null, D(2026, 11, 20), "Ngày Nhà giáo"));
        await svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, null, D(2027, 2, 6), "Tết"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.UpdateYearAsync(year.AcademicYearId, Year("2026-09-05", "2026-11-15")));

        Assert.Contains("2 ngày nghỉ", ex.Message);
        Assert.Contains("20/11/2026", ex.Message);
        Assert.Equal(D(2027, 5, 31), (await svc.ListYearsAsync()).Single().EndDate);   // không lưu gì
    }

    [Fact]
    public async Task MovingTheStartPastAHoliday_IsAlsoRejected()
    {
        var svc = Create();
        var year = await svc.CreateYearAsync(Year("2026-09-05", "2027-05-31"));
        await svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, null, D(2026, 9, 7), null));

        await Assert.ThrowsAsync<ApiException>(() => svc.UpdateYearAsync(year.AcademicYearId, Year("2026-09-10", "2027-05-31")));
    }

    [Fact]
    public async Task WideningTheYear_OrKeepingEveryHolidayInside_IsAllowed()
    {
        var svc = Create();
        var year = await svc.CreateYearAsync(Year("2026-09-05", "2027-05-31"));
        await svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, null, D(2026, 11, 20), null));

        await svc.UpdateYearAsync(year.AcademicYearId, Year("2026-09-01", "2027-06-15"));    // rộng hơn
        await svc.UpdateYearAsync(year.AcademicYearId, Year("2026-09-05", "2026-11-25"));    // thu hẹp nhưng vẫn ôm ngày nghỉ 20/11

        Assert.Equal(D(2026, 11, 25), (await svc.ListYearsAsync()).Single().EndDate);
    }

    [Fact]
    public async Task HolidaysOfAnotherYear_DoNotBlockEditingThisOne()
    {
        var svc = Create();
        var first = await svc.CreateYearAsync(Year("2026-09-05", "2027-05-31"));
        var second = await svc.CreateYearAsync(new AcademicYearRequest("2027-2028", D(2027, 9, 5), D(2028, 5, 31)));
        await svc.CreateHolidayAsync(new HolidayRequest(second.AcademicYearId, null, D(2027, 10, 1), null));

        await svc.UpdateYearAsync(first.AcademicYearId, Year("2026-09-05", "2027-04-30"));   // chỉ ngày nghỉ của chính năm này mới tính
    }

    // ---- Mốc kết thúc muộn nhất: một nguồn duy nhất ----
    [Theory]
    [InlineData(1, "2027-05-31", "2027-01-18")]   // học kỳ 1: 18/01
    [InlineData(2, "2027-05-31", "2027-05-31")]   // học kỳ 2: 31/05
    [InlineData(2, "2027-06-30", "2027-05-31")]   // năm học dài hơn thì vẫn chặn ở 31/05
    [InlineData(2, "2027-05-20", "2027-05-20")]   // năm học kết thúc sớm hơn mốc thì lấy ngày kết thúc năm học
    [InlineData(1, "2027-01-10", "2027-01-10")]
    public void LatestEnd_IsTheMarkOrTheYearEnd_WhicheverComesFirst(int semesterNo, string yearEnd, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), SemesterRules.LatestEnd(semesterNo, DateOnly.Parse(yearEnd)));

    [Fact]
    public async Task TheYearListReturnsTheLimits_SoTheUiNeverRecomputesThem()
    {
        var svc = Create();
        await svc.CreateYearAsync(Year("2026-09-05", "2027-05-31"));

        var dto = (await svc.ListYearsAsync()).Single();

        Assert.Equal(D(2027, 1, 18), dto.Semester1LatestEnd);
        Assert.Equal(D(2027, 5, 31), dto.Semester2LatestEnd);
    }
}
