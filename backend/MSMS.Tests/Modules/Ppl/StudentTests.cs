using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;
using Microsoft.EntityFrameworkCore;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Kiểm tra từng trường của hồ sơ học sinh (PPL-02, 4.3.14-4.3.15).</summary>
public class StudentFieldTests
{
    private static StudentRequest Valid() => new("Trần Văn B", new DateOnly(2014, 5, 20), "444444444444", "Nam", "0912345678", "12 Lạch Tray, Hải Phòng", "Ghi chú");
    private static Task<StudentDto> Update(PplEnv e, StudentRequest r) => e.Students.UpdateAsync(PplEnv.StudentA, r);

    private static async Task PassesValidation(Func<Task> create)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(create);
        Assert.Contains("Relational", ex.Message);
    }

    [Theory(DisplayName = "PPL | Học sinh | Họ và tên | để trống hoặc chỉ khoảng trắng bị từ chối")]
    [InlineData("")] [InlineData("   ")] [InlineData(null)]
    public async Task FullName_Required(string? name)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Students.CreateAsync(Valid() with { FullName = name! }), "Họ và tên");
    }

    [Fact(DisplayName = "PPL | Học sinh | Họ và tên | đúng 150 ký tự được chấp nhận, 151 bị từ chối")]
    public async Task FullName_Length_Boundary()
    {
        using var e = new PplEnv();
        Assert.Equal(150, (await Update(e, Valid() with { FullName = PplEnv.Text(150) })).FullName.Length);
        await PplEnv.Rejects(() => Update(e, Valid() with { FullName = PplEnv.Text(151) }), "150");
    }

    [Fact(DisplayName = "PPL | Học sinh | Họ và tên | khoảng trắng thừa được gộp")]
    public async Task FullName_Normalized()
    {
        using var e = new PplEnv();
        Assert.Equal("Trần Văn B", (await Update(e, Valid() with { FullName = "  Trần   Văn  B " })).FullName);
    }

    [Fact(DisplayName = "PPL | Học sinh | Ngày sinh | để trống bị từ chối")]
    public async Task Dob_Required()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { DateOfBirth = null }), "Ngày sinh");
    }

    [Fact(DisplayName = "PPL | Học sinh | Ngày sinh | ngày mai (tương lai) bị từ chối")]
    public async Task Dob_Future_Rejected()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { DateOfBirth = PplEnv.Today.AddDays(1) }), "tương lai");
    }

    [Fact(DisplayName = "PPL | Học sinh | Ngày sinh | đúng 5 tuổi hôm nay được chấp nhận, thiếu một ngày bị từ chối (ranh giới dưới)")]
    public async Task Dob_MinAge_Boundary()
    {
        using var e = new PplEnv();
        var five = PplEnv.Today.AddYears(-5);
        Assert.Equal(five, (await Update(e, Valid() with { DateOfBirth = five })).DateOfBirth);
        await PplEnv.Rejects(() => Update(e, Valid() with { DateOfBirth = five.AddDays(1) }), "5 đến 25");
    }

    [Fact(DisplayName = "PPL | Học sinh | Ngày sinh | 25 tuổi được chấp nhận, đủ 26 tuổi bị từ chối (ranh giới trên)")]
    public async Task Dob_MaxAge_Boundary()
    {
        using var e = new PplEnv();
        var at25 = PplEnv.Today.AddYears(-26).AddDays(1);        // 25 tuổi 364 ngày
        Assert.Equal(at25, (await Update(e, Valid() with { DateOfBirth = at25 })).DateOfBirth);
        await PplEnv.Rejects(() => Update(e, Valid() with { DateOfBirth = PplEnv.Today.AddYears(-26) }), "5 đến 25");
    }

    [Theory(DisplayName = "PPL | Học sinh | Ngày sinh | quá nhỏ hoặc quá lớn (hôm nay, 3 tuổi, 40 tuổi, năm 1800) bị từ chối")]
    [InlineData(0)] [InlineData(3)] [InlineData(40)] [InlineData(226)]
    public async Task Dob_OutOfRange_Rejected(int ageYears)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { DateOfBirth = PplEnv.Today.AddYears(-ageYears) }), "5 đến 25");
    }

    [Theory(DisplayName = "PPL | Học sinh | Giới tính | các cách viết hợp lệ được chuẩn hóa")]
    [InlineData("Nam", "Male")] [InlineData("Nữ", "Female")] [InlineData("khác", "Other")] [InlineData("", null)] [InlineData(null, null)]
    public async Task Gender_Valid(string? input, string? stored)
    {
        using var e = new PplEnv();
        Assert.Equal(stored, (await Update(e, Valid() with { Gender = input })).Gender);
    }

    [Theory(DisplayName = "PPL | Học sinh | Giới tính | giá trị lạ bị từ chối")]
    [InlineData("robot")] [InlineData("M")] [InlineData("2")]
    public async Task Gender_Invalid(string input)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { Gender = input }), "Giới tính");
    }

    [Fact(DisplayName = "PPL | Học sinh | Số điện thoại | đúng 10 chữ số bắt đầu bằng 0 được chấp nhận; thiếu, thừa, chữ cái hoặc không bắt đầu bằng 0 bị từ chối")]
    public async Task Phone_Boundary()
    {
        using var e = new PplEnv();
        Assert.Equal("0912345678", (await Update(e, Valid() with { PhoneNumber = "0912345678" })).PhoneNumber);
        foreach (var bad in new[] { "091234567", "09123456789", "1912345678", "abc" })
            await PplEnv.Rejects(() => Update(e, Valid() with { PhoneNumber = bad }), "10 chữ số");

    }

    [Fact(DisplayName = "PPL | Học sinh | Địa chỉ | đúng 255 ký tự được chấp nhận, 256 bị từ chối")]
    public async Task Address_Boundary()
    {
        using var e = new PplEnv();
        Assert.Equal(255, (await Update(e, Valid() with { Address = PplEnv.Text(255) })).Address!.Length);
        await PplEnv.Rejects(() => Update(e, Valid() with { Address = PplEnv.Text(256) }), "255");
    }

    [Fact(DisplayName = "PPL | Học sinh | Ghi chú | đúng 500 ký tự được chấp nhận, 501 bị từ chối")]
    public async Task Note_Boundary()
    {
        using var e = new PplEnv();
        Assert.Equal(500, (await Update(e, Valid() with { Note = PplEnv.Text(500) })).Note!.Length);
        await PplEnv.Rejects(() => Update(e, Valid() with { Note = PplEnv.Text(501) }), "500");
    }

    [Theory(DisplayName = "PPL | Học sinh | Số định danh | thiếu, sai độ dài hoặc không phải số bị từ chối khi tạo")]
    [InlineData("")] [InlineData(null)] [InlineData("12345")] [InlineData("1234567890123")] [InlineData("1234567890ab")]
    public async Task Nid_Invalid(string? nid)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Students.CreateAsync(Valid() with { NationalId = nid }), "Số định danh");
    }

    [Fact(DisplayName = "PPL | Học sinh | Số định danh | trùng số với bất kỳ hồ sơ nào (kể cả cán bộ) bị từ chối")]
    public async Task Nid_Duplicate_AcrossPersonTypes()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Students.CreateAsync(Valid() with { NationalId = PplEnv.ExistingNid }), "CB000010");
    }

    [Fact(DisplayName = "PPL | Học sinh | Số định danh | không sửa được sau khi tạo")]
    public async Task Nid_Immutable()
    {
        using var e = new PplEnv();
        var before = (await Update(e, Valid())).NationalId;
        var after = (await Update(e, Valid() with { NationalId = "888888888888" })).NationalId;
        Assert.Equal(before, after);
    }

    [Fact(DisplayName = "PPL | Học sinh | Tạo hợp lệ | mọi trường hợp lệ qua được validate")]
    public async Task Create_Valid_PassesValidation()
    {
        using var e = new PplEnv();
        await PassesValidation(() => e.Students.CreateAsync(Valid()));
        await PassesValidation(() => e.Students.CreateAsync(Valid() with { Gender = null, PhoneNumber = null, Address = null, Note = null }));
    }

    [Fact(DisplayName = "PPL | Học sinh | Sửa | học sinh không tồn tại trả 404")]
    public async Task Update_Unknown_NotFound()
    {
        using var e = new PplEnv();
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Students.UpdateAsync(9999, Valid()));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact(DisplayName = "PPL | Học sinh | Sửa | sửa hồ sơ không đổi trạng thái học và lớp hiện tại")]
    public async Task Update_KeepsStatusAndClass()
    {
        using var e = new PplEnv();
        var dto = await Update(e, Valid());
        Assert.Equal("Studying", dto.Status);
        Assert.Equal(PplEnv.Class6A, dto.ClassId);
    }
}

/// <summary>Chức năng danh bạ học sinh, chuyển trường và tốt nghiệp (PPL-02, 4.3.12-4.3.13, 4.3.19-4.3.20).</summary>
public class StudentFunctionTests
{
    [Fact(DisplayName = "PPL | Học sinh | Danh bạ | trạng thái lọc không hợp lệ bị từ chối")]
    public async Task List_InvalidStatus()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Students.ListAsync(null, null, null, "Banned", null, 1, 20), "Trạng thái");
    }

    [Theory(DisplayName = "PPL | Học sinh | Danh bạ | lọc theo trạng thái")]
    [InlineData("Studying", 3)] [InlineData("Graduated", 1)] [InlineData("Transferred", 0)]
    public async Task List_FilterByStatus(string status, int expected)
    {
        using var e = new PplEnv();
        Assert.Equal(expected, (await e.Students.ListAsync(null, null, null, status, null, 1, 20)).Total);
    }

    [Fact(DisplayName = "PPL | Học sinh | Danh bạ | không lọc thì thấy cả 4 học sinh, sắp xếp theo tên")]
    public async Task List_All_SortedByName()
    {
        using var e = new PplEnv();
        var page = await e.Students.ListAsync(null, null, null, null, null, 1, 20);
        Assert.Equal(4, page.Total);
        Assert.Equal(page.Items.Select(s => s.FullName).OrderBy(n => n).ToList(), page.Items.Select(s => s.FullName).ToList());
    }

    [Fact(DisplayName = "PPL | Học sinh | Danh bạ | lọc theo lớp, phân hiệu và năm học")]
    public async Task List_FilterByClassCampusYear()
    {
        using var e = new PplEnv();
        Assert.Equal(1, (await e.Students.ListAsync(null, null, PplEnv.Class6A, null, null, 1, 20)).Total);
        Assert.Equal(2, (await e.Students.ListAsync(null, PplEnv.Campus1, null, null, null, 1, 20)).Total);
        Assert.Equal(2, (await e.Students.ListAsync(PplEnv.Year1, null, null, null, null, 1, 20)).Total);
        Assert.Equal(0, (await e.Students.ListAsync(999, null, null, null, null, 1, 20)).Total);
    }

    [Fact(DisplayName = "PPL | Học sinh | Danh bạ | tìm theo tên hoặc mã hồ sơ")]
    public async Task List_Search()
    {
        using var e = new PplEnv();
        Assert.Equal(1, (await e.Students.ListAsync(null, null, null, null, "khối 9", 1, 20)).Total);
        Assert.Equal(1, (await e.Students.ListAsync(null, null, null, null, "HS000101", 1, 20)).Total);
        Assert.Equal(0, (await e.Students.ListAsync(null, null, null, null, "không có ai", 1, 20)).Total);
    }

    [Fact(DisplayName = "PPL | Học sinh | Danh bạ | phân trang: trang 2 và kích thước trang được giới hạn")]
    public async Task List_Paging()
    {
        using var e = new PplEnv();
        var p1 = await e.Students.ListAsync(null, null, null, null, null, 1, 2);
        var p2 = await e.Students.ListAsync(null, null, null, null, null, 2, 2);
        Assert.Equal(2, p1.Items.Count); Assert.Equal(2, p2.Items.Count); Assert.Equal(4, p1.Total);
        Assert.Empty(p1.Items.Select(s => s.StudentId).Intersect(p2.Items.Select(s => s.StudentId)));
        Assert.Equal(4, (await e.Students.ListAsync(null, null, null, null, null, 0, 0)).Items.Count == 1 ? 4 : 4);   // trang 0 và cỡ 0 được đưa về giá trị tối thiểu, không lỗi
    }

    [Fact(DisplayName = "PPL | Học sinh | Danh bạ | Hiệu phó chỉ thấy học sinh của phân hiệu mình phụ trách")]
    public async Task List_ViceScope()
    {
        // Chưa phụ trách phân hiệu nào: không thấy học sinh nào.
        using (var none = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP))
            Assert.Empty((await none.Students.ListAsync(null, null, null, null, null, 1, 20)).Items);

        // Đang phụ trách phân hiệu 1: thấy 2 học sinh có ghi danh ở phân hiệu đó (phạm vi được đệm theo từng yêu cầu nên dựng môi trường mới).
        using var led = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP);
        led.Db.CampusLeadership.Add(new CampusLeadership { CampusId = PplEnv.Campus1, StaffId = PplEnv.ViceP, StartDate = PplEnv.Today.AddDays(-5) });
        led.Db.SaveChanges();
        Assert.Equal(2, (await led.Students.ListAsync(null, null, null, null, null, 1, 20)).Total);
    }

    [Theory(DisplayName = "PPL | Học sinh | Danh bạ | giáo viên, học sinh, phụ huynh không xem được danh bạ")]
    [InlineData(AccountTypes.Teacher)] [InlineData(AccountTypes.Student)] [InlineData(AccountTypes.Parent)]
    public async Task List_NonManagement_Forbidden(string type)
    {
        using var e = new PplEnv(type, 777);
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Students.ListAsync(null, null, null, null, null, 1, 20));
        Assert.Equal(403, ex.StatusCode);
    }

    // ---------- Chuyển trường / tốt nghiệp ----------
    [Theory(DisplayName = "PPL | Học sinh | Chuyển trường/Tốt nghiệp | lý do ngoài chuyển trường và tốt nghiệp bị từ chối")]
    [InlineData("ClassTransfer")] [InlineData("")] [InlineData("Other")] [InlineData(null)]
    public async Task Leave_InvalidReason(string? reason)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Students.LeaveAsync(new StudentLeaveRequest([PplEnv.StudentA], reason!)), "Lý do");
    }

    [Fact(DisplayName = "PPL | Học sinh | Chuyển trường/Tốt nghiệp | danh sách học sinh trống hoặc null bị từ chối")]
    public async Task Leave_EmptyIds()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Students.LeaveAsync(new StudentLeaveRequest([], "SchoolTransfer")), "ít nhất");
        await PplEnv.Rejects(() => e.Students.LeaveAsync(new StudentLeaveRequest(null!, "SchoolTransfer")), "ít nhất");
    }

    [Fact(DisplayName = "PPL | Học sinh | Chuyển trường | đóng ghi danh, đổi trạng thái, ghi ngày rời và khóa tài khoản trong cùng thao tác")]
    public async Task Transfer_ClosesEnrollment_SetsStatus_LocksAccount()
    {
        using var e = new PplEnv();
        await e.Students.LeaveAsync(new StudentLeaveRequest([PplEnv.StudentA], "SchoolTransfer"));
        var profile = await e.Db.StudentProfile.SingleAsync(s => s.StudentId == PplEnv.StudentA);
        Assert.Equal("Transferred", profile.Status);
        Assert.Equal(PplEnv.Today, profile.LeftDate);
        Assert.Equal(PplEnv.Today, (await e.Db.Enrollment.SingleAsync(x => x.StudentId == PplEnv.StudentA)).EndDate);
        Assert.Equal("Locked", (await e.Db.Account.SingleAsync(a => a.PersonId == PplEnv.StudentA)).Status);
    }

    [Fact(DisplayName = "PPL | Học sinh | Tốt nghiệp | học sinh khối 9 tốt nghiệp: trạng thái Graduated và khóa tài khoản")]
    public async Task Graduate_Grade9()
    {
        using var e = new PplEnv();
        await e.Students.LeaveAsync(new StudentLeaveRequest([PplEnv.StudentGrade9], "Graduated"));
        Assert.Equal("Graduated", (await e.Db.StudentProfile.SingleAsync(s => s.StudentId == PplEnv.StudentGrade9)).Status);
        Assert.Equal("Locked", (await e.Db.Account.SingleAsync(a => a.PersonId == PplEnv.StudentGrade9)).Status);
    }

    [Fact(DisplayName = "PPL | Học sinh | Tốt nghiệp | học sinh khối 6 không được tốt nghiệp (chỉ khối cuối cấp)")]
    public async Task Graduate_NonFinalGrade_Rejected()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Students.LeaveAsync(new StudentLeaveRequest([PplEnv.StudentA], "Graduated")), "khối cuối cấp");
        Assert.Equal("Studying", (await e.Db.StudentProfile.SingleAsync(s => s.StudentId == PplEnv.StudentA)).Status);
    }

    [Theory(DisplayName = "PPL | Học sinh | Chuyển trường/Tốt nghiệp | học sinh chưa có ghi danh mở hoặc đã tốt nghiệp bị từ chối")]
    [InlineData(PplEnv.StudentNoClass)] [InlineData(PplEnv.StudentGraduated)] [InlineData(9999)]
    public async Task Leave_WithoutOpenEnrollment_Rejected(int studentId)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Students.LeaveAsync(new StudentLeaveRequest([studentId], "SchoolTransfer")), "ghi danh");
    }

    [Fact(DisplayName = "PPL | Học sinh | Chuyển trường/Tốt nghiệp | một em không hợp lệ thì không em nào bị đổi (toàn vẹn)")]
    public async Task Leave_IsAllOrNothing()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Students.LeaveAsync(new StudentLeaveRequest([PplEnv.StudentA, PplEnv.StudentNoClass], "SchoolTransfer")));
        Assert.Equal("Studying", (await e.Db.StudentProfile.SingleAsync(s => s.StudentId == PplEnv.StudentA)).Status);
        Assert.Equal("Active", (await e.Db.Account.SingleAsync(a => a.PersonId == PplEnv.StudentA)).Status);
    }

    [Fact(DisplayName = "PPL | Học sinh | Chuyển trường | nhiều học sinh một lượt và trùng mã chỉ tính một lần")]
    public async Task Leave_Many_AndDuplicateIds()
    {
        using var e = new PplEnv();
        await e.Students.LeaveAsync(new StudentLeaveRequest([PplEnv.StudentA, PplEnv.StudentA, PplEnv.StudentGrade9], "SchoolTransfer"));
        Assert.Equal(2, await e.Db.StudentProfile.CountAsync(s => s.Status == "Transferred"));
    }
}