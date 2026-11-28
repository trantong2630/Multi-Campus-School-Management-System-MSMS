using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Adm;

/// <summary>ADM-06: mỗi thao tác của module khác ghi đúng một dòng nhật ký với danh mục, hành động, người thực hiện đúng và không lộ mật khẩu.</summary>
[Collection("api")]
public class AuditWriteTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();

    private Task<List<(string Category, string Action, int Actor, string? Detail)>> Since(DateTime t, int actor) => f.WithDbAsync(async db =>
        (await db.AuditLog.Where(a => a.CreatedAt >= t && a.ActorPersonId == actor).Select(a => new { a.Category, a.Action, a.ActorPersonId, a.Detail }).ToListAsync())
        .Select(a => (a.Category, a.Action, a.ActorPersonId, a.Detail)).ToList());

    [Fact]
    public async Task ADM_06_Actions_in_other_modules_write_one_audit_entry_each_without_passwords()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var t0 = f.Clock.Now.AddMinutes(-1);
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var principal = await f.LoginAsync(w.Principal.Username);
        var s = await w.NewScenarioAsync();
        var target = await w.NewAccountAsync("ADM06 tài khoản " + U(), "Staff", "Teacher", "Active");
        var accId = await f.WithDbAsync(db => db.Account.Where(a => a.PersonId == target.PersonId).Select(a => a.AccountId).SingleAsync());
        // Tài khoản: khóa, mở khóa, cấp lại mật khẩu, đổi loại
        Assert.Equal(204, (await admin.PutAsync($"/api/iam/accounts/{accId}/lock")).Status);
        Assert.Equal(204, (await admin.PutAsync($"/api/iam/accounts/{accId}/unlock")).Status);
        var reset = await admin.PostAsync($"/api/iam/accounts/{accId}/reset-password");
        Assert.Equal(200, reset.Status);
        var temp = reset.Body!["tempPassword"]!.GetValue<string>();
        Assert.Equal(200, (await admin.PutAsync($"/api/iam/accounts/{accId}/type", new { accountType = "VicePrincipal" })).Status);
        // Trách nhiệm
        var cls = await w.NewClassAsync(w.CampusAId, "AU" + U(), 6);
        Assert.Equal(204, (await principal.PostAsync("/api/ppl/responsibilities/assign", new { kind = "homeroom", targetId = cls, staffId = s.Sub.PersonId, startDate = World.Iso(f.Clock.Today) })).Status);
        // Điều chỉnh lịch: khai báo và xóa khai báo vắng
        var day = w.NextMonday();
        await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, 1);
        var head = await f.LoginAsync(s.Head.Username);
        var decl = await head.PostAsync("/api/scd/absences/department", new { teacherId = s.Absent.PersonId, fromDate = World.Iso(day), toDate = World.Iso(day), reason = "Ốm" });
        Assert.Equal(200, decl.Status);
        Assert.Equal(200, (await head.DeleteAsync($"/api/scd/absences/department/{decl.Body!["absenceId"]!.GetValue<int>()}")).Status);
        // Điểm danh: School Admin nhập bù
        var (pupil, _) = await w.NewStudentAsync(s.ClassAId, "HS ADM06 " + U());
        var sid = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, f.Clock.Today.AddDays(-3), 1);
        Assert.Equal(200, (await admin.PutAsync($"/api/att/sessions/{sid}", new { items = new[] { new { studentId = pupil.PersonId, status = "Present", note = (string?)null } } })).Status);

        var byAdmin = await Since(t0, w.SchoolAdmin.PersonId);
        foreach (var (cat, act) in new[] { ("Tài khoản", "Khóa tài khoản"), ("Tài khoản", "Mở khóa tài khoản"), ("Tài khoản", "Cấp lại mật khẩu tạm"), ("Tài khoản", "Đổi loại tài khoản"), ("Điểm danh", "Nhập bù hoặc sửa điểm danh") })
            Assert.True(1 <= byAdmin.Count(a => a.Category == cat && a.Action == act), cat + "/" + act + " | có: " + string.Join("; ", byAdmin.Select(a => a.Category + "/" + a.Action)));
        Assert.Contains(await Since(t0, w.Principal.PersonId), a => a.Category == "Trách nhiệm" && a.Action == "Gán trách nhiệm");
        var byHead = await Since(t0, s.Head.PersonId);
        Assert.Contains(byHead, a => a.Category == "Điều chỉnh lịch" && a.Action == "Khai báo giáo viên vắng");
        Assert.Contains(byHead, a => a.Category == "Điều chỉnh lịch" && a.Action == "Xóa khai báo giáo viên vắng");
        // không dòng nào chứa mật khẩu
        var all = await f.WithDbAsync(db => db.AuditLog.Where(a => a.CreatedAt >= t0).Select(a => a.Detail ?? "").ToListAsync());
        Assert.DoesNotContain(all, d => d.Contains(temp) || d.Contains(ApiFactory.Password));
        Assert.All(all.Where(d => d.Length > 0), d => Assert.DoesNotContain("password", d, StringComparison.OrdinalIgnoreCase));
    }
}
