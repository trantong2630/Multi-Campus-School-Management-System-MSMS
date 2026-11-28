using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MSMS.Api.Common.Auth;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Security;

/// <summary>
/// Kiểm thử bảo mật tầng 1: loại tài khoản nào được gọi endpoint nào (ma trận policy), xác thực token, tuyến bị gỡ.
/// Tên test bắt đầu bằng mã case (TC_SEC_xxx).
/// </summary>
[Collection("api")]
public class PolicyMatrixTests(ApiFactory f)
{
    private const string Admin = "SchoolAdmin", Principal = "Principal", Vice = "VicePrincipal", Teacher = "Teacher", Student = "Student", Parent = "Parent";
    private static readonly string[] All = [Admin, Principal, Vice, Teacher, Student, Parent];
    private const string PolicyDenied = "Không có quyền thực hiện thao tác này.";

    private async Task<Dictionary<string, ApiClient>> RolesAsync()
    {
        await f.World.EnsureAsync();
        return new Dictionary<string, ApiClient>
        {
            [Admin] = await f.LoginAsPersonAsync("Trần Đức Kiên"), [Principal] = await f.LoginAsPersonAsync("Nguyễn Văn Hùng"),
            [Vice] = await f.LoginAsPersonAsync("Lê Thị Mai"), [Teacher] = await f.LoginAsPersonAsync("Phạm Minh Tuấn"),
            [Student] = await f.LoginAsPersonAsync("Hoàng Gia Bảo"), [Parent] = await f.LoginAsPersonAsync("Vũ Thị Lan"),
        };
    }

    /// <summary>Loại không thuộc <paramref name="allowed"/> phải nhận 403 của policy; loại được phép không được nhận 401 hoặc 403 của policy.</summary>
    private async Task AssertMatrixAsync(string method, string url, object? body, params string[] allowed)
    {
        var roles = await RolesAsync();
        foreach (var role in All)
        {
            var c = roles[role];
            var r = method switch
            {
                "GET" => await c.GetAsync(url),
                "POST" => await c.PostAsync(url, body),
                "PUT" => await c.PutAsync(url, body),
                _ => await c.DeleteAsync(url),
            };
            if (allowed.Contains(role))
            {
                Assert.True(r.Status != 401, $"{method} {url} as {role}: 401");
            }
            else
            {
                Assert.True(r.Status == 403, $"{method} {url} as {role}: mong đợi 403, nhận {r.Status}");
                Assert.Equal(PolicyDenied, r.Message);
            }
        }
    }

    // ------------------------------------------------------------------ Xác thực

    [Fact]
    public async Task TC_SEC_001_No_token_on_protected_endpoints()
    {
        var anon = f.Anonymous();
        foreach (var url in new[] { "/api/sch/campuses", "/api/att/today", "/api/lrn/my-assignments/1/attempt", "/api/com/notifications" })
        {
            var r = await anon.GetAsync(url);
            Assert.Equal(401, r.Status);
            Assert.Equal("UNAUTHORIZED", r.Code);
        }
    }

    [Fact]
    public async Task TC_SEC_002_Public_endpoints_stay_public_and_the_rest_needs_a_token()
    {
        var anon = f.Anonymous();
        Assert.NotEqual(401, (await anon.GetAsync("/api/school")).Status);
        Assert.NotEqual(401, (await anon.GetAsync("/api/school/logo")).Status);
        Assert.Equal("INVALID_CREDENTIALS", (await anon.PostAsync("/api/auth/login", new { username = "x", password = "y" })).Code);
        Assert.NotEqual(401, (await anon.PostAsync("/api/auth/forgot-password", new { username = "x" })).Status);
        Assert.Equal(401, (await anon.GetAsync("/api/iam/accounts")).Status);
    }

    [Fact]
    public async Task TC_SEC_003_And_004_Tampered_and_expired_tokens_are_rejected()
    {
        var admin = await f.LoginAsPersonAsync("Trần Đức Kiên");
        var token = admin.Http.DefaultRequestHeaders.Authorization!.Parameter!;
        // chữ ký bị sửa
        var parts = token.Split('.');
        var tampered = $"{parts[0]}.{parts[1]}.{parts[2][..^3]}abc";
        Assert.Equal(401, (await WithBearer(tampered).GetAsync("/api/sch/campuses")).Status);
        // thay payload (loại tài khoản) mà giữ chữ ký cũ
        var payload = Convert.FromBase64String(Pad(parts[1].Replace('-', '+').Replace('_', '/')));
        var forged = Convert.ToBase64String(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(payload).Replace("SchoolAdmin", "Principal"))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Equal(401, (await WithBearer($"{parts[0]}.{forged}.{parts[2]}").GetAsync("/api/sch/campuses")).Status);
        // token đã hết hạn, ký đúng khóa
        var jwt = f.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var original = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var expired = new JwtSecurityToken(jwt.Issuer, jwt.Issuer, original.Claims, DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(-2),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256));
        Assert.Equal(401, (await WithBearer(new JwtSecurityTokenHandler().WriteToken(expired)).GetAsync("/api/sch/campuses")).Status);
        Assert.Equal(200, (await admin.GetAsync("/api/sch/campuses")).Status);
    }

    private static string Pad(string s) => s + new string('=', (4 - s.Length % 4) % 4);

    private ApiClient WithBearer(string token)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return new ApiClient(c);
    }

    [Fact]
    public async Task TC_SEC_005_And_006_Token_of_a_locked_or_pending_password_account()
    {
        await f.World.EnsureAsync();
        var u = await f.World.NewAccountAsync("SEC005 " + World.Uniq(), "Staff", "Teacher", "Active");
        var c = await f.LoginAsync(u.Username);
        await f.WithDbAsync(async db =>
        {
            (await db.Account.SingleAsync(a => a.PersonId == u.PersonId)).Status = "Locked";
            await db.SaveChangesAsync();
        });
        var locked = await c.GetAsync("/api/att/today");
        Assert.Equal(401, locked.Status);
        Assert.Equal("ACCOUNT_LOCKED", locked.Code);
        var p = await f.World.NewAccountAsync("SEC006 " + World.Uniq(), "Staff", "Teacher", "PendingPasswordChange");
        var pc = await f.LoginAsync(p.Username);
        var blocked = await pc.GetAsync("/api/att/today");
        Assert.Equal(403, blocked.Status);
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", blocked.Code);
        Assert.Equal(200, (await pc.GetAsync("/api/auth/me")).Status);
    }

    [Fact]
    public async Task TC_SEC_007_Token_after_logout_password_change_and_type_change()
    {
        await f.World.EnsureAsync();
        var admin = await f.LoginAsync(f.World.SchoolAdmin.Username);
        var a = await f.World.NewAccountAsync("SEC007 a " + World.Uniq(), "Staff", "Teacher", "Active");
        var ca = await f.LoginAsync(a.Username);
        Assert.Equal(204, (await ca.PostAsync("/api/auth/logout")).Status);
        Assert.Equal(401, (await ca.GetAsync("/api/auth/me")).Status);
        var b = await f.World.NewAccountAsync("SEC007 b " + World.Uniq(), "Staff", "Teacher", "Active");
        var old = await f.LoginAsync(b.Username);
        var other = await f.LoginAsync(b.Username);
        Assert.Equal(204, (await other.PostAsync("/api/auth/change-password", new { currentPassword = ApiFactory.Password, newPassword = "Mat-khau-moi-1" })).Status);
        Assert.Equal(401, (await old.GetAsync("/api/auth/me")).Status);
        var c = await f.World.NewAccountAsync("SEC007 c " + World.Uniq(), "Staff", "Teacher", "Active");
        var cc = await f.LoginAsync(c.Username);
        var id = await f.WithDbAsync(db => db.Account.Where(x => x.PersonId == c.PersonId).Select(x => x.AccountId).SingleAsync());
        Assert.Equal(200, (await admin.PutAsync($"/api/iam/accounts/{id}/type", new { accountType = "VicePrincipal" })).Status);
        Assert.Equal(401, (await cc.GetAsync("/api/auth/me")).Status);
    }

    [Fact]
    public async Task TC_SEC_011_Passwords_are_never_returned()
    {
        var admin = await f.LoginAsPersonAsync("Trần Đức Kiên");
        foreach (var url in new[] { "/api/iam/accounts?pageSize=50", "/api/iam/accounts/directory?personType=Staff&pageSize=50", "/api/auth/me", "/api/adm/audit-log?pageSize=50" })
        {
            var r = await admin.GetAsync(url);
            Assert.Equal(200, r.Status);
            Assert.DoesNotContain("passwordHash", r.Raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("tempPassword", r.Raw, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("refreshToken", r.Raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------------------ Ma trận policy

    [Fact]
    public async Task TC_SEC_012_SchoolAdmin_only_policy()
    {
        await AssertMatrixAsync("GET", "/api/iam/accounts", null, Admin);
        await AssertMatrixAsync("GET", "/api/adm/audit-log", null, Admin);
        await AssertMatrixAsync("GET", "/api/adm/dashboard/admin", null, Admin);
        await AssertMatrixAsync("POST", "/api/sch/academic-years", new { }, Admin);
        await AssertMatrixAsync("POST", "/api/sch/departments", new { }, Admin);
        await AssertMatrixAsync("POST", "/api/ppl/guardians", new { }, Admin);
        await AssertMatrixAsync("POST", "/api/sch/classes/1/enrollments", new { }, Admin);
    }

    [Fact]
    public async Task TC_SEC_013_Management_and_CampusReader_policy()
    {
        await AssertMatrixAsync("GET", "/api/ppl/guardians", null, Admin, Principal, Vice);
        await AssertMatrixAsync("GET", "/api/ppl/responsibilities/history?kind=homeroom&targetId=1", null, Admin, Principal, Vice);
        await AssertMatrixAsync("GET", "/api/att/sessions", null, Admin, Principal, Vice);
        await AssertMatrixAsync("GET", "/api/sch/stats/catalog", null, Admin, Principal, Vice);
        await AssertMatrixAsync("GET", "/api/sch/stats/campuses", null, Admin, Principal);
    }

    [Fact]
    public async Task TC_SEC_014_TimetableManager_is_the_vice_principal_only()
    {
        await AssertMatrixAsync("GET", "/api/scd/timetable/template", null, Vice);
        await AssertMatrixAsync("POST", "/api/scd/adjust/makeup", new { }, Vice);
        await AssertMatrixAsync("POST", "/api/scd/absences/1/approve", null, Vice);
        await AssertMatrixAsync("POST", "/api/scd/absences/1/return", new { reason = "x" }, Vice);
        await AssertMatrixAsync("POST", "/api/scd/absences/pending/1/resolve", new { action = "Cancel" }, Vice);
        await AssertMatrixAsync("POST", "/api/scd/timetable/versions/1/publish", new { }, Vice);
        await AssertMatrixAsync("DELETE", "/api/scd/timetable/versions/1", null, Vice);
    }

    [Fact]
    public async Task TC_SEC_015_TimetableReader_is_principal_and_vice_principal()
    {
        await f.World.EnsureAsync();
        var c = f.World.CampusAId;
        await AssertMatrixAsync("GET", $"/api/scd/absences?campusId={c}", null, Principal, Vice);
        await AssertMatrixAsync("GET", $"/api/scd/absences/pending?campusId={c}", null, Principal, Vice);
        await AssertMatrixAsync("GET", "/api/scd/responsibilities", null, Principal, Vice);
        await AssertMatrixAsync("GET", $"/api/scd/timetable/versions?campusId={c}", null, Principal, Vice);
    }

    [Fact]
    public async Task TC_SEC_016_ResponsibilityManager_is_the_principal_only()
    {
        await AssertMatrixAsync("POST", "/api/ppl/responsibilities/assign", new { }, Principal);
        await AssertMatrixAsync("POST", "/api/ppl/responsibilities/end", new { }, Principal);
        await AssertMatrixAsync("POST", "/api/ppl/responsibilities/cancel", new { }, Principal);
    }

    [Fact]
    public async Task TC_SEC_017_Teacher_only_endpoints()
    {
        await AssertMatrixAsync("GET", "/api/att/today", null, Teacher);
        await AssertMatrixAsync("GET", "/api/lrn/questions/my-subjects", null, Teacher);
        await AssertMatrixAsync("GET", "/api/scd/schedule/mine", null, Teacher);
        await AssertMatrixAsync("GET", "/api/scd/absences/department", null, Teacher);
        await AssertMatrixAsync("GET", "/api/par/leave-requests/homeroom", null, Teacher);
        await AssertMatrixAsync("GET", "/api/lrn/assignments/class-subjects?classId=1", null, Teacher);
    }

    [Fact]
    public async Task TC_SEC_018_Student_only_endpoints()
    {
        await AssertMatrixAsync("GET", "/api/att/me", null, Student);
        await AssertMatrixAsync("GET", "/api/scd/schedule/student", null, Student);
        await AssertMatrixAsync("GET", "/api/lrn/my-assignments/1/attempt", null, Student);
    }

    [Fact]
    public async Task TC_SEC_019_Parent_only_endpoints()
    {
        await AssertMatrixAsync("GET", "/api/att/child", null, Parent);
        await AssertMatrixAsync("GET", "/api/par/leave-requests/my-children", null, Parent);
        await AssertMatrixAsync("GET", "/api/lrn/results/children", null, Parent);
        await AssertMatrixAsync("GET", "/api/par/conversations/available-teachers", null, Parent);
    }

    [Fact]
    public async Task TC_SEC_020_StudentOrParent_ReportViewer_and_ConversationParticipant()
    {
        await AssertMatrixAsync("GET", "/api/lrn/results/1", null, Student, Parent);
        await AssertMatrixAsync("GET", "/api/par/weekly-reports/weeks", null, Teacher, Student, Parent);
        await AssertMatrixAsync("GET", "/api/par/conversations/1/messages", null, Teacher, Parent);
    }

    [Fact]
    public async Task TC_SEC_021_AttendanceWriter_DashboardReader_and_removed_endpoints()
    {
        await AssertMatrixAsync("PUT", "/api/att/sessions/1", new { }, Teacher, Admin);
        await AssertMatrixAsync("GET", "/api/adm/dashboard", null, Principal, Vice);
        await AssertMatrixAsync("POST", "/api/adm/ai/chat", new { question = "x", history = Array.Empty<object>() }, Principal, Vice);
        var roles = await RolesAsync();
        foreach (var url in new[] { "/api/adm/exports/attendance", "/api/adm/progress", "/api/adm/report", "/api/lrn/department/assignments" })
            foreach (var role in All)
            {
                // tuyến đã gỡ không còn endpoint; FallbackPolicy từ chối mọi loại tài khoản nên không trả dữ liệu nào
                var r = await roles[role].GetAsync(url);
                Assert.True(r.Status is 403 or 404, $"{role} {url}: {r.Status}");
                Assert.DoesNotContain("items", r.Raw);
            }
    }

    [Fact]
    public async Task TC_SEC_022_An_undeclared_route_is_denied_by_default()
    {
        var roles = await RolesAsync();
        foreach (var role in All)
        {
            var r = await roles[role].GetAsync("/api/does/not/exist");
            Assert.True(r.Status is 404 or 401 or 403, $"{role}: {r.Status}");
            Assert.NotEqual(200, r.Status);
        }
    }

    [Fact]
    public async Task TC_SEC_036_Validation_errors_do_not_expose_internals()
    {
        var admin = await f.LoginAsPersonAsync("Trần Đức Kiên");
        foreach (var (url, body) in new (string, object)[] { ("/api/iam/accounts", new { personId = "abc" }), ("/api/sch/academic-years", new { name = 123 }), ("/api/iam/accounts/bulk", new { personIds = "x" }) })
        {
            var r = await admin.PostAsync(url, body);
            Assert.Equal(400, r.Status);
            Assert.DoesNotContain("   at ", r.Raw);
            Assert.DoesNotContain("System.", r.Raw);
            Assert.DoesNotContain("MSMS.Api", r.Raw);   // tên lớp nội bộ không được lộ ra ngoài
            Assert.DoesNotContain("SELECT", r.Raw, StringComparison.OrdinalIgnoreCase);
        }
    }
}
