using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Controllers;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using MSMS.Tests.Modules.Ppl;

namespace MSMS.Tests.Modules.Sch;

/// <summary>Thông tin trường (SCH-01): kiểm từng trường. Phần logo đã có test riêng trong SchoolProfileTests.</summary>
public class SchSchoolProfileFieldTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "msms-prof-" + Guid.NewGuid().ToString("N"));
    private readonly SchEnv _e = new();
    private SchoolProfileService Svc => new(_e.Db, SchTestSupport.UserOf(AccountTypes.SchoolAdmin, 900), _e.Clock, new DiskImageStorage(_root));
    private static SchoolProfileRequest Req(string name = "THCS Lập Lễ", string? addr = null, string? phone = null, string? email = null, string? web = null) => new(name, addr, phone, email, web);
    public void Dispose() { _e.Dispose(); try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch (IOException) { } }

    [Theory(DisplayName = "SCH | Thông tin trường | Tên trường | để trống bị từ chối")]
    [InlineData("")] [InlineData("  ")] [InlineData(null)]
    public async Task Name_Required(string? name) => await SchEnv.Rejects(() => Svc.UpdateAsync(Req(name!)), "Tên trường");

    [Fact(DisplayName = "SCH | Thông tin trường | Tên trường | đúng 200 ký tự được chấp nhận, 201 bị từ chối")]
    public async Task Name_Boundary()
    {
        Assert.Equal(200, (await Svc.UpdateAsync(Req(SchEnv.Text(200)))).Name!.Length);
        await SchEnv.Rejects(() => Svc.UpdateAsync(Req(SchEnv.Text(201))), "200");
    }

    [Theory(DisplayName = "SCH | Thông tin trường | Email | sai định dạng bị từ chối")]
    [InlineData("abc")] [InlineData("a@b")] [InlineData("a@b.vn, c@d.vn")] [InlineData("Tên <a@b.vn>")]
    public async Task Email_Invalid(string email) => await SchEnv.Rejects(() => Svc.UpdateAsync(Req(email: email)), "Email");

    [Fact(DisplayName = "SCH | Thông tin trường | Email | để trống được, đúng 150 ký tự được chấp nhận, 151 bị từ chối")]
    public async Task Email_Optional_AndBoundary()
    {
        Assert.Null((await Svc.UpdateAsync(Req(email: null))).Email);
        Assert.Equal(150, (await Svc.UpdateAsync(Req(email: new string('a', 140) + "@school.vn"))).Email!.Length);
        await SchEnv.Rejects(() => Svc.UpdateAsync(Req(email: new string('a', 141) + "@school.vn")), "150");
    }

    [Theory(DisplayName = "SCH | Thông tin trường | Website | chỉ nhận địa chỉ http:// hoặc https://")]
    [InlineData("http://truong.edu.vn", true)] [InlineData("https://truong.edu.vn/trang-chu", true)] [InlineData("ftp://truong.edu.vn", false)]
    [InlineData("truong.edu.vn", false)] [InlineData("javascript:alert(1)", false)] [InlineData("//truong.edu.vn", false)]
    public async Task Website_Scheme(string website, bool accepted)
    {
        if (accepted) Assert.Equal(website, (await Svc.UpdateAsync(Req(web: website))).Website);
        else await SchEnv.Rejects(() => Svc.UpdateAsync(Req(web: website)), "http");
    }

    [Fact(DisplayName = "SCH | Thông tin trường | Website | tối đa 200 ký tự")]
    public async Task Website_Length()
    {
        var at200 = "https://a.vn/" + new string('x', 200 - 13);
        Assert.Equal(200, (await Svc.UpdateAsync(Req(web: at200))).Website!.Length);
        await SchEnv.Rejects(() => Svc.UpdateAsync(Req(web: at200 + "x")), "200");
    }

    [Fact(DisplayName = "SCH | Thông tin trường | Địa chỉ và Số điện thoại | địa chỉ 255/256 ký tự, SĐT phải đúng 10 chữ số bắt đầu bằng 0")]
    public async Task Address_Phone_Boundaries()
    {
        var ok = await Svc.UpdateAsync(Req(addr: SchEnv.Text(255), phone: "0912345678"));
        Assert.Equal(255, ok.Address!.Length); Assert.Equal("0912345678", ok.PhoneNumber);
        await SchEnv.Rejects(() => Svc.UpdateAsync(Req(addr: SchEnv.Text(256))), "255");
        await SchEnv.Rejects(() => Svc.UpdateAsync(Req(phone: "091234567")), "10 chữ số");
        await SchEnv.Rejects(() => Svc.UpdateAsync(Req(phone: "abc")), "10 chữ số");
    }
}

/// <summary>Ghi danh học sinh vào lớp (SCH-05, 4.2.28-4.2.34): kiểm từng trường của yêu cầu ghi danh, chuyển lớp và kết thúc ghi danh.</summary>
public class SchEnrollmentFieldTests
{
    private static readonly DateOnly YStart = new(2026, 9, 5), YEnd = new(2027, 5, 31);

    [Fact(DisplayName = "SCH | Ghi danh | Ngày bắt đầu | đúng ngày đầu và ngày cuối năm học được chấp nhận")]
    public async Task StartDate_YearBoundaries()
    {
        using var e = new PplEnv();
        e.AddStudent(120, "Em 120", null); e.AddStudent(121, "Em 121", null);
        await e.Enrollments.EnrollAsync(PplEnv.Class6A, new EnrollRequest(120, YStart));
        await e.Enrollments.EnrollAsync(PplEnv.Class6A, new EnrollRequest(121, YEnd));
        Assert.Equal(2, await e.Db.Enrollment.CountAsync(x => x.StudentId >= 120));
    }

    [Theory(DisplayName = "SCH | Ghi danh | Ngày bắt đầu | trước ngày đầu hoặc sau ngày cuối năm học bị từ chối")]
    [InlineData(-1)] [InlineData(1)]
    public async Task StartDate_OutsideYear_Rejected(int sign)
    {
        using var e = new PplEnv();
        var date = sign < 0 ? YStart.AddDays(-1) : YEnd.AddDays(1);
        await PplEnv.Rejects(() => e.Enrollments.EnrollAsync(PplEnv.Class6A, new EnrollRequest(PplEnv.StudentNoClass, date)), "nằm trong năm học");
    }

    [Fact(DisplayName = "SCH | Ghi danh | Học sinh | không tồn tại, đã tốt nghiệp hoặc đã có ghi danh trong năm bị từ chối")]
    public async Task Student_Invalid()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Enrollments.EnrollAsync(PplEnv.Class6A, new EnrollRequest(9999, YStart)), "học sinh");
        await PplEnv.Rejects(() => e.Enrollments.EnrollAsync(PplEnv.Class6A, new EnrollRequest(PplEnv.StudentGraduated, YStart)), "Đang học");
        await PplEnv.Rejects(() => e.Enrollments.EnrollAsync(PplEnv.Class9A, new EnrollRequest(PplEnv.StudentA, YStart.AddDays(10))), "đã có ghi danh");
    }

    [Fact(DisplayName = "SCH | Ghi danh | Lớp | không tồn tại trả 404, lớp đã ngừng hoạt động bị từ chối")]
    public async Task Class_Invalid()
    {
        using var e = new PplEnv();
        e.Db.Class.Add(new Class { ClassId = 55, CampusId = PplEnv.Campus1, AcademicYearId = PplEnv.Year1, Name = "Cũ", GradeLevel = 7, IsActive = false, CreatedAt = e.Clock.Now });
        e.Db.SaveChanges();
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Enrollments.EnrollAsync(999, new EnrollRequest(PplEnv.StudentNoClass, YStart)));
        Assert.Equal(404, ex.StatusCode);
        await PplEnv.Rejects(() => e.Enrollments.EnrollAsync(55, new EnrollRequest(PplEnv.StudentNoClass, YStart)), "ngừng hoạt động");
    }

    [Fact(DisplayName = "SCH | Ghi danh | Ghi danh nhiều học sinh | danh sách trống, học sinh không tồn tại hoặc ngày ngoài năm học bị từ chối; hợp lệ thì ghi tất cả")]
    public async Task EnrollMany_Validation()
    {
        using var e = new PplEnv();
        e.AddStudent(130, "Em 130", null); e.AddStudent(131, "Em 131", null);
        await PplEnv.Rejects(() => e.Enrollments.EnrollManyAsync(PplEnv.Class6A, new EnrollManyRequest([], YStart)), "ít nhất");
        await PplEnv.Rejects(() => e.Enrollments.EnrollManyAsync(PplEnv.Class6A, new EnrollManyRequest([130, 9999], YStart)), "không tồn tại");
        await PplEnv.Rejects(() => e.Enrollments.EnrollManyAsync(PplEnv.Class6A, new EnrollManyRequest([130], YStart.AddDays(-1))), "năm học");
        Assert.Equal(0, await e.Db.Enrollment.CountAsync(x => x.StudentId == 130));    // lỗi thì không ghi em nào
        await e.Enrollments.EnrollManyAsync(PplEnv.Class6A, new EnrollManyRequest([130, 131], YStart));
        Assert.Equal(2, await e.Db.Enrollment.CountAsync(x => x.StudentId >= 130));
    }

    [Fact(DisplayName = "SCH | Ghi danh | Chuyển lớp | lớp đích không chọn, trùng lớp hiện tại, đã ngừng hoạt động hoặc khác năm học bị từ chối")]
    public async Task Transfer_Validation()
    {
        using var e = new PplEnv();
        SchTestSupport.AddYear(e.Db, 2, "2027-2028", new(2027, 9, 5), new(2028, 5, 31));
        SchTestSupport.AddClass(e.Db, 80, PplEnv.Campus1, 2, "7A", 7);
        SchTestSupport.AddClass(e.Db, 81, PplEnv.Campus1, PplEnv.Year1, "6B", 6, active: false);
        var id = (await e.Db.Enrollment.SingleAsync(x => x.StudentId == PplEnv.StudentA)).EnrollmentId;
        var eff = new DateOnly(2026, 10, 27);
        await PplEnv.Rejects(() => e.Enrollments.TransferAsync(id, new TransferRequest(0, eff)), "Chọn lớp");
        await PplEnv.Rejects(() => e.Enrollments.TransferAsync(id, new TransferRequest(PplEnv.Class6A, eff)), "trùng lớp hiện tại");
        await PplEnv.Rejects(() => e.Enrollments.TransferAsync(id, new TransferRequest(81, eff)), "ngừng hoạt động");
        await PplEnv.Rejects(() => e.Enrollments.TransferAsync(id, new TransferRequest(80, eff)), "cùng năm học");
    }

    [Fact(DisplayName = "SCH | Ghi danh | Chuyển lớp | hợp lệ: ghi danh cũ kết thúc ngày liền trước ngày hiệu lực, ghi danh mới mở từ ngày hiệu lực")]
    public async Task Transfer_Valid()
    {
        using var e = new PplEnv();
        var id = (await e.Db.Enrollment.SingleAsync(x => x.StudentId == PplEnv.StudentA)).EnrollmentId;
        await e.Enrollments.TransferAsync(id, new TransferRequest(PplEnv.Class9A, new DateOnly(2026, 10, 27)));
        var all = await e.Db.Enrollment.Where(x => x.StudentId == PplEnv.StudentA).OrderBy(x => x.StartDate).ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(new DateOnly(2026, 10, 26), all[0].EndDate);
        Assert.Equal(new DateOnly(2026, 10, 27), all[1].StartDate);
    }

    [Theory(DisplayName = "SCH | Ghi danh | Kết thúc ghi danh | lý do không hợp lệ hoặc danh sách trống bị từ chối")]
    [InlineData("")] [InlineData("Other")] [InlineData(null)]
    public async Task Close_InvalidReason(string? reason)
    {
        using var e = new PplEnv();
        var id = (await e.Db.Enrollment.SingleAsync(x => x.StudentId == PplEnv.StudentA)).EnrollmentId;
        await PplEnv.Rejects(() => e.Enrollments.CloseAsync(id, new CloseEnrollmentRequest(reason!)), "lý do");
        await PplEnv.Rejects(() => e.Enrollments.CloseManyAsync(new CloseManyRequest([], "SchoolTransfer")), "ít nhất");
    }

    [Fact(DisplayName = "SCH | Ghi danh | Kết thúc ghi danh | kết thúc hai lần bị từ chối; ghi danh không tồn tại trả 404")]
    public async Task Close_Twice_AndUnknown()
    {
        using var e = new PplEnv();
        var id = (await e.Db.Enrollment.SingleAsync(x => x.StudentId == PplEnv.StudentA)).EnrollmentId;
        await e.Enrollments.CloseAsync(id, new CloseEnrollmentRequest("ClassTransfer"));
        await PplEnv.Rejects(() => e.Enrollments.CloseAsync(id, new CloseEnrollmentRequest("ClassTransfer")), "đã kết thúc");
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Enrollments.CloseAsync(9999, new CloseEnrollmentRequest("ClassTransfer")));
        Assert.Equal(404, ex.StatusCode);
    }
}

/// <summary>Phân quyền các API của PPL (BR-01, tầng 1): mỗi hành động gắn đúng policy và policy chỉ cho đúng loại tài khoản.</summary>
public class PplAuthorizationTests
{
    private static string? PolicyOf(Type controller, string action)
    {
        var m = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Single(x => x.Name == action);
        return m.GetCustomAttributes<AuthorizeAttribute>().SingleOrDefault()?.Policy
               ?? controller.GetCustomAttributes<AuthorizeAttribute>().SingleOrDefault()?.Policy;
    }

    [Theory(DisplayName = "PPL | Phân quyền | Cán bộ | mỗi API gắn đúng policy")]
    [InlineData("List", Policies.CampusReader)] [InlineData("Create", Policies.SchoolAdmin)] [InlineData("Update", Policies.SchoolAdmin)]
    [InlineData("SetActive", Policies.SchoolAdmin)] [InlineData("Template", Policies.SchoolAdmin)] [InlineData("Import", Policies.SchoolAdmin)]
    public void Staff_ActionPolicy(string action, string policy) => Assert.Equal(policy, PolicyOf(typeof(StaffController), action));

    [Theory(DisplayName = "PPL | Phân quyền | Học sinh | mỗi API gắn đúng policy")]
    [InlineData("List", Policies.CampusReader)] [InlineData("Create", Policies.SchoolAdmin)] [InlineData("Update", Policies.SchoolAdmin)]
    [InlineData("Leave", Policies.SchoolAdmin)] [InlineData("Template", Policies.SchoolAdmin)] [InlineData("Import", Policies.SchoolAdmin)]
    public void Students_ActionPolicy(string action, string policy) => Assert.Equal(policy, PolicyOf(typeof(StudentsController), action));

    [Theory(DisplayName = "PPL | Phân quyền | Phụ huynh | mỗi API gắn đúng policy")]
    [InlineData("List", Policies.CampusReader)] [InlineData("Children", Policies.CampusReader)] [InlineData("Create", Policies.SchoolAdmin)]
    [InlineData("Update", Policies.SchoolAdmin)] [InlineData("Template", Policies.SchoolAdmin)] [InlineData("Import", Policies.SchoolAdmin)] [InlineData("ChangeRepresentative", Policies.SchoolAdmin)]
    public void Guardians_ActionPolicy(string action, string policy) => Assert.Equal(policy, PolicyOf(typeof(GuardiansController), action));

    [Theory(DisplayName = "PPL | Phân quyền | Trách nhiệm | mỗi API gắn đúng policy")]
    [InlineData("List", Policies.CampusReader)] [InlineData("Export", Policies.CampusReader)] [InlineData("History", Policies.CampusReader)]
    [InlineData("Assign", Policies.ResponsibilityManager)] [InlineData("End", Policies.ResponsibilityManager)]
    public void Responsibilities_ActionPolicy(string action, string policy) => Assert.Equal(policy, PolicyOf(typeof(ResponsibilitiesController), action));

    [Fact(DisplayName = "PPL | Phân quyền | Toàn bộ | không có API nào của PPL thiếu policy (mọi hành động đều yêu cầu đăng nhập và đúng vai trò)")]
    public void NoActionWithoutPolicy()
    {
        var controllers = new[] { typeof(StaffController), typeof(StudentsController), typeof(GuardiansController), typeof(ResponsibilitiesController) };
        foreach (var c in controllers)
            foreach (var m in c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                Assert.False(string.IsNullOrEmpty(PolicyOf(c, m.Name)), $"{c.Name}.{m.Name} chưa có policy");
    }

    private static async Task<bool> CanAsync(string accountType, string policy)
    {
        await using var provider = new ServiceCollection().AddLogging().AddAuthorization(Policies.Register).BuildServiceProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CurrentUser.TypeClaim, accountType)], "Test"));
        return (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, policy)).Succeeded;
    }

    [Theory(DisplayName = "PPL | Phân quyền | Policy CampusReader | chỉ School Admin, Hiệu trưởng, Hiệu phó đọc được")]
    [InlineData(AccountTypes.SchoolAdmin, true)] [InlineData(AccountTypes.Principal, true)] [InlineData(AccountTypes.VicePrincipal, true)]
    [InlineData(AccountTypes.Teacher, false)] [InlineData(AccountTypes.Student, false)] [InlineData(AccountTypes.Parent, false)]
    public async Task CampusReader_Matrix(string type, bool expected) => Assert.Equal(expected, await CanAsync(type, Policies.CampusReader));

    [Theory(DisplayName = "PPL | Phân quyền | Policy ResponsibilityManager | chỉ School Admin và Hiệu trưởng gán trách nhiệm")]
    [InlineData(AccountTypes.SchoolAdmin, true)] [InlineData(AccountTypes.Principal, true)] [InlineData(AccountTypes.VicePrincipal, false)]
    [InlineData(AccountTypes.Teacher, false)] [InlineData(AccountTypes.Student, false)] [InlineData(AccountTypes.Parent, false)]
    public async Task ResponsibilityManager_Matrix(string type, bool expected) => Assert.Equal(expected, await CanAsync(type, Policies.ResponsibilityManager));

    [Theory(DisplayName = "PPL | Phân quyền | Policy SchoolAdmin | chỉ School Admin tạo, sửa, import hồ sơ")]
    [InlineData(AccountTypes.SchoolAdmin, true)] [InlineData(AccountTypes.Principal, false)] [InlineData(AccountTypes.VicePrincipal, false)]
    [InlineData(AccountTypes.Teacher, false)] [InlineData(AccountTypes.Student, false)] [InlineData(AccountTypes.Parent, false)]
    public async Task SchoolAdmin_Matrix(string type, bool expected) => Assert.Equal(expected, await CanAsync(type, Policies.SchoolAdmin));
}