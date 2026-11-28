using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;
using static MSMS.Tests.Integration.Infrastructure.Roles;

namespace MSMS.Tests.Integration.Modules.Adm;

/// <summary>ADM: bảng tình hình, nhật ký hoạt động, trợ lý MSMS AI. Tên test bắt đầu bằng mã case (ADM_xx).</summary>
[Collection("api")]
public class AdmTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();

    [Fact]
    public async Task ADM_01_Situation_boards_for_the_principal_and_the_vice_principal()
    {
        await f.AssertGetByRoleAsync("/api/adm/dashboard", Principal, Vice);
        await w.EnsureAsync();
        var principal = await f.LoginAsync(w.Principal.Username);
        Assert.Equal(200, (await principal.GetAsync($"/api/adm/dashboard?campusId={w.CampusBId}&gradeLevel=7")).Status);
        var vice = await f.LoginAsync(w.ViceA.Username);
        var other = await vice.GetAsync($"/api/adm/dashboard?campusId={w.CampusBId}");
        Assert.Equal(403, other.Status);
        Assert.Equal("Không có quyền xem dữ liệu của phân hiệu này.", other.Message);
        var unknown = await principal.GetAsync("/api/adm/dashboard?campusId=987654");
        Assert.Equal(404, unknown.Status);
        Assert.Equal("Không tìm thấy phân hiệu đang hoạt động này.", unknown.Message);
    }

    [Fact]
    public async Task ADM_03_Admin_dashboard_is_for_the_school_admin_only()
    {
        await f.AssertGetByRoleAsync("/api/adm/dashboard/admin", Admin);
    }

    [Fact]
    public async Task ADM_04_Homeroom_overview_is_for_the_current_homeroom_teacher_only()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var cls = await w.NewClassAsync(w.CampusAId, "AD4" + U(), 6);
        var other = await w.NewClassAsync(w.CampusAId, "AD4o" + U(), 6);
        var hr = await w.NewStaffAsync("GVCN ADM " + U(), "Teacher", w.CampusAId, null);
        var plain = await w.NewStaffAsync("GV ADM " + U(), "Teacher", w.CampusAId, null);
        await f.WithDbAsync(async db =>
        {
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = cls, StaffId = hr.PersonId, StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var c = await f.LoginAsync(hr.Username);
        Assert.Equal(200, (await c.GetAsync("/api/adm/homeroom")).Status);
        var wrongClass = await c.GetAsync($"/api/adm/homeroom?classId={other}");
        Assert.Equal(403, wrongClass.Status);
        Assert.StartsWith("Không có quyền xem lớp này.", wrongClass.Message);
        var notHr = await (await f.LoginAsync(plain.Username)).GetAsync("/api/adm/homeroom");
        Assert.Equal(403, notHr.Status);
        Assert.Equal("Chỉ giáo viên đang chủ nhiệm mới xem được màn hình này.", notHr.Message);
    }

    [Fact]
    public async Task ADM_14_Department_board_is_for_a_current_department_head()
    {
        var s = await w.NewScenarioAsync();
        Assert.Equal(200, (await (await f.LoginAsync(s.Head.Username)).GetAsync("/api/adm/dashboard/department")).Status);
        var denied = await (await f.LoginAsync(s.Sub.Username)).GetAsync("/api/adm/dashboard/department");
        Assert.Equal(403, denied.Status);
        Assert.Equal("Chỉ tổ trưởng đang giữ trách nhiệm mới xem được màn hình này.", denied.Message);
    }

    [Fact]
    public async Task ADM_05_And_07_Audit_log_filters_and_access()
    {
        await w.EnsureAsync();
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        foreach (var category in new[] { "Tài khoản", "Phát hành thời khóa biểu", "Điều chỉnh lịch", "Trách nhiệm", "Điểm danh" })
            Assert.Equal(200, (await admin.GetAsync("/api/adm/audit-log?category=" + Uri.EscapeDataString(category))).Status);
        Assert.Equal(200, (await admin.GetAsync("/api/adm/audit-log/actors")).Status);
        var reversed = await admin.GetAsync("/api/adm/audit-log?from=2026-12-31&to=2026-01-01");
        Assert.Equal(400, reversed.Status);
        Assert.Equal("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.", reversed.Message);
        var missing = await admin.GetAsync("/api/adm/audit-log/987654321");
        Assert.Equal(404, missing.Status);
        Assert.Equal("Không tìm thấy bản ghi nhật ký.", missing.Message);
        // ADM-07: chỉ quản trị đọc; không có động từ ghi
        await f.AssertGetByRoleAsync("/api/adm/audit-log", Admin);
        foreach (var r in new[] { await admin.PostAsync("/api/adm/audit-log", new { }), await admin.PutAsync("/api/adm/audit-log/1", new { }), await admin.DeleteAsync("/api/adm/audit-log/1") })
            Assert.True(r.Status is 403 or 404 or 405, "audit log có endpoint ghi: " + r.Status);
    }

    // ------------------------------------------------------------------ MSMS AI

    private static object Ask(string q) => new { question = q, history = Array.Empty<object>() };

    [Fact]
    public async Task ADM_10_MSMS_AI_is_for_the_principal_and_the_vice_principal_only()
    {
        await w.EnsureAsync();
        f.Ai.Fail = false;
        var principal = await f.LoginAsync(w.Principal.Username);
        var r = await principal.PostAsync("/api/adm/ai/chat", Ask("Lớp nào có chuyên cần thấp nhất?"));
        Assert.Equal(200, r.Status);
        Assert.False(string.IsNullOrEmpty(r.Body!["text"]!.GetValue<string>()));
        Assert.Equal(200, (await (await f.LoginAsync(w.ViceA.Username)).PostAsync("/api/adm/ai/chat", Ask("Lớp nào có chuyên cần thấp nhất?"))).Status);
        // Hiệu phó chưa được gán phân hiệu nào
        var vp = await w.NewStaffAsync("HP không phân hiệu " + U(), "VicePrincipal", w.CampusAId, null);
        var noCampus = await (await f.LoginAsync(vp.Username)).PostAsync("/api/adm/ai/chat", Ask("x?"));
        Assert.Equal(403, noCampus.Status);
        Assert.Equal("Thầy cô chưa được gán phụ trách phân hiệu nào nên chưa dùng được MSMS AI.", noCampus.Message);
        var roles = await f.LoginAllAsync();
        foreach (var role in new[] { Admin, Teacher, Student, Parent })
            Assert.Equal(403, (await roles[role].PostAsync("/api/adm/ai/chat", Ask("x?"))).Status);
        Assert.Equal(200, (await principal.GetAsync("/api/adm/ai/people?q=nguyen")).Status);
    }

    [Fact]
    public async Task ADM_11_MSMS_AI_input_validation_and_failure_message()
    {
        await w.EnsureAsync();
        var principal = await f.LoginAsync((await w.NewStaffAsync("HT thử AI " + U(), "Principal", w.CampusAId, null)).Username);
        var blank = await principal.PostAsync("/api/adm/ai/chat", Ask("   "));
        Assert.Equal(400, blank.Status);
        Assert.Equal("Nhập câu hỏi cho MSMS AI.", blank.Message);
        var longQ = await principal.PostAsync("/api/adm/ai/chat", Ask(new string('a', 1001)));
        Assert.Equal(400, longQ.Status);
        Assert.Equal("Câu hỏi dài quá 1.000 ký tự. Rút gọn rồi hỏi lại.", longQ.Message);
        f.Ai.Fail = true;
        try
        {
            var failed = await principal.PostAsync("/api/adm/ai/chat", Ask("Một câu hỏi?"));
            Assert.Equal(400, failed.Status);
        }
        finally { f.Ai.Fail = false; }
    }

    [Fact]
    public async Task ADM_12_MSMS_AI_is_rate_limited_per_account()
    {
        await w.EnsureAsync();
        f.Ai.Fail = false;
        var a = await f.LoginAsync((await w.NewStaffAsync("HT AI giới hạn A " + U(), "Principal", w.CampusAId, null)).Username);
        var b = await f.LoginAsync((await w.NewStaffAsync("HT AI giới hạn B " + U(), "Principal", w.CampusAId, null)).Username);
        for (var i = 0; i < 6; i++) Assert.Equal(200, (await a.PostAsync("/api/adm/ai/chat", Ask("Câu hỏi " + i))).Status);
        Assert.Equal(429, (await a.PostAsync("/api/adm/ai/chat", Ask("Câu hỏi thứ bảy"))).Status);
        Assert.Equal(200, (await b.PostAsync("/api/adm/ai/chat", Ask("Câu hỏi của tài khoản khác"))).Status);
    }
}
