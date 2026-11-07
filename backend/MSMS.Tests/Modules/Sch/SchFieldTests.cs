using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Tests.Modules.Sch;

/// <summary>Môi trường test SCH. Hôm nay = 20/10/2026. Năm học 2026-2027 (01/09/2026 - 31/05/2027), phân hiệu 1 hoạt động và 2 ngừng, tổ 1 hoạt động và 2 ngừng.</summary>
internal sealed class SchEnv : IDisposable
{
    public static readonly DateOnly Start = new(2026, 9, 1), End = new(2027, 5, 31);
    public MsmsDbContext Db { get; } = SchTestSupport.NewDb();
    public IClock Clock { get; } = SchTestSupport.ClockAt();
    public CurrentUser Admin { get; } = SchTestSupport.UserOf(AccountTypes.SchoolAdmin, 900);

    public CampusService Campuses => new(Db, Clock);
    public CatalogService Catalog => new(Db, Clock);
    public CalendarService Calendar => new(Db, Admin, Clock);
    public ClassService Classes => new(Db, Clock);
    public PeriodSlotService Slots => new(Db);

    public SchEnv()
    {
        SchTestSupport.AddCampus(Db, 1, "Cơ sở 1");
        SchTestSupport.AddCampus(Db, 2, "Cơ sở cũ", active: false);
        SchTestSupport.AddYear(Db, 1, "2026-2027", Start, End, current: true);
        Db.SubjectDepartment.AddRange(new SubjectDepartment { DepartmentId = 1, Name = "Tổ 1", IsActive = true }, new SubjectDepartment { DepartmentId = 2, Name = "Tổ cũ", IsActive = false });
        Db.SaveChanges();
    }

    public static string Text(int n) => new('a', n);
    public static async Task Rejects(Func<Task> act, string? part = null)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(act);
        Assert.Equal(400, ex.StatusCode);
        if (part is not null) Assert.Contains(part, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
    public void Dispose() => Db.Dispose();
}

/// <summary>Thiết lập trường: kiểm từng trường của phân hiệu, tổ chuyên môn và môn học (SCH-01, SCH-02).</summary>
public class SchCatalogFieldTests
{
    private static CampusRequest Campus(string code = "PH9", string name = "Phân hiệu 9", string? addr = null, string? phone = null) => new(code, name, addr, phone);

    [Theory(DisplayName = "SCH | Phân hiệu | Mã phân hiệu | để trống hoặc chỉ khoảng trắng bị từ chối")]
    [InlineData("")] [InlineData("   ")] [InlineData(null)]
    public async Task Campus_Code_Required(string? code)
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Campuses.CreateAsync(Campus(code!)), "Mã phân hiệu");
    }

    [Fact(DisplayName = "SCH | Phân hiệu | Mã phân hiệu | đúng 20 ký tự được chấp nhận, 21 bị từ chối, trùng mã bị từ chối")]
    public async Task Campus_Code_Boundary_AndUnique()
    {
        using var e = new SchEnv();
        Assert.Equal(20, (await e.Campuses.CreateAsync(Campus(code: SchEnv.Text(20)))).Code.Length);
        await SchEnv.Rejects(() => e.Campuses.CreateAsync(Campus(code: SchEnv.Text(21), name: "Tên khác")), "20");
        await SchEnv.Rejects(() => e.Campuses.CreateAsync(Campus(code: SchEnv.Text(20), name: "Tên khác nữa")), "đã có");
    }

    [Theory(DisplayName = "SCH | Phân hiệu | Tên phân hiệu | để trống hoặc chỉ khoảng trắng bị từ chối")]
    [InlineData("")] [InlineData("  ")] [InlineData(null)]
    public async Task Campus_Name_Required(string? name)
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Campuses.CreateAsync(Campus(name: name!)), "Tên phân hiệu");
    }

    [Fact(DisplayName = "SCH | Phân hiệu | Tên phân hiệu | đúng 150 ký tự được chấp nhận, 151 bị từ chối, trùng tên bị từ chối")]
    public async Task Campus_Name_Boundary_AndUnique()
    {
        using var e = new SchEnv();
        Assert.Equal(150, (await e.Campuses.CreateAsync(Campus(name: SchEnv.Text(150)))).Name.Length);
        await SchEnv.Rejects(() => e.Campuses.CreateAsync(Campus(code: "PH10", name: SchEnv.Text(151))), "150");
        await SchEnv.Rejects(() => e.Campuses.CreateAsync(Campus(code: "PH11", name: SchEnv.Text(150))), "đã có");
    }

    [Fact(DisplayName = "SCH | Phân hiệu | Địa chỉ và Số điện thoại | địa chỉ 255/256 ký tự, SĐT phải đúng 10 chữ số bắt đầu bằng 0")]
    public async Task Campus_Address_Phone_Boundaries()
    {
        using var e = new SchEnv();
        var ok = await e.Campuses.CreateAsync(Campus(addr: SchEnv.Text(255), phone: "0912345678"));
        Assert.Equal(255, ok.Address!.Length); Assert.Equal("0912345678", ok.PhoneNumber);
        await SchEnv.Rejects(() => e.Campuses.CreateAsync(Campus("PH10", "Tên 10", SchEnv.Text(256))), "255");
        await SchEnv.Rejects(() => e.Campuses.CreateAsync(Campus("PH11", "Tên 11", null, "091234567")), "10 chữ số");
        await SchEnv.Rejects(() => e.Campuses.CreateAsync(Campus("PH12", "Tên 12", null, "abc-xyz")), "10 chữ số");
    }

    [Fact(DisplayName = "SCH | Phân hiệu | Sửa | kiểm cùng các giới hạn như khi tạo, phân hiệu không tồn tại trả 404")]
    public async Task Campus_Update_Validates()
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Campuses.UpdateAsync(1, Campus(code: SchEnv.Text(21))), "20");
        await SchEnv.Rejects(() => e.Campuses.UpdateAsync(1, Campus(name: "")), "Tên phân hiệu");
        await SchEnv.Rejects(() => e.Campuses.UpdateAsync(1, Campus(addr: SchEnv.Text(256))), "255");
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Campuses.UpdateAsync(999, Campus()));
        Assert.Equal(404, ex.StatusCode);
    }

    // ---------- Tổ chuyên môn ----------
    [Theory(DisplayName = "SCH | Tổ chuyên môn | Tên tổ | để trống bị từ chối, đúng 150 ký tự được chấp nhận, 151 bị từ chối")]
    [InlineData("", false)] [InlineData("   ", false)] [InlineData(null, false)] [InlineData("150", true)] [InlineData("151", false)]
    public async Task Department_Name(string? name, bool accepted)
    {
        using var e = new SchEnv();
        var value = name is "150" or "151" ? SchEnv.Text(int.Parse(name)) : name!;
        if (accepted) Assert.Equal(150, (await e.Catalog.CreateDepartmentAsync(new DepartmentRequest(value, null))).Name.Length);
        else await SchEnv.Rejects(() => e.Catalog.CreateDepartmentAsync(new DepartmentRequest(value, null)));
    }

    [Fact(DisplayName = "SCH | Tổ chuyên môn | Mô tả | đúng 255 ký tự được chấp nhận, 256 bị từ chối, để trống được; trùng tên bị từ chối")]
    public async Task Department_Description_AndUnique()
    {
        using var e = new SchEnv();
        Assert.Equal(255, (await e.Catalog.CreateDepartmentAsync(new DepartmentRequest("Tổ mới", SchEnv.Text(255)))).Description!.Length);
        await SchEnv.Rejects(() => e.Catalog.CreateDepartmentAsync(new DepartmentRequest("Tổ khác", SchEnv.Text(256))), "255");
        Assert.Null((await e.Catalog.CreateDepartmentAsync(new DepartmentRequest("Tổ không mô tả", null))).Description);
        await SchEnv.Rejects(() => e.Catalog.CreateDepartmentAsync(new DepartmentRequest("Tổ 1", null)), "đã có");
    }

    // ---------- Môn học ----------
    private static SubjectRequest Subject(string name = "Toán", string abbr = "TOAN", int dept = 1) => new(dept, name, abbr);

    [Theory(DisplayName = "SCH | Môn học | Tên môn | để trống bị từ chối")]
    [InlineData("")] [InlineData("  ")] [InlineData(null)]
    public async Task Subject_Name_Required(string? name)
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Catalog.CreateSubjectAsync(Subject(name!)), "Tên môn học");
    }

    [Fact(DisplayName = "SCH | Môn học | Tên môn | đúng 150 ký tự được chấp nhận, 151 bị từ chối, trùng tên bị từ chối")]
    public async Task Subject_Name_Boundary_AndUnique()
    {
        using var e = new SchEnv();
        Assert.Equal(150, (await e.Catalog.CreateSubjectAsync(Subject(SchEnv.Text(150), "A1"))).Name.Length);
        await SchEnv.Rejects(() => e.Catalog.CreateSubjectAsync(Subject(SchEnv.Text(151), "A2")), "150");
        await SchEnv.Rejects(() => e.Catalog.CreateSubjectAsync(Subject(SchEnv.Text(150), "A3")), "đã có");
    }

    [Theory(DisplayName = "SCH | Môn học | Viết tắt | chữ và số ASCII hợp lệ được chấp nhận")]
    [InlineData("A")] [InlineData("TOAN")] [InlineData("toan")] [InlineData("T0AN1")] [InlineData("1234567890")]
    public async Task Subject_Abbr_Valid(string abbr)
    {
        using var e = new SchEnv();
        Assert.Equal(abbr, (await e.Catalog.CreateSubjectAsync(Subject("Môn x", abbr))).Abbreviation);
    }

    [Theory(DisplayName = "SCH | Môn học | Viết tắt | trống, có dấu cách, gạch, dấu tiếng Việt hoặc quá 10 ký tự bị từ chối")]
    [InlineData("")] [InlineData("  ")] [InlineData("TO AN")] [InlineData("TOAN-1")] [InlineData("TOÁN")] [InlineData("TO_AN")] [InlineData("12345678901")]
    public async Task Subject_Abbr_Invalid(string abbr)
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Catalog.CreateSubjectAsync(Subject("Môn y", abbr)), "Viết tắt");
    }

    [Fact(DisplayName = "SCH | Môn học | Viết tắt | trùng viết tắt của môn khác bị từ chối")]
    public async Task Subject_Abbr_Unique()
    {
        using var e = new SchEnv();
        await e.Catalog.CreateSubjectAsync(Subject("Toán", "TOAN"));
        await SchEnv.Rejects(() => e.Catalog.CreateSubjectAsync(Subject("Toán nâng cao", "TOAN")), "duy nhất");
    }

    [Theory(DisplayName = "SCH | Môn học | Tổ chuyên môn | không chọn, không tồn tại hoặc đã ngừng hoạt động bị từ chối")]
    [InlineData(0)] [InlineData(999)] [InlineData(2)]
    public async Task Subject_Department_Invalid(int dept)
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Catalog.CreateSubjectAsync(Subject(dept: dept)), "tổ chuyên môn");
    }

    [Fact(DisplayName = "SCH | Môn học | Sửa | không tồn tại trả 404; đổi sang viết tắt của môn khác bị từ chối")]
    public async Task Subject_Update()
    {
        using var e = new SchEnv();
        var a = await e.Catalog.CreateSubjectAsync(Subject("Toán", "TOAN"));
        await e.Catalog.CreateSubjectAsync(Subject("Văn", "VAN"));
        await SchEnv.Rejects(() => e.Catalog.UpdateSubjectAsync(a.SubjectId, Subject("Toán", "VAN")), "duy nhất");
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Catalog.UpdateSubjectAsync(9999, Subject()));
        Assert.Equal(404, ex.StatusCode);
    }
}

/// <summary>Thiết lập trường: kiểm từng trường của năm học, học kỳ, ngày nghỉ và khung giờ tiết (SCH-03, SCH-04).</summary>
public class SchCalendarFieldTests
{
    private static AcademicYearRequest Year(string name = "2027-2028", DateOnly? s = null, DateOnly? en = null) => new(name, s ?? new(2027, 9, 1), en ?? new(2028, 5, 31));

    [Theory(DisplayName = "SCH | Năm học | Tên năm học | để trống bị từ chối")]
    [InlineData("")] [InlineData("   ")] [InlineData(null)]
    public async Task Year_Name_Required(string? name)
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Calendar.CreateYearAsync(Year(name!)), "Tên năm học");
    }

    [Fact(DisplayName = "SCH | Năm học | Tên năm học | đúng 20 ký tự được chấp nhận, 21 bị từ chối, trùng tên bị từ chối")]
    public async Task Year_Name_Boundary_AndUnique()
    {
        using var e = new SchEnv();
        Assert.Equal(20, (await e.Calendar.CreateYearAsync(Year(SchEnv.Text(20)))).Name.Length);
        await SchEnv.Rejects(() => e.Calendar.CreateYearAsync(Year(SchEnv.Text(21), new(2030, 9, 1), new(2031, 5, 31))), "20");
        await SchEnv.Rejects(() => e.Calendar.CreateYearAsync(Year(SchEnv.Text(20), new(2030, 9, 1), new(2031, 5, 31))), "đã có");
    }

    [Theory(DisplayName = "SCH | Năm học | Ngày bắt đầu và kết thúc | ngày kết thúc trùng hoặc trước ngày bắt đầu bị từ chối")]
    [InlineData(0)] [InlineData(-1)] [InlineData(-300)]
    public async Task Year_End_NotAfterStart_Rejected(int endOffsetDays)
    {
        using var e = new SchEnv();
        var s = new DateOnly(2027, 9, 1);
        await SchEnv.Rejects(() => e.Calendar.CreateYearAsync(Year(en: s.AddDays(endOffsetDays))), "sau ngày bắt đầu");
    }

    [Fact(DisplayName = "SCH | Năm học | Ngày bắt đầu và kết thúc | kết thúc đúng 1 ngày sau bắt đầu được chấp nhận; dài đúng 13 tháng được chấp nhận, quá 13 tháng bị từ chối")]
    public async Task Year_Range_Boundaries()
    {
        using var e = new SchEnv();
        Assert.NotNull(await e.Calendar.CreateYearAsync(Year("Ngắn", new(2029, 9, 1), new(2029, 9, 2))));
        Assert.NotNull(await e.Calendar.CreateYearAsync(Year("Dài", new(2030, 1, 1), new(2031, 2, 1))));
        await SchEnv.Rejects(() => e.Calendar.CreateYearAsync(Year("Quá dài", new(2033, 1, 1), new(2034, 2, 2))), "13 tháng");
    }

    [Theory(DisplayName = "SCH | Năm học | Khoảng thời gian | chồng khoảng của năm học khác (kể cả chạm đúng một ngày) bị từ chối")]
    [InlineData(2026, 12, 1, 2027, 1, 1)] [InlineData(2027, 5, 31, 2027, 8, 1)] [InlineData(2026, 1, 1, 2026, 9, 1)] [InlineData(2026, 10, 1, 2027, 2, 1)]
    public async Task Year_Overlap_Rejected(int y1, int m1, int d1, int y2, int m2, int d2)
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Calendar.CreateYearAsync(Year("Trùng", new(y1, m1, d1), new(y2, m2, d2))), "chồng");
    }

    [Fact(DisplayName = "SCH | Năm học | Khoảng thời gian | bắt đầu ngay sau ngày kết thúc của năm trước được chấp nhận")]
    public async Task Year_Adjacent_Accepted()
    {
        using var e = new SchEnv();
        Assert.NotNull(await e.Calendar.CreateYearAsync(Year("Liền kề", SchEnv.End.AddDays(1), new(2028, 5, 31))));
    }

    [Fact(DisplayName = "SCH | Năm học | Xóa và đặt hiện hành | không xóa được năm hiện hành khi còn năm khác, xóa được khi là năm học duy nhất, năm không tồn tại trả 404")]
    public async Task Year_Delete_AndSetCurrent()
    {
        using var e = new SchEnv();
        SchTestSupport.AddYear(e.Db, 2, "2027-2028", new(2027, 9, 1), new(2028, 5, 31));
        await SchEnv.Rejects(() => e.Calendar.DeleteYearAsync(1), "hiện hành");
        await e.Calendar.DeleteYearAsync(2);        // năm không hiện hành, chưa có dữ liệu gắn vào
        await e.Calendar.DeleteYearAsync(1);        // còn lại là năm học duy nhất nên xóa được
        Assert.Empty(e.Db.AcademicYear);
        var a = await Assert.ThrowsAsync<ApiException>(() => e.Calendar.DeleteYearAsync(999));
        var b = await Assert.ThrowsAsync<ApiException>(() => e.Calendar.SetCurrentYearAsync(999));
        Assert.Equal(404, a.StatusCode); Assert.Equal(404, b.StatusCode);
    }

    // ---------- Học kỳ ----------
    private static SemestersRequest Sem(DateOnly s1, DateOnly e1, DateOnly s2, DateOnly e2) => new(new SemesterRange(s1, e1), new SemesterRange(s2, e2));
    private static readonly DateOnly D1 = new(2026, 9, 1);

    [Fact(DisplayName = "SCH | Học kỳ | Hợp lệ | hai học kỳ nối tiếp trong năm học được lưu")]
    public async Task Semesters_Valid()
    {
        using var e = new SchEnv();
        var y = await e.Calendar.ConfigureSemestersAsync(1, Sem(D1, new(2027, 1, 15), new(2027, 1, 16), new(2027, 5, 31)));
        Assert.Equal(2, y.Semesters.Count);
    }

    [Theory(DisplayName = "SCH | Học kỳ | Ngày | kết thúc không sau bắt đầu, học kỳ 1 muộn hơn 18/01, học kỳ 2 muộn hơn 31/05, chồng nhau hoặc nằm ngoài năm học bị từ chối")]
    [InlineData("2026-09-01", "2026-09-01", "2027-01-16", "2027-05-31", "sau ngày bắt đầu")]
    [InlineData("2026-09-01", "2027-01-19", "2027-01-20", "2027-05-31", "18/01")]
    [InlineData("2026-09-01", "2027-01-15", "2027-01-16", "2027-06-01", "31/05")]
    [InlineData("2026-09-01", "2027-01-15", "2027-01-15", "2027-05-31", "không được chồng")]
    [InlineData("2026-09-01", "2027-01-15", "2027-01-10", "2027-05-31", "không được chồng")]
    [InlineData("2026-08-31", "2027-01-15", "2027-01-16", "2027-05-31", "nằm trong năm học")]
    public async Task Semesters_Invalid(string s1, string e1, string s2, string e2, string reason)
    {
        using var e = new SchEnv();
        var req = Sem(DateOnly.Parse(s1), DateOnly.Parse(e1), DateOnly.Parse(s2), DateOnly.Parse(e2));
        await SchEnv.Rejects(() => e.Calendar.ConfigureSemestersAsync(1, req), reason);
    }

    [Fact(DisplayName = "SCH | Học kỳ | Năm học | năm học không tồn tại trả 404")]
    public async Task Semesters_UnknownYear()
    {
        using var e = new SchEnv();
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Calendar.ConfigureSemestersAsync(999, Sem(D1, new(2027, 1, 15), new(2027, 1, 16), new(2027, 5, 31))));
        Assert.Equal(404, ex.StatusCode);
    }

    // ---------- Ngày nghỉ ----------
    private static HolidayRequest Holiday(DateOnly? date = null, int? campus = null, string? reason = "Nghỉ lễ", int year = 1) => new(year, campus, date ?? new(2026, 11, 20), reason);

    [Fact(DisplayName = "SCH | Ngày nghỉ | Ngày nghỉ | đúng ngày đầu và ngày cuối năm học được chấp nhận")]
    public async Task Holiday_YearBoundaries_Accepted()
    {
        using var e = new SchEnv();
        Assert.NotNull(await e.Calendar.CreateHolidayAsync(Holiday(SchEnv.Start)));
        Assert.NotNull(await e.Calendar.CreateHolidayAsync(Holiday(SchEnv.End)));
    }

    [Theory(DisplayName = "SCH | Ngày nghỉ | Ngày nghỉ | ngoài năm học (trước ngày đầu hoặc sau ngày cuối) bị từ chối")]
    [InlineData(-1)] [InlineData(1)]
    public async Task Holiday_OutsideYear_Rejected(int sign)
    {
        using var e = new SchEnv();
        var date = sign < 0 ? SchEnv.Start.AddDays(-1) : SchEnv.End.AddDays(1);
        await SchEnv.Rejects(() => e.Calendar.CreateHolidayAsync(Holiday(date)), "nằm trong năm học");
    }

    [Fact(DisplayName = "SCH | Ngày nghỉ | Năm học và Phân hiệu | năm học hoặc phân hiệu không tồn tại bị từ chối")]
    public async Task Holiday_UnknownYearOrCampus()
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Calendar.CreateHolidayAsync(Holiday(year: 999)), "năm học");
        await SchEnv.Rejects(() => e.Calendar.CreateHolidayAsync(Holiday(campus: 999)), "Phân hiệu");
    }

    [Fact(DisplayName = "SCH | Ngày nghỉ | Phạm vi | nghỉ toàn trường và nghỉ riêng phân hiệu cùng ngày là hai khai báo; trùng cùng phạm vi bị từ chối")]
    public async Task Holiday_Scope_Duplicates()
    {
        using var e = new SchEnv();
        await e.Calendar.CreateHolidayAsync(Holiday(campus: null));
        await e.Calendar.CreateHolidayAsync(Holiday(campus: 1));
        await SchEnv.Rejects(() => e.Calendar.CreateHolidayAsync(Holiday(campus: null)), "đã khai");
        await SchEnv.Rejects(() => e.Calendar.CreateHolidayAsync(Holiday(campus: 1)), "đã khai");
    }

    [Fact(DisplayName = "SCH | Ngày nghỉ | Lý do | đúng 255 ký tự được chấp nhận, 256 bị từ chối, để trống được")]
    public async Task Holiday_Reason()
    {
        using var e = new SchEnv();
        Assert.Equal(255, (await e.Calendar.CreateHolidayAsync(Holiday(reason: SchEnv.Text(255)))).Reason!.Length);
        await SchEnv.Rejects(() => e.Calendar.CreateHolidayAsync(Holiday(new DateOnly(2026, 11, 21), reason: SchEnv.Text(256))), "255");
        Assert.Null((await e.Calendar.CreateHolidayAsync(Holiday(new DateOnly(2026, 11, 22), reason: null))).Reason);
    }

    [Fact(DisplayName = "SCH | Ngày nghỉ | Xóa | ngày nghỉ không tồn tại trả 404")]
    public async Task Holiday_Delete_Unknown()
    {
        using var e = new SchEnv();
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Calendar.DeleteHolidayAsync(999));
        Assert.Equal(404, ex.StatusCode);
    }

    // ---------- Khung giờ tiết ----------
    private static PeriodSlotRequest Slot(string session = "Morning", byte no = 1, string s = "07:00", string en = "07:45") => new(session, no, TimeOnly.Parse(s), TimeOnly.Parse(en));

    [Theory(DisplayName = "SCH | Khung giờ | Buổi | chỉ nhận Morning hoặc Afternoon, giá trị khác bị từ chối")]
    [InlineData("Evening")] [InlineData("morning")] [InlineData("")] [InlineData("Sáng")]
    public async Task Slot_Session_Invalid(string session)
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Slots.CreateAsync(Slot(session)), "Buổi");
    }

    [Theory(DisplayName = "SCH | Khung giờ | Số tiết | từ 1 trở lên được chấp nhận, bằng 0 hoặc quá 20 bị từ chối")]
    [InlineData(0, false)] [InlineData(1, true)] [InlineData(20, true)] [InlineData(21, false)] [InlineData(255, false)]
    public async Task Slot_PeriodNo(int no, bool accepted)
    {
        using var e = new SchEnv();
        if (accepted) Assert.Equal((byte)no, (await e.Slots.CreateAsync(Slot(no: (byte)no))).PeriodNo);
        else await SchEnv.Rejects(() => e.Slots.CreateAsync(Slot(no: (byte)no)), "Số tiết");
    }

    [Theory(DisplayName = "SCH | Khung giờ | Giờ | giờ kết thúc trùng hoặc trước giờ bắt đầu bị từ chối")]
    [InlineData("07:00", "07:00")] [InlineData("07:45", "07:00")] [InlineData("23:59", "00:00")]
    public async Task Slot_End_NotAfterStart(string s, string en)
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Slots.CreateAsync(Slot(s: s, en: en)), "sau giờ bắt đầu");
    }

    [Fact(DisplayName = "SCH | Khung giờ | Thứ tự | tiết sau phải bắt đầu sau khi tiết trước kết thúc; chạm đúng giờ được chấp nhận; chồng bị từ chối")]
    public async Task Slot_Order_And_Overlap()
    {
        using var e = new SchEnv();
        await e.Slots.CreateAsync(Slot(no: 1, s: "07:00", en: "07:45"));
        Assert.NotNull(await e.Slots.CreateAsync(Slot(no: 2, s: "07:45", en: "08:30")));       // nối tiếp đúng giờ
        await SchEnv.Rejects(() => e.Slots.CreateAsync(Slot(no: 3, s: "08:00", en: "08:45")), "chồng");
        await SchEnv.Rejects(() => e.Slots.CreateAsync(Slot(no: 0 + 4, s: "06:00", en: "06:50")), "thứ tự");   // tiết 4 mà sớm hơn tiết 1
    }

    [Fact(DisplayName = "SCH | Khung giờ | Buổi sáng và chiều | buổi chiều phải sau buổi sáng; trùng (buổi, tiết) bị từ chối")]
    public async Task Slot_MorningBeforeAfternoon_AndUniquePair()
    {
        using var e = new SchEnv();
        await e.Slots.CreateAsync(Slot("Morning", 1, "07:00", "07:45"));
        await SchEnv.Rejects(() => e.Slots.CreateAsync(Slot("Afternoon", 1, "07:30", "08:15")), "chồng");
        Assert.NotNull(await e.Slots.CreateAsync(Slot("Afternoon", 1, "13:30", "14:15")));
        await SchEnv.Rejects(() => e.Slots.CreateAsync(Slot("Morning", 1, "09:00", "09:45")), "đã có");
    }

    [Fact(DisplayName = "SCH | Khung giờ | Sửa giờ | kiểm cùng quy tắc giờ; khung không tồn tại trả 404")]
    public async Task Slot_UpdateTimes()
    {
        using var e = new SchEnv();
        var s = await e.Slots.CreateAsync(Slot());
        await SchEnv.Rejects(() => e.Slots.UpdateTimesAsync(s.PeriodSlotId, new PeriodSlotTimesRequest(new(8, 0), new(7, 0))), "sau giờ bắt đầu");
        await e.Slots.UpdateTimesAsync(s.PeriodSlotId, new PeriodSlotTimesRequest(new(7, 5), new(7, 50)));
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Slots.UpdateTimesAsync(999, new PeriodSlotTimesRequest(new(7, 0), new(7, 45))));
        Assert.Equal(404, ex.StatusCode);
    }
}

/// <summary>Lớp học (SCH-05): kiểm từng trường khi tạo và sửa lớp.</summary>
public class SchClassFieldTests
{
    private static ClassRequest Cls(string name = "6A1", byte grade = 6, int campus = 1, int year = 1) => new(campus, year, name, grade);

    [Theory(DisplayName = "SCH | Lớp | Tên lớp | để trống bị từ chối")]
    [InlineData("")] [InlineData("  ")] [InlineData(null)]
    public async Task Name_Required(string? name)
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Classes.CreateAsync(Cls(name!)), "Tên lớp");
    }

    [Fact(DisplayName = "SCH | Lớp | Tên lớp | đúng 50 ký tự được chấp nhận, 51 bị từ chối, khoảng trắng thừa được gộp")]
    public async Task Name_Boundary_AndNormalize()
    {
        using var e = new SchEnv();
        Assert.Equal(50, (await e.Classes.CreateAsync(Cls(SchEnv.Text(50)))).Name.Length);
        await SchEnv.Rejects(() => e.Classes.CreateAsync(Cls(SchEnv.Text(51))), "50");
        Assert.Equal("7 A 1", (await e.Classes.CreateAsync(Cls("  7   A   1 ", 7))).Name);
    }

    [Theory(DisplayName = "SCH | Lớp | Khối | chỉ từ 6 đến 9; 5 và 10 bị từ chối")]
    [InlineData(0, false)] [InlineData(5, false)] [InlineData(6, true)] [InlineData(9, true)] [InlineData(10, false)] [InlineData(255, false)]
    public async Task Grade_Range(int grade, bool accepted)
    {
        using var e = new SchEnv();
        if (accepted) Assert.Equal((byte)grade, (await e.Classes.CreateAsync(Cls("L" + grade, (byte)grade))).GradeLevel);
        else await SchEnv.Rejects(() => e.Classes.CreateAsync(Cls("L" + grade, (byte)grade)), "6 đến 9");
    }

    [Fact(DisplayName = "SCH | Lớp | Phân hiệu | không tồn tại hoặc đã ngừng hoạt động bị từ chối")]
    public async Task Campus_Invalid()
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Classes.CreateAsync(Cls(campus: 999)), "phân hiệu");
        await SchEnv.Rejects(() => e.Classes.CreateAsync(Cls(campus: 2)), "ngừng hoạt động");
    }

    [Fact(DisplayName = "SCH | Lớp | Năm học | không tồn tại bị từ chối")]
    public async Task Year_Invalid()
    {
        using var e = new SchEnv();
        await SchEnv.Rejects(() => e.Classes.CreateAsync(Cls(year: 999)), "năm học");
    }

    [Fact(DisplayName = "SCH | Lớp | Tên lớp | tên trùng chỉ bị từ chối khi cùng phân hiệu và cùng năm học")]
    public async Task Name_Unique_PerCampusAndYear()
    {
        using var e = new SchEnv();
        SchTestSupport.AddCampus(e.Db, 3, "Cơ sở 3");
        SchTestSupport.AddYear(e.Db, 2, "2027-2028", new(2027, 9, 1), new(2028, 5, 31));
        await e.Classes.CreateAsync(Cls());
        await SchEnv.Rejects(() => e.Classes.CreateAsync(Cls()), "đã có");
        Assert.NotNull(await e.Classes.CreateAsync(Cls(campus: 3)));
        Assert.NotNull(await e.Classes.CreateAsync(Cls(year: 2)));
    }

    [Fact(DisplayName = "SCH | Lớp | Sửa | kiểm tên và khối như khi tạo; lớp không tồn tại trả 404")]
    public async Task Update_Validates()
    {
        using var e = new SchEnv();
        var c = await e.Classes.CreateAsync(Cls());
        await SchEnv.Rejects(() => e.Classes.UpdateAsync(c.ClassId, new ClassUpdateRequest("", 6)), "Tên lớp");
        await SchEnv.Rejects(() => e.Classes.UpdateAsync(c.ClassId, new ClassUpdateRequest(SchEnv.Text(51), 6)), "50");
        await SchEnv.Rejects(() => e.Classes.UpdateAsync(c.ClassId, new ClassUpdateRequest("6A1", 10)), "6 đến 9");
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Classes.UpdateAsync(999, new ClassUpdateRequest("X", 6)));
        Assert.Equal(404, ex.StatusCode);
    }
}