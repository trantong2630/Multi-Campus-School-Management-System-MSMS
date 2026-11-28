using System.Net.Http.Json;
using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Iam;

/// <summary>IAM-02: cấp, khóa, mở khóa, cấp lại mật khẩu, đổi loại tài khoản. Tên test bắt đầu bằng mã case (ACC_xx).</summary>
[Collection("api")]
public class AccountTests(ApiFactory f)
{
    private World w => f.World;
    private const string Pw = ApiFactory.Password;

    private async Task<ApiClient> AdminAsync() { await w.EnsureAsync(); return await f.LoginAsync(w.SchoolAdmin.Username); }

    /// <summary>Hồ sơ (người) chưa có tài khoản.</summary>
    private Task<int> ProfileAsync(string personType, string name) => f.WithDbAsync(async db =>
    {
        await w.EnsureAsync();
        var code = (personType == "Staff" ? "CB7" : personType == "Student" ? "HS7" : "PH7") + World.Uniq();
        var p = new Person { PersonType = personType, PersonCode = code, FullName = name, DateOfBirth = new DateOnly(1990, 1, 1), Gender = "Male", CreatedAt = f.Clock.Now };
        db.Person.Add(p);
        if (personType == "Staff") db.StaffProfile.Add(new StaffProfile { Staff = p, PrimaryCampusId = w.CampusAId, IsActive = true, Position = "GV", HireDate = new DateOnly(2015, 9, 1) });
        if (personType == "Student") db.StudentProfile.Add(new StudentProfile { Student = p, Status = "Studying", AdmissionDate = new DateOnly(2026, 9, 5) });
        await db.SaveChangesAsync();
        return p.PersonId;
    });

    private Task<int> AccountIdOf(int personId) => f.WithDbAsync(db => db.Account.Where(a => a.PersonId == personId).Select(a => a.AccountId).SingleAsync());
    private Task<string> StatusOf(int personId) => f.WithDbAsync(db => db.Account.Where(a => a.PersonId == personId).Select(a => a.Status).SingleAsync());

    [Fact]
    public async Task ACC_01_Create_an_account_for_a_staff_profile()
    {
        var admin = await AdminAsync();
        var personId = await ProfileAsync("Staff", "Hồ sơ ACC01 " + World.Uniq());
        var dir = await admin.GetAsync("/api/iam/accounts/directory?personType=Staff&pageSize=200&search=ACC01");
        Assert.Equal(200, dir.Status);
        Assert.Contains(personId.ToString(), dir.Raw);
        var r = await admin.PostAsync("/api/iam/accounts", new { personId, accountType = "Teacher" });
        Assert.Equal(200, r.Status);
        Assert.False(string.IsNullOrEmpty(r.Body!["tempPassword"]!.GetValue<string>()));
        Assert.Equal("PendingPasswordChange", await StatusOf(personId));
        var list = await admin.GetAsync("/api/iam/accounts?search=" + Uri.EscapeDataString(r.Body["username"]!.GetValue<string>()));
        Assert.Contains(r.Body["username"]!.GetValue<string>(), list.Raw);
        Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Category == "Tài khoản" && a.Action == "Tạo tài khoản")) >= 1);
    }

    [Fact]
    public async Task ACC_02_Account_type_must_match_the_profile_type()
    {
        var admin = await AdminAsync();
        var student = await ProfileAsync("Student", "Hồ sơ HS ACC02 " + World.Uniq());
        var staff = await ProfileAsync("Staff", "Hồ sơ CB ACC02 " + World.Uniq());
        foreach (var (id, type) in new[] { (student, "Teacher"), (staff, "Student"), (staff, "Parent") })
        {
            var r = await admin.PostAsync("/api/iam/accounts", new { personId = id, accountType = type });
            Assert.Equal(400, r.Status);
            Assert.Equal("Loại tài khoản không phù hợp với loại hồ sơ.", r.Message);
        }
    }

    [Fact]
    public async Task ACC_03_A_profile_cannot_get_a_second_account()
    {
        var admin = await AdminAsync();
        var staff = await ProfileAsync("Staff", "Hồ sơ ACC03 " + World.Uniq());
        Assert.Equal(200, (await admin.PostAsync("/api/iam/accounts", new { personId = staff, accountType = "Teacher" })).Status);
        var again = await admin.PostAsync("/api/iam/accounts", new { personId = staff, accountType = "Teacher" });
        Assert.Equal(400, again.Status);
        Assert.Equal(1, await f.WithDbAsync(db => db.Account.CountAsync(a => a.PersonId == staff)));
    }

    [Fact]
    public async Task ACC_04_And_05_Bulk_creation_limits_and_skipping()
    {
        var admin = await AdminAsync();
        var empty = await admin.PostAsync("/api/iam/accounts/bulk", new { personIds = Array.Empty<int>(), accountType = "Teacher" });
        Assert.Equal(400, empty.Status);
        Assert.StartsWith("Chọn ít nhất một hồ sơ", empty.Message);
        var tooMany = await admin.PostAsync("/api/iam/accounts/bulk", new { personIds = Enumerable.Range(900000, 201).ToArray(), accountType = "Teacher" });
        Assert.Equal(400, tooMany.Status);
        Assert.Equal("Mỗi lần tạo tối đa 200 tài khoản.", tooMany.Message);
        // ACC-05: hồ sơ hợp lệ, hồ sơ đã có tài khoản và mã không tồn tại
        var a = await ProfileAsync("Staff", "Hồ sơ ACC05 A " + World.Uniq());
        var b = await ProfileAsync("Staff", "Hồ sơ ACC05 B " + World.Uniq());
        var has = await ProfileAsync("Staff", "Hồ sơ ACC05 đã có " + World.Uniq());
        await admin.PostAsync("/api/iam/accounts", new { personId = has, accountType = "Teacher" });
        var r = await admin.PostAsync("/api/iam/accounts/bulk", new { personIds = new[] { a, b, has, 987654321 }, accountType = "Teacher" });
        Assert.Equal(200, r.Status);
        Assert.Equal(2, r.Body!["created"]!.AsArray().Count);
        Assert.Equal(2, r.Body["skipped"]!.AsArray().Count);
    }

    [Fact]
    public async Task ACC_06_Temp_password_sheet_is_an_excel_file_for_1_to_200_accounts()
    {
        var admin = await AdminAsync();
        var p = await ProfileAsync("Staff", "Hồ sơ ACC06 " + World.Uniq());
        var created = await admin.PostAsync("/api/iam/accounts", new { personId = p, accountType = "Teacher" });
        var item = created.Body!;
        var ok = await admin.Http.PostAsJsonAsync("/api/iam/accounts/temp-password-sheet", new { items = new[] { item } });
        Assert.Equal(200, (int)ok.StatusCode);
        Assert.Contains("spreadsheet", ok.Content.Headers.ContentType?.MediaType);
        Assert.True((await ok.Content.ReadAsByteArrayAsync()).Length > 1000);
        foreach (var items in new[] { Array.Empty<object>(), Enumerable.Range(0, 201).Select(_ => (object)item).ToArray() })
        {
            var bad = await admin.PostAsync("/api/iam/accounts/temp-password-sheet", new { items });
            Assert.Equal(400, bad.Status);
            Assert.Equal("Chỉ tải danh sách vừa cấp, từ 1 đến 200 tài khoản.", bad.Message);
        }
    }

    [Fact]
    public async Task ACC_08_And_09_Lock_impact_matches_the_real_result_and_releases_sessions_and_duties()
    {
        var admin = await AdminAsync();
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var date = w.NextMonday();
        var s1 = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 1);
        var s2 = await w.NewSessionAsync(s.ClassBId, s.SubjectId, s.Absent.PersonId, date, 2);
        await f.WithDbAsync(async db =>
        {
            db.DepartmentHead.Add(new DepartmentHead { DepartmentId = s.DepartmentId, StaffId = s.Absent.PersonId, StartDate = f.Clock.Today.AddDays(30), CreatedAt = f.Clock.Now });
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = s.ClassAId, StaffId = s.Absent.PersonId, StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var id = await AccountIdOf(s.Absent.PersonId);
        var impact = await admin.GetAsync($"/api/iam/accounts/{id}/lock-impact");
        Assert.Equal(200, impact.Status);
        Assert.Equal(2, impact.Body!["sessionCount"]!.GetValue<int>());
        Assert.Single(impact.Body["duties"]!.AsArray());
        Assert.Single(impact.Body["upcomingDuties"]!.AsArray());
        Assert.Equal(204, (await admin.PutAsync($"/api/iam/accounts/{id}/lock")).Status);
        foreach (var sid in new[] { s1, s2 })
        {
            var x = await w.SessionAsync(sid);
            Assert.Equal("Pending", x.Status);
            Assert.Null(x.TeacherId);
        }
        Assert.Equal(f.Clock.Today, await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.StaffId == s.Absent.PersonId).Select(h => h.EndDate!.Value).SingleAsync()));
        Assert.False(await f.WithDbAsync(db => db.DepartmentHead.AnyAsync(h => h.StaffId == s.Absent.PersonId)));   // hẹn tương lai bị hủy
        Assert.True(await f.WithDbAsync(db => db.Notification.CountAsync(n => n.RecipientPersonId == w.ViceA.PersonId && n.NotificationType == "SessionPending" && n.CreatedAt >= f.Clock.Now.AddMinutes(-5))) >= 1);
        // ACC-10: mở khóa không khôi phục trách nhiệm
        Assert.Equal(204, (await admin.PutAsync($"/api/iam/accounts/{id}/unlock")).Status);
        Assert.Equal(f.Clock.Today, await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.StaffId == s.Absent.PersonId).Select(h => h.EndDate!.Value).SingleAsync()));
    }

    [Fact]
    public async Task ACC_11_And_12_Lock_validations()
    {
        var admin = await AdminAsync();
        var self = await AccountIdOf(w.SchoolAdmin.PersonId);
        var r1 = await admin.PutAsync($"/api/iam/accounts/{self}/lock");
        Assert.Equal(400, r1.Status);
        Assert.Equal("Không khóa được tài khoản đang đăng nhập.", r1.Message);
        var locked = await w.NewAccountAsync("ACC12 khóa " + World.Uniq(), "Staff", "Teacher", "Locked");
        var active = await w.NewAccountAsync("ACC12 hoạt động " + World.Uniq(), "Staff", "Teacher", "Active");
        Assert.Equal("Tài khoản đã bị khóa từ trước.", (await admin.PutAsync($"/api/iam/accounts/{await AccountIdOf(locked.PersonId)}/lock")).Message);
        Assert.Equal("Tài khoản không ở trạng thái Đã khóa.", (await admin.PutAsync($"/api/iam/accounts/{await AccountIdOf(active.PersonId)}/unlock")).Message);
    }

    [Fact]
    public async Task ACC_13_Unlock_restores_the_right_status()
    {
        var admin = await AdminAsync();
        var changed = await w.NewAccountAsync("ACC13 đã đổi " + World.Uniq(), "Staff", "Teacher", "PendingPasswordChange");
        Assert.Equal(204, (await (await f.LoginAsync(changed.Username)).PostAsync("/api/auth/change-password", new { currentPassword = Pw, newPassword = "Mat-khau-moi-1" })).Status);   // đã tự đổi mật khẩu
        var never = await w.NewAccountAsync("ACC13 chưa đổi " + World.Uniq(), "Staff", "Teacher", "PendingPasswordChange");
        foreach (var u in new[] { changed, never }) Assert.Equal(204, (await admin.PutAsync($"/api/iam/accounts/{await AccountIdOf(u.PersonId)}/lock")).Status);
        foreach (var u in new[] { changed, never }) Assert.Equal(204, (await admin.PutAsync($"/api/iam/accounts/{await AccountIdOf(u.PersonId)}/unlock")).Status);
        Assert.Equal("Active", await StatusOf(changed.PersonId));
        Assert.Equal("PendingPasswordChange", await StatusOf(never.PersonId));
    }

    [Fact]
    public async Task ACC_15_Reset_password_issues_a_temporary_password_and_revokes_sessions()
    {
        var admin = await AdminAsync();
        var u = await w.NewAccountAsync("ACC15 " + World.Uniq(), "Staff", "Teacher", "Active");
        var old = await f.LoginAsync(u.Username);
        var r = await admin.PostAsync($"/api/iam/accounts/{await AccountIdOf(u.PersonId)}/reset-password");
        Assert.Equal(200, r.Status);
        var temp = r.Body!["tempPassword"]!.GetValue<string>();
        Assert.Equal(401, (await old.GetAsync("/api/auth/me")).Status);
        var oldLogin = await f.Anonymous().PostAsync("/api/auth/login", new { username = u.Username, password = Pw });
        Assert.Equal("INVALID_CREDENTIALS", oldLogin.Code);
        var newLogin = await f.Anonymous().PostAsync("/api/auth/login", new { username = u.Username, password = temp });
        Assert.Equal(200, newLogin.Status);
        Assert.True(newLogin.Body!["user"]!["mustChangePassword"]!.GetValue<bool>());
        Assert.Equal("PendingPasswordChange", await StatusOf(u.PersonId));
        Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Action == "Cấp lại mật khẩu tạm")) >= 1);
    }

    [Fact]
    public async Task ACC_16_And_17_Change_account_type()
    {
        var admin = await AdminAsync();
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var date = w.NextMonday();
        var sid = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 1);
        var id = await AccountIdOf(s.Absent.PersonId);
        var r = await admin.PutAsync($"/api/iam/accounts/{id}/type", new { accountType = "VicePrincipal" });
        Assert.Equal(200, r.Status);
        Assert.False(string.IsNullOrEmpty(r.Body!["tempPassword"]!.GetValue<string>()));
        Assert.Equal("Pending", (await w.SessionAsync(sid)).Status);
        Assert.Equal("VicePrincipal", await f.WithDbAsync(db => db.Account.Where(a => a.PersonId == s.Absent.PersonId).Select(a => a.AccountType).SingleAsync()));
        Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Action == "Đổi loại tài khoản")) >= 1);
        // ACC-17
        var self = await AccountIdOf(w.SchoolAdmin.PersonId);
        Assert.Equal("Không đổi loại của tài khoản đang đăng nhập.", (await admin.PutAsync($"/api/iam/accounts/{self}/type", new { accountType = "Principal" })).Message);
        Assert.Equal("Tài khoản đã đúng loại này.", (await admin.PutAsync($"/api/iam/accounts/{id}/type", new { accountType = "VicePrincipal" })).Message);
        var student = await w.NewAccountAsync("ACC17 HS " + World.Uniq(), "Student", "Student", "Active");
        var bad = await admin.PutAsync($"/api/iam/accounts/{await AccountIdOf(student.PersonId)}/type", new { accountType = "Teacher" });
        Assert.Equal(400, bad.Status);
        Assert.Equal("Loại tài khoản không phù hợp với loại hồ sơ.", bad.Message);
    }

    [Fact]
    public async Task ACC_18_Account_actions_are_visible_in_the_audit_log_in_Vietnamese()
    {
        var admin = await AdminAsync();
        var u = await w.NewAccountAsync("ACC18 " + World.Uniq(), "Staff", "Teacher", "Active");
        var id = await AccountIdOf(u.PersonId);
        await admin.PutAsync($"/api/iam/accounts/{id}/lock");
        await admin.PutAsync($"/api/iam/accounts/{id}/unlock");
        var list = await admin.GetAsync("/api/adm/audit-log?category=" + Uri.EscapeDataString("Tài khoản") + "&pageSize=50");
        Assert.Equal(200, list.Status);
        Assert.Contains("Khóa tài khoản", list.Raw);
        Assert.Contains("Mở khóa tài khoản", list.Raw);
        var first = list.Body!["items"]!.AsArray().First()!;
        var detail = await admin.GetAsync("/api/adm/audit-log/" + first["auditId"]!.GetValue<long>());
        Assert.Equal(200, detail.Status);
        Assert.DoesNotContain("TempPassword", detail.Raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Pw, detail.Raw);
    }

    [Fact]
    public async Task ACC_19_Account_list_search_and_filters()
    {
        var admin = await AdminAsync();
        var tag = "ACC19" + World.Uniq();
        await w.NewAccountAsync(tag + " khóa", "Staff", "Teacher", "Locked");
        await w.NewAccountAsync(tag + " ok", "Staff", "Teacher", "Active");
        await w.NewAccountAsync(tag + " hp", "Staff", "VicePrincipal", "Locked");
        var r = await admin.GetAsync($"/api/iam/accounts?search={tag}&accountType=Teacher&status=Locked&page=1&pageSize=20");
        Assert.Equal(200, r.Status);
        Assert.Single(r.Body!["items"]!.AsArray());
        Assert.Equal(1, r.Body["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task ACC_20_Only_the_school_admin_uses_the_account_endpoints()
    {
        await w.EnsureAsync();
        Assert.Equal(401, (await f.Anonymous().GetAsync("/api/iam/accounts")).Status);
        foreach (var name in new[] { "Nguyễn Văn Hùng", "Lê Thị Mai", "Phạm Minh Tuấn", "Hoàng Gia Bảo", "Vũ Thị Lan" })
        {
            var c = await f.LoginAsPersonAsync(name);
            Assert.Equal(403, (await c.GetAsync("/api/iam/accounts")).Status);
            Assert.Equal(403, (await c.PostAsync("/api/iam/accounts", new { personId = 1, accountType = "Teacher" })).Status);
        }
    }

    [Fact]
    public async Task ACC_21_The_mobile_phone_rule_applies_to_own_contact_data()
    {
        var u = await w.NewAccountAsync("ACC21 " + World.Uniq(), "Staff", "Teacher", "Active");
        var c = await f.LoginAsync(u.Username);
        Assert.Equal(200, (await c.PutAsync("/api/iam/profile", new { phoneNumber = "0912345678" })).Status);
        foreach (var bad in new[] { "09123", "0212345678" })
        {
            var r = await c.PutAsync("/api/iam/profile", new { phoneNumber = bad });
            Assert.Equal(400, r.Status);
            Assert.Equal("Số điện thoại di động phải gồm đúng 10 chữ số, đầu số 03, 05, 07, 08 hoặc 09.", r.Message);
        }
        Assert.Equal(200, (await c.PutAsync("/api/iam/profile", new { phoneNumber = "" })).Status);
    }
}
