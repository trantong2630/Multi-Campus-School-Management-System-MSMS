using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

// Các test case của file MSMS_UnitTest_Tu_SCH_PPL.xlsx (phần SCH). Mỗi lớp = một sheet, mỗi test = một UTCID.

/// <summary>Sheet SCH-Classify: SchoolCalendarRules.Classify (SCH-03, BR-08).</summary>
public class SchoolCalendarClassifyUtcTests
{
    private static readonly (DateOnly Start, DateOnly End)[] Semesters =
    [
        (D(2026, 9, 5), D(2027, 1, 15)),
        (D(2027, 1, 18), D(2027, 5, 31)),
    ];

    private static readonly HashSet<DateOnly> Holidays = [D(2026, 11, 20)];

    [Fact] public void UTCID01_Tuesday_InSemester_IsSchoolDay() =>
        Assert.Equal(SchoolDayStatus.SchoolDay, SchoolCalendarRules.Classify(D(2026, 11, 10), Semesters, Holidays));

    [Fact] public void UTCID02_Sunday_IsSunday() =>
        Assert.Equal(SchoolDayStatus.Sunday, SchoolCalendarRules.Classify(D(2026, 11, 15), Semesters, Holidays));

    [Fact] public void UTCID03_HolidayFriday_IsHoliday() =>
        Assert.Equal(SchoolDayStatus.Holiday, SchoolCalendarRules.Classify(D(2026, 11, 20), Semesters, Holidays));

    [Fact] public void UTCID04_BetweenSemesters_IsOutsideSemester() =>
        Assert.Equal(SchoolDayStatus.OutsideSemester, SchoolCalendarRules.Classify(D(2027, 1, 16), Semesters, Holidays));

    [Fact] public void UTCID05_FirstDayOfSemester1_IsSchoolDay() =>
        Assert.Equal(SchoolDayStatus.SchoolDay, SchoolCalendarRules.Classify(D(2026, 9, 5), Semesters, Holidays));

    [Fact] public void UTCID06_LastDayOfSemester2_IsSchoolDay() =>
        Assert.Equal(SchoolDayStatus.SchoolDay, SchoolCalendarRules.Classify(D(2027, 5, 31), Semesters, Holidays));

    [Fact] public void UTCID07_AfterYear_IsOutsideSemester() =>
        Assert.Equal(SchoolDayStatus.OutsideSemester, SchoolCalendarRules.Classify(D(2027, 6, 1), Semesters, Holidays));
}

/// <summary>Sheet SCH-LatestEnd: SemesterRules.LatestEnd (SCH-03).</summary>
public class SemesterLatestEndUtcTests
{
    [Fact] public void UTCID01_Semester1_YearEnd31May_Is18Jan() =>
        Assert.Equal(D(2027, 1, 18), SemesterRules.LatestEnd(1, D(2027, 5, 31)));

    [Fact] public void UTCID02_Semester2_YearEnd31May_Is31May() =>
        Assert.Equal(D(2027, 5, 31), SemesterRules.LatestEnd(2, D(2027, 5, 31)));

    [Fact] public void UTCID03_Semester2_YearEndBefore31May_IsYearEnd() =>
        Assert.Equal(D(2027, 5, 20), SemesterRules.LatestEnd(2, D(2027, 5, 20)));

    [Fact] public void UTCID04_Semester1_YearEndBefore18Jan_IsYearEnd() =>
        Assert.Equal(D(2027, 1, 10), SemesterRules.LatestEnd(1, D(2027, 1, 10)));
}

/// <summary>Sheet SCH-EnrollmentOverlaps: EnrollmentRules.Overlaps (SCH-05, BR-09). Kỳ A = 01/11 → 05/11.</summary>
public class EnrollmentOverlapsUtcTests
{
    private static bool OverlapsA(DateOnly start, DateOnly end) => EnrollmentRules.Overlaps(D(2026, 11, 1), D(2026, 11, 5), start, end);

    [Fact] public void UTCID01_TouchesEnd_Overlaps() => Assert.True(OverlapsA(D(2026, 11, 5), D(2026, 11, 9)));
    [Fact] public void UTCID02_NextDay_DoesNotOverlap() => Assert.False(OverlapsA(D(2026, 11, 6), D(2026, 11, 9)));
    [Fact] public void UTCID03_InsideA_Overlaps() => Assert.True(OverlapsA(D(2026, 11, 2), D(2026, 11, 3)));
    [Fact] public void UTCID04_Identical_Overlaps() => Assert.True(OverlapsA(D(2026, 11, 1), D(2026, 11, 5)));
    [Fact] public void UTCID05_BeforeA_DoesNotOverlap() => Assert.False(OverlapsA(D(2026, 10, 25), D(2026, 10, 31)));
    [Fact] public void UTCID06_ContainsA_Overlaps() => Assert.True(OverlapsA(D(2026, 10, 20), D(2026, 11, 30)));
}

/// <summary>Sheet SCH-CreateCampusAsync: CampusService.CreateAsync (SCH-01). Tiền điều kiện: có phân hiệu PH1 "Phân hiệu 1".</summary>
public class CampusCreateUtcTests
{
    private static async Task<(MsmsDbContext Db, CampusService Service)> CreateAsync()
    {
        var db = NewDb();
        var svc = new CampusService(db, ClockAt());
        await svc.CreateAsync(new CampusRequest("PH1", "Phân hiệu 1", null, null));
        return (db, svc);
    }

    private static async Task<ApiException> Reject(string code, string name, string? phone)
    {
        var (_, svc) = await CreateAsync();
        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(new CampusRequest(code, name, null, phone)));
        Assert.Equal(400, ex.StatusCode);
        return ex;
    }

    [Fact]
    public async Task UTCID01_ValidWithPhone_CreatesActiveCampus()
    {
        var (_, svc) = await CreateAsync();

        var dto = await svc.CreateAsync(new CampusRequest("PH2", "Phân hiệu 2", null, "0912345678"));

        Assert.True(dto.IsActive);
        Assert.Equal("PH2", dto.Code);
        Assert.Equal("Phân hiệu 2", dto.Name);
        Assert.Equal("0912345678", dto.PhoneNumber);
    }

    [Fact]
    public async Task UTCID02_ValidWithoutPhone_CreatesActiveCampus()
    {
        var (_, svc) = await CreateAsync();

        var dto = await svc.CreateAsync(new CampusRequest("PH2", "Phân hiệu 2", null, null));

        Assert.True(dto.IsActive);
        Assert.Null(dto.PhoneNumber);
    }

    [Fact]
    public async Task UTCID03_DuplicateCode_IsRejected() =>
        Assert.Contains("Mã phân hiệu \"PH1\" đã có", (await Reject("PH1", "Phân hiệu 2", "0912345678")).Message);

    [Fact]
    public async Task UTCID04_DuplicateName_IsRejected() =>
        Assert.Contains("Tên phân hiệu \"Phân hiệu 1\" đã có", (await Reject("PH2", "Phân hiệu 1", "0912345678")).Message);

    [Fact]
    public async Task UTCID05_EmptyCode_IsRejected() =>
        Assert.Contains("Mã phân hiệu không được để trống", (await Reject("", "Phân hiệu 2", "0912345678")).Message);

    [Fact]
    public async Task UTCID06_Code21Chars_IsRejected() =>
        Assert.Contains("Mã phân hiệu tối đa 20 ký tự", (await Reject(new string('P', 21), "Phân hiệu 2", "0912345678")).Message);

    [Fact]
    public async Task UTCID07_PhoneWithWrongPrefix_IsRejected() =>
        Assert.Contains("Số điện thoại di động phải gồm đúng 10 chữ số", (await Reject("PH2", "Phân hiệu 2", "0123456789")).Message);
}

/// <summary>Sheet SCH-CreateSubjectAsync: CatalogService.CreateSubjectAsync (SCH-02, BR-07).</summary>
public class SubjectCreateUtcTests
{
    private static (MsmsDbContext Db, CatalogService Service, int ActiveDept, int InactiveDept) Setup()
    {
        var db = NewDb();
        db.SubjectDepartment.Add(new SubjectDepartment { DepartmentId = 1, Name = "Tổ Toán - Tin", IsActive = true });
        db.SubjectDepartment.Add(new SubjectDepartment { DepartmentId = 2, Name = "Tổ cũ", IsActive = false });
        db.Subject.Add(new Subject { SubjectId = 1, DepartmentId = 1, Name = "Toán", Abbreviation = "TOAN", IsActive = true });
        db.SaveChanges();
        return (db, new CatalogService(db, ClockAt()), 1, 2);
    }

    private static async Task<ApiException> Reject(int dept, string name, string abbr)
    {
        var (_, svc, _, _) = Setup();
        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.CreateSubjectAsync(new SubjectRequest(dept, name, abbr)));
        Assert.Equal(400, ex.StatusCode);
        return ex;
    }

    [Fact]
    public async Task UTCID01_Valid_CreatesActiveSubjectWithDepartmentName()
    {
        var (_, svc, dept, _) = Setup();

        var dto = await svc.CreateSubjectAsync(new SubjectRequest(dept, "Tin học", "TIN"));

        Assert.True(dto.IsActive);
        Assert.Equal("Tổ Toán - Tin", dto.DepartmentName);
        Assert.Equal("TIN", dto.Abbreviation);
    }

    [Fact]
    public async Task UTCID02_DuplicateName_IsRejected() =>
        Assert.Contains("Môn học \"Toán\" đã có", (await Reject(1, "Toán", "TIN")).Message);

    [Fact]
    public async Task UTCID03_DuplicateAbbreviation_IsRejected() =>
        Assert.Contains("Viết tắt \"TOAN\" đã được môn khác dùng", (await Reject(1, "Tin học", "TOAN")).Message);

    [Fact]
    public async Task UTCID04_AbbreviationWithDash_IsRejected() =>
        Assert.Contains("Viết tắt môn chỉ gồm chữ và số", (await Reject(1, "Tin học", "T-IN")).Message);

    [Fact]
    public async Task UTCID05_Abbreviation10Chars_IsAccepted()
    {
        var (_, svc, dept, _) = Setup();

        var dto = await svc.CreateSubjectAsync(new SubjectRequest(dept, "Tin học", "ABCDEFGHIJ"));

        Assert.True(dto.IsActive);
        Assert.Equal("ABCDEFGHIJ", dto.Abbreviation);
    }

    [Fact]
    public async Task UTCID06_Abbreviation11Chars_IsRejected() =>
        Assert.Contains("Viết tắt môn tối đa 10 ký tự", (await Reject(1, "Tin học", "ABCDEFGHIJK")).Message);

    [Fact]
    public async Task UTCID07_UnknownDepartment_IsRejected() =>
        Assert.Contains("Chọn tổ chuyên môn cho môn học.", (await Reject(999, "Tin học", "TIN")).Message);

    [Fact]
    public async Task UTCID08_InactiveDepartment_IsRejected() =>
        Assert.Contains("Tổ chuyên môn đã ngừng hoạt động", (await Reject(2, "Tin học", "TIN")).Message);
}

/// <summary>Sheet SCH-CreatePeriodSlotAsync: PeriodSlotService.CreateAsync (SCH-04).</summary>
public class PeriodSlotCreateUtcTests
{
    private static TimeOnly T(string s) => TimeOnly.Parse(s);

    /// <summary>Sáng 1–5 (07:00–11:20) và Chiều 1–5 (13:30–17:45) đang hoạt động.</summary>
    private static (MsmsDbContext Db, PeriodSlotService Service) Setup()
    {
        var db = NewDb();
        string[][] morning = [["07:00", "07:45"], ["07:50", "08:35"], ["08:45", "09:30"], ["09:45", "10:30"], ["10:35", "11:20"]];
        string[][] afternoon = [["13:30", "14:15"], ["14:20", "15:05"], ["15:15", "16:00"], ["16:10", "16:55"], ["17:00", "17:45"]];
        for (var i = 0; i < 5; i++)
        {
            db.PeriodSlot.Add(new PeriodSlot { SessionOfDay = SessionOfDay.Morning, PeriodNo = (byte)(i + 1), StartTime = T(morning[i][0]), EndTime = T(morning[i][1]), IsActive = true });
            db.PeriodSlot.Add(new PeriodSlot { SessionOfDay = SessionOfDay.Afternoon, PeriodNo = (byte)(i + 1), StartTime = T(afternoon[i][0]), EndTime = T(afternoon[i][1]), IsActive = true });
        }
        db.SaveChanges();
        return (db, new PeriodSlotService(db));
    }

    private static async Task<ApiException> Reject(string session, byte period, string start, string end)
    {
        var (_, svc) = Setup();
        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(new PeriodSlotRequest(session, period, T(start), T(end))));
        Assert.Equal(400, ex.StatusCode);
        return ex;
    }

    [Fact]
    public async Task UTCID01_Afternoon6_AfterLastSlot_IsCreatedActive()
    {
        var (_, svc) = Setup();

        var dto = await svc.CreateAsync(new PeriodSlotRequest(SessionOfDay.Afternoon, 6, T("17:50"), T("18:35")));

        Assert.True(dto.IsActive);
        Assert.Equal(6, dto.PeriodNo);
    }

    [Fact]
    public async Task UTCID02_EveningSession_IsRejected() =>
        Assert.Contains("Buổi chỉ có Sáng hoặc Chiều.", (await Reject("Evening", 6, "17:50", "18:35")).Message);

    [Fact]
    public async Task UTCID03_Period0_IsRejected() =>
        Assert.Contains("Số tiết phải từ 1 đến 20.", (await Reject(SessionOfDay.Afternoon, 0, "17:50", "18:35")).Message);

    [Fact]
    public async Task UTCID04_Period21_IsRejected() =>
        Assert.Contains("Số tiết phải từ 1 đến 20.", (await Reject(SessionOfDay.Afternoon, 21, "17:50", "18:35")).Message);

    [Fact]
    public async Task UTCID05_Period20_IsCreatedActive()
    {
        var (_, svc) = Setup();

        var dto = await svc.CreateAsync(new PeriodSlotRequest(SessionOfDay.Afternoon, 20, T("19:00"), T("19:45")));

        Assert.True(dto.IsActive);
        Assert.Equal(20, dto.PeriodNo);
    }

    [Fact]
    public async Task UTCID06_EndEqualsStart_IsRejected() =>
        Assert.Contains("Giờ kết thúc phải sau giờ bắt đầu.", (await Reject(SessionOfDay.Afternoon, 6, "18:00", "18:00")).Message);

    [Fact]
    public async Task UTCID07_MorningPeriod1AlreadyExists_IsRejected() =>
        Assert.Contains("tiết 1 đã có", (await Reject(SessionOfDay.Morning, 1, "17:50", "18:35")).Message);

    [Fact]
    public async Task UTCID08_OverlapsAfternoonPeriod5_IsRejected() =>
        Assert.Contains("chồng hoặc sai thứ tự với khung", (await Reject(SessionOfDay.Afternoon, 6, "17:30", "18:15")).Message);
}

/// <summary>Sheet SCH-NextGrade: RolloverRules.NextGrade (SCH-06).</summary>
public class RolloverNextGradeUtcTests
{
    [Fact] public void UTCID01_Grade6_Is7() => Assert.Equal((byte)7, RolloverRules.NextGrade(6));
    [Fact] public void UTCID02_Grade7_Is8() => Assert.Equal((byte)8, RolloverRules.NextGrade(7));
    [Fact] public void UTCID03_Grade8_Is9() => Assert.Equal((byte)9, RolloverRules.NextGrade(8));
    [Fact] public void UTCID04_Grade9_Graduates_ReturnsNull() => Assert.Null(RolloverRules.NextGrade(9));
}

/// <summary>Sheet SCH-SuggestName: RolloverRules.SuggestName (SCH-06).</summary>
public class RolloverSuggestNameUtcTests
{
    [Fact] public void UTCID01_6A1_To7() => Assert.Equal("7A1", RolloverRules.SuggestName("6A1", 6, 7));
    [Fact] public void UTCID02_Lop6B_To7() => Assert.Equal("Lớp 7B", RolloverRules.SuggestName("Lớp 6B", 6, 7));
    [Fact] public void UTCID03_8A2_To9() => Assert.Equal("9A2", RolloverRules.SuggestName("8A2", 8, 9));
    [Fact] public void UTCID04_6A6_OnlyFirstGradeNumberReplaced() => Assert.Equal("7A6", RolloverRules.SuggestName("6A6", 6, 7));
    [Fact] public void UTCID05_16A_GradeNumberInsideLargerNumber_Unchanged() => Assert.Equal("16A", RolloverRules.SuggestName("16A", 6, 7));
    [Fact] public void UTCID06_NameWithoutGradeNumber_Unchanged() => Assert.Equal("Chuyên Toán", RolloverRules.SuggestName("Chuyên Toán", 6, 7));
}

/// <summary>Sheet SCH-CreateClassAsync: ClassService.CreateAsync (SCH-05). Người gọi là School Admin; PH1 active, năm 2026-2027, PH9 inactive.</summary>
public class ClassCreateUtcTests
{
    private const int Ph1 = 1, Ph9 = 9, Year = 1;

    private static async Task<(MsmsDbContext Db, ClassService Service)> SetupAsync()
    {
        var db = NewDb();
        AddCampus(db, Ph1, "Phân hiệu 1");
        AddCampus(db, Ph9, "Phân hiệu 9", active: false);
        AddYear(db, Year, "2026-2027", D(2026, 9, 5), D(2027, 5, 31), current: true);
        var svc = new ClassService(db, ClockAt());
        await svc.CreateAsync(new ClassRequest(Ph1, Year, "6A1", 6));
        return (db, svc);
    }

    private static async Task<ApiException> Reject(int campus, int year, string name, byte grade)
    {
        var (_, svc) = await SetupAsync();
        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(new ClassRequest(campus, year, name, grade)));
        Assert.Equal(400, ex.StatusCode);
        return ex;
    }

    [Fact]
    public async Task UTCID01_Grade6_CreatesActiveClassWithNoStudents()
    {
        var (_, svc) = await SetupAsync();

        var dto = await svc.CreateAsync(new ClassRequest(Ph1, Year, "6A9", 6));

        Assert.True(dto.IsActive);
        Assert.Equal(0, dto.StudentCount);
        Assert.Equal("6A9", dto.Name);
    }

    [Fact]
    public async Task UTCID02_Grade9_CreatesClassWithGrade9()
    {
        var (_, svc) = await SetupAsync();

        var dto = await svc.CreateAsync(new ClassRequest(Ph1, Year, "9B9", 9));

        Assert.Equal((byte)9, dto.GradeLevel);
    }

    [Fact]
    public async Task UTCID03_BlankName_IsRejected() =>
        Assert.Contains("Tên lớp không được để trống", (await Reject(Ph1, Year, "   ", 6)).Message);

    [Fact]
    public async Task UTCID04_Name51Chars_IsRejected() =>
        Assert.Contains("Tên lớp tối đa 50 ký tự", (await Reject(Ph1, Year, new string('A', 51), 6)).Message);

    [Fact]
    public async Task UTCID05_Grade5_IsRejected() =>
        Assert.Contains("Khối chỉ từ 6 đến 9.", (await Reject(Ph1, Year, "5A1", 5)).Message);

    [Fact]
    public async Task UTCID06_Grade10_IsRejected() =>
        Assert.Contains("Khối chỉ từ 6 đến 9.", (await Reject(Ph1, Year, "10A1", 10)).Message);

    [Fact]
    public async Task UTCID07_DuplicateNameInCampusAndYear_IsRejected()
    {
        var message = (await Reject(Ph1, Year, "6A1", 6)).Message;

        Assert.Contains("Lớp \"6A1\" đã có ở", message);
        Assert.Contains("Tên lớp phải duy nhất trong phạm vi đó.", message);
    }

    [Fact]
    public async Task UTCID08_UnknownCampus_IsRejected() =>
        Assert.Contains("Chọn phân hiệu cho lớp.", (await Reject(999, Year, "6A8", 6)).Message);

    [Fact]
    public async Task UTCID09_InactiveCampus_IsRejected() =>
        Assert.Contains("Phân hiệu đã ngừng hoạt động. Chọn phân hiệu khác.", (await Reject(Ph9, Year, "6A8", 6)).Message);

    [Fact]
    public async Task UTCID10_UnknownYear_IsRejected() =>
        Assert.Contains("Chọn năm học cho lớp.", (await Reject(Ph1, 999, "6A8", 6)).Message);
}
