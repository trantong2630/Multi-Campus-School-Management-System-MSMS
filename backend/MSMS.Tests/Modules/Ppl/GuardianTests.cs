using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Kiểm tra từng trường của hồ sơ phụ huynh (PPL-03, 4.3.23-4.3.24).</summary>
public class GuardianFieldTests
{
    private static GuardianRequest Valid() => new("Nguyễn Thị Lan", "0900000001", "lan@gmail.com", "12 Lạch Tray", "Nữ");
    private static Task<GuardianDto> Update(PplEnv e, GuardianRequest r) => e.Guardians.UpdateAsync(PplEnv.Parent2, r);

    [Theory(DisplayName = "PPL | Phụ huynh | Họ và tên | để trống hoặc chỉ khoảng trắng bị từ chối")]
    [InlineData("")] [InlineData("  ")] [InlineData(null)]
    public async Task FullName_Required(string? name)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Guardians.CreateAsync(Valid() with { FullName = name! }), "Họ và tên");
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Họ và tên | đúng 150 ký tự được chấp nhận, 151 bị từ chối, khoảng trắng thừa được gộp")]
    public async Task FullName_Boundary_AndNormalize()
    {
        using var e = new PplEnv();
        Assert.Equal(150, (await Update(e, Valid() with { FullName = PplEnv.Text(150) })).FullName.Length);
        await PplEnv.Rejects(() => Update(e, Valid() with { FullName = PplEnv.Text(151) }), "150");
        Assert.Equal("Lê Văn C", (await Update(e, Valid() with { FullName = "  Lê   Văn  C " })).FullName);
    }

    [Theory(DisplayName = "PPL | Phụ huynh | Số điện thoại | thiếu hoặc không có chữ số nào bị từ chối")]
    [InlineData("")] [InlineData(null)] [InlineData("   ")] [InlineData("abc")]
    public async Task Phone_Required(string? phone)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Guardians.CreateAsync(Valid() with { PhoneNumber = phone }), "Số điện thoại");
    }

    [Theory(DisplayName = "PPL | Phụ huynh | Số điện thoại | ít hơn 9 hoặc nhiều hơn 15 chữ số bị từ chối")]
    [InlineData("12345678")] [InlineData("1234567890123456")] [InlineData("09123abc78")] [InlineData("12-34")]
    public async Task Phone_Length_Invalid(string phone)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { PhoneNumber = phone }), "9 đến 15");
    }

    [Theory(DisplayName = "PPL | Phụ huynh | Số điện thoại | đúng 9 và 15 chữ số được chấp nhận, ký tự phân cách được bỏ khi lưu")]
    [InlineData("123456789", "123456789")] [InlineData("123456789012345", "123456789012345")]
    [InlineData("0900 000 001", "0900000001")] [InlineData("0900-000-001", "0900000001")] [InlineData("0900.000.001", "0900000001")] [InlineData("+84900000001", "0900000001")]
    public async Task Phone_Valid_Normalized(string input, string stored)
    {
        using var e = new PplEnv();
        Assert.Equal(stored, (await Update(e, Valid() with { PhoneNumber = input })).PhoneNumber);
    }

    [Theory(DisplayName = "PPL | Phụ huynh | Số điện thoại | trùng số của phụ huynh khác bị từ chối dù viết khác kiểu (+84, dấu cách)")]
    [InlineData("0912345678")] [InlineData("+84912345678")] [InlineData("091 234 5678")]
    public async Task Phone_Duplicate_AnyFormat(string phone)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { PhoneNumber = phone }), "PH000201");
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Số điện thoại | giữ nguyên số của chính mình khi sửa thì được")]
    public async Task Phone_KeepOwn()
    {
        using var e = new PplEnv();
        Assert.Equal("0987654321", (await Update(e, Valid() with { PhoneNumber = "0987654321" })).PhoneNumber);
    }

    [Theory(DisplayName = "PPL | Phụ huynh | Email | sai định dạng bị từ chối")]
    [InlineData("abc")] [InlineData("a@b")] [InlineData("a@b.vn, c@d.vn")] [InlineData("Tên <a@b.vn>")]
    public async Task Email_Invalid(string email)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { Email = email }), "Email");
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Email | để trống được chấp nhận, đúng 150 ký tự được chấp nhận, 151 bị từ chối")]
    public async Task Email_Optional_AndBoundary()
    {
        using var e = new PplEnv();
        Assert.Null((await Update(e, Valid() with { Email = null })).Email);
        Assert.Equal(150, (await Update(e, Valid() with { Email = new string('a', 140) + "@school.vn" })).Email!.Length);
        await PplEnv.Rejects(() => Update(e, Valid() with { Email = new string('a', 141) + "@school.vn" }), "150");
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Địa chỉ | đúng 255 ký tự được chấp nhận, 256 bị từ chối")]
    public async Task Address_Boundary()
    {
        using var e = new PplEnv();
        var dto = await Update(e, Valid() with { Address = PplEnv.Text(255) });
        Assert.NotNull(dto);
        await PplEnv.Rejects(() => Update(e, Valid() with { Address = PplEnv.Text(256) }), "255");
    }

    [Theory(DisplayName = "PPL | Phụ huynh | Giới tính | các cách viết hợp lệ được chuẩn hóa")]
    [InlineData("Nam", "Male")] [InlineData("Nữ", "Female")] [InlineData("Khác", "Other")] [InlineData(null, null)]
    public async Task Gender_Valid(string? input, string? stored)
    {
        using var e = new PplEnv();
        Assert.Equal(stored, (await Update(e, Valid() with { Gender = input })).Gender);
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Giới tính | giá trị lạ bị từ chối")]
    public async Task Gender_Invalid()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => Update(e, Valid() with { Gender = "robot" }), "Giới tính");
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Tạo hợp lệ | mọi trường hợp lệ qua được validate")]
    public async Task Create_Valid_PassesValidation()
    {
        using var e = new PplEnv();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => e.Guardians.CreateAsync(Valid()));
        Assert.Contains("Relational", ex.Message);
    }

    [Theory(DisplayName = "PPL | Phụ huynh | Sửa | không tồn tại hoặc không phải phụ huynh trả 404")]
    [InlineData(9999)] [InlineData(PplEnv.Teacher)] [InlineData(PplEnv.StudentA)]
    public async Task Update_NotAGuardian_NotFound(int id)
    {
        using var e = new PplEnv();
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Guardians.UpdateAsync(id, Valid()));
        Assert.Equal(404, ex.StatusCode);
    }
}

/// <summary>Chức năng danh bạ phụ huynh, xem các con và đổi phụ huynh đại diện (PPL-03, 4.3.22, 4.3.25-4.3.26).</summary>
public class GuardianFunctionTests
{
    [Fact(DisplayName = "PPL | Phụ huynh | Danh bạ | tìm theo tên, mã hồ sơ và số điện thoại (kể cả viết +84)")]
    public async Task List_Search()
    {
        using var e = new PplEnv();
        Assert.Equal(2, (await e.Guardians.ListAsync(null, 1, 20)).Total);
        Assert.Equal(1, (await e.Guardians.ListAsync("Phụ huynh A", 1, 20)).Total);
        Assert.Equal(1, (await e.Guardians.ListAsync("PH000202", 1, 20)).Total);
        Assert.Equal(1, (await e.Guardians.ListAsync("0912345678", 1, 20)).Total);
        Assert.Equal(1, (await e.Guardians.ListAsync("+84912345678", 1, 20)).Total);
        Assert.Equal(0, (await e.Guardians.ListAsync("không có", 1, 20)).Total);
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Danh bạ | hiện số con và tên các con (tối đa 3)")]
    public async Task List_ChildCount_AndSummary()
    {
        using var e = new PplEnv();
        var row = (await e.Guardians.ListAsync("Phụ huynh A", 1, 20)).Items.Single();
        Assert.Equal(1, row.ChildCount);
        Assert.Equal("Học sinh A", row.ChildrenSummary);
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Danh bạ | phân trang")]
    public async Task List_Paging()
    {
        using var e = new PplEnv();
        var p1 = await e.Guardians.ListAsync(null, 1, 1);
        var p2 = await e.Guardians.ListAsync(null, 2, 1);
        Assert.Single(p1.Items); Assert.Single(p2.Items); Assert.Equal(2, p1.Total);
        Assert.NotEqual(p1.Items[0].ParentId, p2.Items[0].ParentId);
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Danh bạ | Hiệu phó chỉ thấy phụ huynh có con ở phân hiệu mình phụ trách")]
    public async Task List_ViceScope()
    {
        using (var none = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP))
            Assert.Empty((await none.Guardians.ListAsync(null, 1, 20)).Items);
        using var led = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP);
        led.Db.CampusLeadership.Add(new CampusLeadership { CampusId = PplEnv.Campus1, StaffId = PplEnv.ViceP, StartDate = PplEnv.Today.AddDays(-5) });
        led.Db.SaveChanges();
        var items = (await led.Guardians.ListAsync(null, 1, 20)).Items;
        Assert.Single(items);                       // phụ huynh B chưa có con nên không hiện
        Assert.Equal(PplEnv.Parent1, items[0].ParentId);
    }

    [Theory(DisplayName = "PPL | Phụ huynh | Danh bạ | giáo viên, học sinh, phụ huynh không xem được")]
    [InlineData(AccountTypes.Teacher)] [InlineData(AccountTypes.Student)] [InlineData(AccountTypes.Parent)]
    public async Task List_NonManagement_Forbidden(string type)
    {
        using var e = new PplEnv(type, 777);
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Guardians.ListAsync(null, 1, 20));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Xem các con | hiện lớp, phân hiệu, trạng thái và quan hệ của từng con")]
    public async Task Children_ShowClassCampusStatus()
    {
        using var e = new PplEnv();
        var child = (await e.Guardians.ChildrenAsync(PplEnv.Parent1)).Single();
        Assert.Equal("Học sinh A", child.FullName);
        Assert.Equal("6A", child.ClassName);
        Assert.Equal("Phân hiệu 1", child.CampusName);
        Assert.Equal("Studying", child.StudentStatus);
        Assert.True(child.IsPrimary);
        Assert.Empty(await e.Guardians.ChildrenAsync(PplEnv.Parent2));
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Xem các con | phụ huynh không tồn tại trả 404")]
    public async Task Children_Unknown_NotFound()
    {
        using var e = new PplEnv();
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Guardians.ChildrenAsync(9999));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Đổi phụ huynh đại diện | sang phụ huynh có sẵn: liên kết cũ bị thay, chỉ còn một phụ huynh đại diện")]
    public async Task ChangeRepresentative_ToExisting()
    {
        using var e = new PplEnv();
        await e.Guardians.ChangeRepresentativeAsync(PplEnv.StudentA, new ChangeGuardianRequest(PplEnv.Parent2, null, "Bố"));
        var links = await e.Db.ParentStudentLink.Where(l => l.StudentId == PplEnv.StudentA).ToListAsync();
        var link = Assert.Single(links);
        Assert.Equal(PplEnv.Parent2, link.ParentId);
        Assert.True(link.IsPrimary);
        Assert.Equal("Bố", link.Relationship);
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Đổi phụ huynh đại diện | không nhập quan hệ thì giữ quan hệ cũ")]
    public async Task ChangeRepresentative_KeepsOldRelationship()
    {
        using var e = new PplEnv();
        await e.Guardians.ChangeRepresentativeAsync(PplEnv.StudentA, new ChangeGuardianRequest(PplEnv.Parent2, null, null));
        Assert.Equal("Mẹ", (await e.Db.ParentStudentLink.SingleAsync(l => l.StudentId == PplEnv.StudentA)).Relationship);
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Đổi phụ huynh đại diện | chọn cả hai hoặc không chọn gì bị từ chối")]
    public async Task ChangeRepresentative_BothOrNeither_Rejected()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Guardians.ChangeRepresentativeAsync(PplEnv.StudentA, new ChangeGuardianRequest(null, null, null)), "không chọn cả hai");
        await PplEnv.Rejects(() => e.Guardians.ChangeRepresentativeAsync(PplEnv.StudentA,
            new ChangeGuardianRequest(PplEnv.Parent2, new GuardianRequest("X", "0900000002", null, null), null)), "không chọn cả hai");
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Đổi phụ huynh đại diện | chọn lại đúng phụ huynh đang đại diện bị từ chối")]
    public async Task ChangeRepresentative_SameGuardian_Rejected()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Guardians.ChangeRepresentativeAsync(PplEnv.StudentA, new ChangeGuardianRequest(PplEnv.Parent1, null, null)), "đang là phụ huynh đại diện");
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Đổi phụ huynh đại diện | quan hệ quá 50 ký tự bị từ chối")]
    public async Task ChangeRepresentative_RelationshipTooLong()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Guardians.ChangeRepresentativeAsync(PplEnv.StudentA, new ChangeGuardianRequest(PplEnv.Parent2, null, PplEnv.Text(51))), "50");
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Đổi phụ huynh đại diện | học sinh hoặc phụ huynh không tồn tại trả 404")]
    public async Task ChangeRepresentative_Unknown_NotFound()
    {
        using var e = new PplEnv();
        var a = await Assert.ThrowsAsync<ApiException>(() => e.Guardians.ChangeRepresentativeAsync(9999, new ChangeGuardianRequest(PplEnv.Parent2, null, null)));
        var b = await Assert.ThrowsAsync<ApiException>(() => e.Guardians.ChangeRepresentativeAsync(PplEnv.StudentA, new ChangeGuardianRequest(9999, null, null)));
        Assert.Equal(404, a.StatusCode); Assert.Equal(404, b.StatusCode);
    }

    [Fact(DisplayName = "PPL | Phụ huynh | Đổi phụ huynh đại diện | khai phụ huynh mới sai số điện thoại thì không đổi gì")]
    public async Task ChangeRepresentative_NewGuardianInvalid_ChangesNothing()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Guardians.ChangeRepresentativeAsync(PplEnv.StudentA,
            new ChangeGuardianRequest(null, new GuardianRequest("Người mới", "123", null, null), null)));
        Assert.Equal(PplEnv.Parent1, (await e.Db.ParentStudentLink.SingleAsync(l => l.StudentId == PplEnv.StudentA)).ParentId);
    }
}