using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Kiểm tra từng trường của hồ sơ cán bộ (PPL-01, 4.3.4-4.3.6). Sửa dùng cán bộ có sẵn; tạo mới dừng ở bước validate vì mã hồ sơ dùng sequence SQL.</summary>
public class StaffFieldTests
{
    private static StaffRequest Valid() =>
        new("Nguyễn Văn A", new DateOnly(1990, 1, 1), "222222222222", "Nam", "0912345678", "a@school.vn", PplEnv.Campus1, PplEnv.Dept1, "NEW1", "Giáo viên");

    private static Task<StaffDto> Update(PplEnv e, StaffRequest r) => e.Staff.UpdateAsync(PplEnv.Teacher, r);

    /// <summary>Tạo mới hợp lệ qua được validate rồi mới gọi sequence SQL nên với DB trong bộ nhớ sẽ ném InvalidOperationException.</summary>
    private static async Task PassesValidation(Func<Task> create)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(create);
        Assert.Contains("Relational", ex.Message);
    }

    // ---------- Họ và tên ----------
    [Theory(DisplayName = "PPL | Cán bộ | Họ và tên | để trống hoặc chỉ khoảng trắng bị từ chối")]
    [InlineData("")] [InlineData("   ")] [InlineData("\t")] [InlineData(null)]
    public async Task FullName_Required(string? name)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Staff.CreateAsync(Valid() with { FullName = name! }), "Họ và tên");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Họ và tên | đúng 150 ký tự được chấp nhận")]
    public async Task FullName_Max150_Accepted()
    {
        using var e = new PplEnv();
        Assert.Equal(150, (await Update(e, Valid() with { FullName = PplEnv.Text(150) })).FullName.Length);
    }

    [Fact(DisplayName = "PPL | Cán bộ | Họ và tên | 151 ký tự bị từ chối")]
    public async Task FullName_Over150_Rejected()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { FullName = PplEnv.Text(151) }), "150");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Họ và tên | khoảng trắng thừa được gộp và cắt hai đầu")]
    public async Task FullName_IsNormalized()
    {
        using var e = new PplEnv();
        Assert.Equal("Nguyễn Văn A", (await Update(e, Valid() with { FullName = "  Nguyễn   Văn  A  " })).FullName);
    }

    // ---------- Ngày sinh ----------
    [Fact(DisplayName = "PPL | Cán bộ | Ngày sinh | để trống bị từ chối")]
    public async Task Dob_Required()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { DateOfBirth = null }), "Ngày sinh");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Ngày sinh | ngày mai (tương lai) bị từ chối")]
    public async Task Dob_Future_Rejected()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { DateOfBirth = PplEnv.Today.AddDays(1) }), "tương lai");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Ngày sinh | đúng hôm nay được chấp nhận (ranh giới)")]
    public async Task Dob_Today_Accepted()
    {
        using var e = new PplEnv();
        Assert.Equal(PplEnv.Today, (await Update(e, Valid() with { DateOfBirth = PplEnv.Today })).DateOfBirth);
    }

    [Fact(DisplayName = "PPL | Cán bộ | Ngày sinh | năm 1800 vẫn được chấp nhận (CHƯA kiểm tuổi hợp lý)")]
    public async Task Dob_VeryOld_CurrentlyAccepted()
    {
        using var e = new PplEnv();
        Assert.Equal(new DateOnly(1800, 1, 1), (await Update(e, Valid() with { DateOfBirth = new DateOnly(1800, 1, 1) })).DateOfBirth);
    }

    // ---------- Giới tính ----------
    [Theory(DisplayName = "PPL | Cán bộ | Giới tính | các cách viết hợp lệ được chuẩn hóa")]
    [InlineData("Nam", "Male")] [InlineData("nữ", "Female")] [InlineData("KHÁC", "Other")] [InlineData("male", "Male")]
    [InlineData("Female", "Female")] [InlineData("nu", "Female")] [InlineData("khac", "Other")] [InlineData("", null)] [InlineData(null, null)]
    public async Task Gender_Valid(string? input, string? stored)
    {
        using var e = new PplEnv();
        Assert.Equal(stored, (await Update(e, Valid() with { Gender = input })).Gender);
    }

    [Theory(DisplayName = "PPL | Cán bộ | Giới tính | giá trị lạ bị từ chối")]
    [InlineData("robot")] [InlineData("M")] [InlineData("1")] [InlineData("nam nữ")]
    public async Task Gender_Invalid(string input)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { Gender = input }), "Giới tính");
    }

    // ---------- Email ----------
    [Theory(DisplayName = "PPL | Cán bộ | Email | địa chỉ hợp lệ được chấp nhận")]
    [InlineData("a@school.vn")] [InlineData("ten.ho+tag@truong.edu.vn")] [InlineData(null)] [InlineData("")]
    public async Task Email_Valid(string? email)
    {
        using var e = new PplEnv();
        var dto = await Update(e, Valid() with { Email = email });
        Assert.Equal(string.IsNullOrEmpty(email) ? null : email, dto.Email);
    }

    [Theory(DisplayName = "PPL | Cán bộ | Email | sai định dạng bị từ chối")]
    [InlineData("abc")] [InlineData("a@b")] [InlineData("a b@c.vn")] [InlineData("Nguyễn <a@b.vn>")] [InlineData("a@b.vn, c@d.vn")] [InlineData("@b.vn")]
    public async Task Email_Invalid(string email)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { Email = email }), "Email");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Email | đúng 150 ký tự được chấp nhận, 151 bị từ chối")]
    public async Task Email_Length_Boundary()
    {
        using var e = new PplEnv();
        var at150 = new string('a', 140) + "@school.vn";
        Assert.Equal(150, (await Update(e, Valid() with { Email = at150 })).Email!.Length);
        await PplEnv.Rejects(() => Update(e, Valid() with { Email = new string('a', 141) + "@school.vn" }), "150");
    }

    // ---------- Số điện thoại ----------
    [Fact(DisplayName = "PPL | Cán bộ | Số điện thoại | đúng 20 ký tự được chấp nhận, 21 bị từ chối")]
    public async Task Phone_Length_Boundary()
    {
        using var e = new PplEnv();
        Assert.Equal(20, (await Update(e, Valid() with { PhoneNumber = new string('1', 20) })).PhoneNumber!.Length);
        await PplEnv.Rejects(() => Update(e, Valid() with { PhoneNumber = new string('1', 21) }), "20");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Số điện thoại | chữ cái vẫn được chấp nhận (CHƯA kiểm định dạng số)")]
    public async Task Phone_Letters_CurrentlyAccepted()
    {
        using var e = new PplEnv();
        Assert.Equal("abc-xyz", (await Update(e, Valid() with { PhoneNumber = "abc-xyz" })).PhoneNumber);
    }

    // ---------- Phân hiệu chính ----------
    [Theory(DisplayName = "PPL | Cán bộ | Phân hiệu chính | không chọn hoặc không tồn tại bị từ chối")]
    [InlineData(0)] [InlineData(999)] [InlineData(-1)]
    public async Task Campus_Unknown(int campusId)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { PrimaryCampusId = campusId }), "phân hiệu");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Phân hiệu chính | phân hiệu đã ngừng hoạt động bị từ chối")]
    public async Task Campus_Inactive()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { PrimaryCampusId = PplEnv.CampusInactive }), "ngừng hoạt động");
    }

    // ---------- Tổ chuyên môn ----------
    [Fact(DisplayName = "PPL | Cán bộ | Tổ chuyên môn | tổ không tồn tại bị từ chối")]
    public async Task Department_Unknown()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { DepartmentId = 999 }), "Tổ chuyên môn");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Tổ chuyên môn | để trống được chấp nhận")]
    public async Task Department_Optional()
    {
        using var e = new PplEnv();
        Assert.Null((await Update(e, Valid() with { DepartmentId = null })).DepartmentId);
    }

    [Fact(DisplayName = "PPL | Cán bộ | Tổ chuyên môn | tổ đã ngừng hoạt động vẫn được chọn (CHƯA kiểm tổ còn hoạt động)")]
    public async Task Department_Inactive_CurrentlyAccepted()
    {
        using var e = new PplEnv();
        Assert.Equal(PplEnv.DeptInactive, (await Update(e, Valid() with { DepartmentId = PplEnv.DeptInactive })).DepartmentId);
    }

    // ---------- Viết tắt giáo viên ----------
    [Theory(DisplayName = "PPL | Cán bộ | Viết tắt GV | chữ và số hợp lệ được chấp nhận")]
    [InlineData("A")] [InlineData("ABC123")] [InlineData("abc")] [InlineData("1234567890")]
    public async Task Abbr_Valid(string abbr)
    {
        using var e = new PplEnv();
        Assert.Equal(abbr, (await Update(e, Valid() with { TeacherAbbr = abbr })).TeacherAbbr);
    }

    [Theory(DisplayName = "PPL | Cán bộ | Viết tắt GV | có dấu cách, gạch, ký tự có dấu bị từ chối")]
    [InlineData("AB-C")] [InlineData("AB C")] [InlineData("Đạt")] [InlineData("AB_C")] [InlineData("A.B")]
    public async Task Abbr_Invalid(string abbr)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { TeacherAbbr = abbr }), "chữ và số");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Viết tắt GV | đúng 10 ký tự được chấp nhận, 11 bị từ chối")]
    public async Task Abbr_Length_Boundary()
    {
        using var e = new PplEnv();
        Assert.Equal(10, (await Update(e, Valid() with { TeacherAbbr = new string('A', 10) })).TeacherAbbr!.Length);
        await PplEnv.Rejects(() => Update(e, Valid() with { TeacherAbbr = new string('A', 11) }), "10");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Viết tắt GV | trùng viết tắt của cán bộ khác bị từ chối, giữ nguyên của mình thì được")]
    public async Task Abbr_Unique()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { TeacherAbbr = "XYZ" }), "duy nhất");
        Assert.Equal("ABC", (await Update(e, Valid() with { TeacherAbbr = "ABC" })).TeacherAbbr);
    }

    [Theory(DisplayName = "PPL | Cán bộ | Viết tắt GV | để trống thì không có viết tắt")]
    [InlineData("")] [InlineData("   ")] [InlineData(null)]
    public async Task Abbr_Optional(string? abbr)
    {
        using var e = new PplEnv();
        Assert.Null((await Update(e, Valid() with { TeacherAbbr = abbr })).TeacherAbbr);
    }

    // ---------- Chức danh ----------
    [Fact(DisplayName = "PPL | Cán bộ | Chức danh | đúng 100 ký tự được chấp nhận, 101 bị từ chối")]
    public async Task Position_Length_Boundary()
    {
        using var e = new PplEnv();
        Assert.Equal(100, (await Update(e, Valid() with { Position = PplEnv.Text(100) })).Position!.Length);
        await PplEnv.Rejects(() => Update(e, Valid() with { Position = PplEnv.Text(101) }), "100");
    }

    // ---------- Số định danh ----------
    [Theory(DisplayName = "PPL | Cán bộ | Số định danh | thiếu, sai độ dài hoặc không phải số bị từ chối khi tạo")]
    [InlineData("")] [InlineData(null)] [InlineData("12345678901")] [InlineData("1234567890123")] [InlineData("12345678901a")] [InlineData("12345 78901 2")]
    public async Task Nid_Invalid(string? nid)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Staff.CreateAsync(Valid() with { NationalId = nid }), "Số định danh");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Số định danh | trùng số của hồ sơ khác bị từ chối và nêu mã hồ sơ")]
    public async Task Nid_Duplicate()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Staff.CreateAsync(Valid() with { NationalId = PplEnv.ExistingNid }), "CB000010");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Số định danh | 12 chữ số hợp lệ (kể cả có khoảng trắng hai đầu) qua được validate khi tạo")]
    public async Task Nid_Valid_PassesValidation()
    {
        using var e = new PplEnv();
        await PassesValidation(() => e.Staff.CreateAsync(Valid()));
        await PassesValidation(() => e.Staff.CreateAsync(Valid() with { NationalId = "  333333333333  " }));
    }

    [Fact(DisplayName = "PPL | Cán bộ | Số định danh | không sửa được sau khi tạo (giá trị gửi lên khi sửa bị bỏ qua)")]
    public async Task Nid_Immutable_OnUpdate()
    {
        using var e = new PplEnv();
        var dto = await Update(e, Valid() with { NationalId = "999999999999" });
        Assert.Equal(PplEnv.ExistingNid, dto.NationalId);
    }

    [Fact(DisplayName = "PPL | Cán bộ | Tạo hợp lệ | mọi trường hợp lệ cùng lúc qua được validate")]
    public async Task Create_AllValid_PassesValidation()
    {
        using var e = new PplEnv();
        await PassesValidation(() => e.Staff.CreateAsync(Valid() with { Gender = "Nữ", Email = null, PhoneNumber = null, DepartmentId = null, TeacherAbbr = null, Position = null }));
    }

    [Fact(DisplayName = "PPL | Cán bộ | Sửa | cán bộ không tồn tại trả 404")]
    public async Task Update_Unknown_NotFound()
    {
        using var e = new PplEnv();
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Staff.UpdateAsync(9999, Valid()));
        Assert.Equal(404, ex.StatusCode);
    }
}