using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>
/// SCH-03 (4.2.12–4.2.19): năm học hiện hành, hai học kỳ, ngày nghỉ theo kế hoạch (BR-08, BR-15).
/// Quy tắc chồng năm học, mốc kết thúc muộn nhất và xóa năm học nằm ở AcademicYearTests.
/// </summary>
public class CalendarRulesTests
{
    private static (MsmsDbContext Db, CalendarService Service) Create()
    {
        var db = NewDb();
        AddCampus(db, 1, "Phân hiệu 1");
        AddCampus(db, 2, "Phân hiệu 2");
        return (db, new CalendarService(db, UserOf(AccountTypes.SchoolAdmin), ClockAt()));
    }

    private static AcademicYearRequest Year(string name, DateOnly start, DateOnly end) => new(name, start, end);

    private static SemestersRequest Semesters(DateOnly s1, DateOnly e1, DateOnly s2, DateOnly e2) =>
        new(new SemesterRange(s1, e1), new SemesterRange(s2, e2));

    private static async Task<AcademicYearDto> NewYear(CalendarService svc) =>
        await svc.CreateYearAsync(Year("2026-2027", D(2026, 9, 5), D(2027, 5, 31)));

    // ---- Năm học hiện hành (4.2.14) ----
    [Fact]
    public async Task FirstYearBecomesCurrent_AndSetCurrentKeepsExactlyOne()
    {
        var (_, svc) = Create();
        var first = await NewYear(svc);
        var second = await svc.CreateYearAsync(Year("2027-2028", D(2027, 9, 5), D(2028, 5, 31)));
        Assert.True(first.IsCurrent);
        Assert.False(second.IsCurrent);

        await svc.SetCurrentYearAsync(second.AcademicYearId);

        var years = await svc.ListYearsAsync();
        Assert.Equal(second.AcademicYearId, years.Single(y => y.IsCurrent).AcademicYearId);
    }

    [Fact]
    public async Task SetCurrent_UnknownYear_IsNotFound_AndChangesNothing()
    {
        var (_, svc) = Create();
        var first = await NewYear(svc);

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.SetCurrentYearAsync(999));

        Assert.Equal(404, ex.StatusCode);
        Assert.True((await svc.ListYearsAsync()).Single(y => y.AcademicYearId == first.AcademicYearId).IsCurrent);
    }

    [Fact]
    public async Task CreateYear_EndNotAfterStart_OrDuplicateName_IsRejected()
    {
        var (_, svc) = Create();
        await NewYear(svc);

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateYearAsync(Year("2030-2031", D(2030, 9, 5), D(2030, 9, 5))));
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateYearAsync(Year(" 2026-2027 ", D(2028, 9, 5), D(2029, 5, 31))));
    }

    [Fact]
    public async Task UpdateYear_ShrinkingBelowAConfiguredSemester_IsRejected()
    {
        var (_, svc) = Create();
        var year = await NewYear(svc);
        await svc.ConfigureSemestersAsync(year.AcademicYearId, Semesters(D(2026, 9, 5), D(2027, 1, 15), D(2027, 1, 19), D(2027, 5, 31)));

        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            svc.UpdateYearAsync(year.AcademicYearId, Year("2026-2027", D(2026, 9, 5), D(2027, 5, 1))));

        Assert.Contains("Học kỳ 2", ex.Message);
    }

    // ---- Hai học kỳ (4.2.15, 4.2.16) ----
    [Fact]
    public async Task ConfigureSemesters_StoresTwoOrderedSemesters_AndReconfigureReplacesNotAdds()
    {
        var (db, svc) = Create();
        var year = await NewYear(svc);

        var saved = await svc.ConfigureSemestersAsync(year.AcademicYearId, Semesters(D(2026, 9, 5), D(2027, 1, 15), D(2027, 1, 19), D(2027, 5, 31)));
        Assert.Equal([1, 2], saved.Semesters.Select(s => (int)s.SemesterNo));

        var again = await svc.ConfigureSemestersAsync(year.AcademicYearId, Semesters(D(2026, 9, 7), D(2027, 1, 10), D(2027, 1, 12), D(2027, 5, 20)));

        Assert.Equal(2, again.Semesters.Count);
        Assert.Equal(2, db.Semester.Count());
        Assert.Equal(D(2027, 1, 10), again.Semesters[0].EndDate);
        Assert.Equal(D(2027, 5, 20), again.Semesters[1].EndDate);
    }

    [Theory]
    [InlineData("2026-09-05", "2027-01-15", "2027-01-15", "2027-05-31")]   // học kỳ 2 bắt đầu đúng ngày kết thúc học kỳ 1: chồng
    [InlineData("2026-09-05", "2027-01-15", "2027-01-10", "2027-05-31")]   // học kỳ 2 bắt đầu trước khi học kỳ 1 kết thúc
    [InlineData("2027-01-15", "2026-09-05", "2027-01-19", "2027-05-31")]   // học kỳ 1 kết thúc trước ngày bắt đầu
    [InlineData("2026-08-01", "2027-01-15", "2027-01-19", "2027-05-31")]   // học kỳ 1 bắt đầu trước năm học
    public async Task ConfigureSemesters_InvalidRanges_AreRejected(string s1, string e1, string s2, string e2)
    {
        var (db, svc) = Create();
        var year = await NewYear(svc);

        await Assert.ThrowsAsync<ApiException>(() => svc.ConfigureSemestersAsync(year.AcademicYearId,
            Semesters(DateOnly.Parse(s1), DateOnly.Parse(e1), DateOnly.Parse(s2), DateOnly.Parse(e2))));

        Assert.Empty(db.Semester);
    }

    [Fact]
    public async Task ConfigureSemesters_UnknownYear_IsNotFound()
    {
        var (_, svc) = Create();
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            svc.ConfigureSemestersAsync(999, Semesters(D(2026, 9, 5), D(2027, 1, 15), D(2027, 1, 19), D(2027, 5, 31))));
        Assert.Equal(404, ex.StatusCode);
    }

    // ---- Ngày nghỉ (4.2.17–4.2.19) ----
    [Fact]
    public async Task CreateHoliday_WholeSchoolAndPerCampus_AreSeparateScopes()
    {
        var (_, svc) = Create();
        var year = await NewYear(svc);
        var tet = D(2027, 2, 6);

        var whole = await svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, null, tet, "Tết"));
        var campus1 = await svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, 1, tet, "Nghỉ thêm"));

        Assert.Null(whole.CampusId);
        Assert.Equal("Phân hiệu 1", campus1.CampusName);
        Assert.False(whole.IsAdHoc);
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, null, tet, null)));
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, 1, tet, null)));
    }

    [Fact]
    public async Task CreateHoliday_OutsideYear_UnknownCampusOrYear_IsRejected()
    {
        var (_, svc) = Create();
        var year = await NewYear(svc);

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, null, D(2026, 9, 4), null)));
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, null, D(2027, 6, 1), null)));
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, 99, D(2027, 2, 6), null)));
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateHolidayAsync(new HolidayRequest(999, null, D(2027, 2, 6), null)));
    }

    [Fact]
    public async Task ListHolidays_FilteredByCampus_IncludesWholeSchoolButNotOtherCampuses()
    {
        var (_, svc) = Create();
        var year = await NewYear(svc);
        await svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, null, D(2026, 10, 10), "Nghỉ chung"));
        await svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, 1, D(2026, 11, 20), "Riêng PH1"));
        await svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, 2, D(2026, 12, 1), "Riêng PH2"));

        var forCampus1 = await svc.ListHolidaysAsync(year.AcademicYearId, 1);
        var all = await svc.ListHolidaysAsync(year.AcademicYearId, null);

        Assert.Equal(["Nghỉ chung", "Riêng PH1"], forCampus1.Select(h => h.Reason));
        Assert.Equal(3, all.Count);
        Assert.Equal(all.OrderBy(h => h.HolidayDate).Select(h => h.HolidayId), all.Select(h => h.HolidayId));
    }

    [Fact]
    public async Task DeleteHoliday_RemovesIt_AndUnknownIsNotFound()
    {
        var (_, svc) = Create();
        var year = await NewYear(svc);
        var holiday = await svc.CreateHolidayAsync(new HolidayRequest(year.AcademicYearId, null, D(2027, 2, 6), "Tết"));

        await svc.DeleteHolidayAsync(holiday.HolidayId);

        Assert.Empty(await svc.ListHolidaysAsync(year.AcademicYearId, null));
        Assert.Equal(404, (await Assert.ThrowsAsync<ApiException>(() => svc.DeleteHolidayAsync(holiday.HolidayId))).StatusCode);
    }

    [Fact]
    public async Task DeleteYear_WithDeclaredHoliday_IsRejected_UntilTheHolidayIsRemoved()
    {
        var (_, svc) = Create();
        await NewYear(svc);
        var second = await svc.CreateYearAsync(Year("2027-2028", D(2027, 9, 5), D(2028, 5, 31)));
        var holiday = await svc.CreateHolidayAsync(new HolidayRequest(second.AcademicYearId, null, D(2027, 10, 1), null));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.DeleteYearAsync(second.AcademicYearId));
        Assert.Contains("1 ngày nghỉ", ex.Message);

        await svc.DeleteHolidayAsync(holiday.HolidayId);
        await svc.DeleteYearAsync(second.AcademicYearId);
        Assert.Single(await svc.ListYearsAsync());
    }
}
