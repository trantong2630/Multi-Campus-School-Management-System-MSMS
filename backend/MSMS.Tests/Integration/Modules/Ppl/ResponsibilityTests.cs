using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Ppl;

/// <summary>PPL-04: gán, thay, kết thúc, hủy hẹn trách nhiệm (BR-09). Tên test bắt đầu bằng mã case (RESP_xx).</summary>
[Collection("api")]
public class ResponsibilityTests(ApiFactory f)
{
    private World w => f.World;

    private async Task<ApiClient> PrincipalAsync() { await w.EnsureAsync(); f.Clock.Reset(); return await f.LoginAsync(w.Principal.Username); }

    private static object Assign(string kind, int target, int staff, DateOnly start) => new { kind, targetId = target, staffId = staff, startDate = World.Iso(start) };

    private async Task<Who> TeacherAsync() => await w.NewStaffAsync("GV trách nhiệm " + World.Uniq(), "Teacher", w.CampusAId, null);

    private DateOnly Today => f.Clock.Today;

    [Fact]
    public async Task RESP_01_Assign_a_homeroom_teacher_ends_by_itself_at_the_year_end()
    {
        var p = await PrincipalAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RS1" + World.Uniq(), 6);
        var t = await TeacherAsync();
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, t.PersonId, Today))).Status);
        var rec = await f.WithDbAsync(db => db.HomeroomAssignment.SingleAsync(h => h.ClassId == cls));
        Assert.Equal(Today, rec.StartDate);
        Assert.Equal(new DateOnly(2027, 5, 31), rec.EndDate);
        var list = await p.GetAsync("/api/ppl/responsibilities?kind=homeroom");
        Assert.Equal(200, list.Status);
        Assert.Contains(t.FullName, list.Raw);
    }

    [Fact]
    public async Task RESP_02_Start_date_in_the_past_is_rejected()
    {
        var p = await PrincipalAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RS2" + World.Uniq(), 6);
        var t = await TeacherAsync();
        var r = await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, t.PersonId, Today.AddDays(-1)));
        Assert.Equal(400, r.Status);
        Assert.StartsWith("Ngày hiệu lực không được trước hôm nay", r.Message);
    }

    [Fact]
    public async Task RESP_03_Assign_validates_target_and_staff()
    {
        var p = await PrincipalAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RS3" + World.Uniq(), 6);
        var inactiveClass = await w.NewClassAsync(w.CampusAId, "RS3x" + World.Uniq(), 6);
        await f.WithDbAsync(async db => { (await db.Class.SingleAsync(c => c.ClassId == inactiveClass)).IsActive = false; await db.SaveChangesAsync(); });
        var good = await TeacherAsync();
        Assert.Equal("Lớp không tồn tại hoặc đã ngừng hoạt động.", (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", inactiveClass, good.PersonId, Today))).Message);
        var inactive = await TeacherAsync();
        await f.WithDbAsync(async db => { (await db.StaffProfile.SingleAsync(s => s.StaffId == inactive.PersonId)).IsActive = false; await db.SaveChangesAsync(); });
        Assert.StartsWith("Cán bộ đã ngừng hoạt động", (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, inactive.PersonId, Today))).Message);
        var wrongType = await w.NewStaffAsync("Hiệu phó không phải GV " + World.Uniq(), "VicePrincipal", w.CampusAId, null);
        Assert.Contains("chưa có tài khoản loại", (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, wrongType.PersonId, Today))).Message);
        var locked = await w.NewStaffAsync("GV bị khóa " + World.Uniq(), "Teacher", w.CampusAId, null);
        await f.WithDbAsync(async db => { (await db.Account.SingleAsync(a => a.PersonId == locked.PersonId)).Status = "Locked"; await db.SaveChangesAsync(); });
        Assert.Contains("đang bị khóa. Mở khóa trước khi gán.", (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, locked.PersonId, Today))).Message);
    }

    [Fact]
    public async Task RESP_04_To_06_Replace_effective_today_with_validations_and_audit()
    {
        var p = await PrincipalAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RS4" + World.Uniq(), 6);
        var oldT = await TeacherAsync(); var newT = await TeacherAsync();
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, oldT.PersonId, Today))).Status);
        // RESP-06: cùng người
        var same = await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, oldT.PersonId, Today));
        Assert.Equal(400, same.Status);
        Assert.Equal("Người này đang đảm nhiệm sẵn. Chọn người khác để thay.", same.Message);
        // RESP-05: thay có hiệu lực ngày mai
        var future = await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, newT.PersonId, Today.AddDays(1)));
        Assert.Equal(400, future.Status);
        Assert.StartsWith("Thay người có hiệu lực ngay hôm nay.", future.Message);
        // RESP-04: thay hôm nay
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, newT.PersonId, Today))).Status);
        var rows = await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.ClassId == cls).OrderBy(h => h.Id).ToListAsync());
        Assert.Equal(2, rows.Count);
        Assert.Equal(Today, rows[0].EndDate);
        Assert.Equal(newT.PersonId, rows[1].StaffId);
        var history = await p.GetAsync($"/api/ppl/responsibilities/history?kind=homeroom&targetId={cls}");
        Assert.Equal(200, history.Status);
        Assert.Equal(2, history.Body!.AsArray().Count);
        Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Category == "Trách nhiệm" && a.Action == "Thay người đảm nhiệm")) >= 1);
    }

    [Fact]
    public async Task RESP_07_End_date_rules()
    {
        var p = await PrincipalAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RS7" + World.Uniq(), 6);
        var t = await TeacherAsync();
        await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, t.PersonId, Today));
        var id = await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.ClassId == cls).Select(h => h.Id).SingleAsync());
        var y = await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "homeroom", recordId = id, endDate = World.Iso(Today.AddDays(-1)) });
        Assert.Equal(400, y.Status);
        Assert.StartsWith("Ngày kết thúc không được trước hôm nay", y.Message);
        // bản ghi bắt đầu ở tương lai: kết thúc trước ngày bắt đầu
        var futureCls = await w.NewClassAsync(w.CampusAId, "RS7f" + World.Uniq(), 6);
        var t2 = await TeacherAsync();
        await f.WithDbAsync(async db =>
        {
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = futureCls, StaffId = t2.PersonId, StartDate = Today.AddDays(10), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var fid = await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.ClassId == futureCls).Select(h => h.Id).SingleAsync());
        var before = await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "homeroom", recordId = fid, endDate = World.Iso(Today.AddDays(5)) });
        Assert.Equal(400, before.Status);
        Assert.StartsWith("Ngày kết thúc không được trước ngày bắt đầu", before.Message);
        // bước 4: kết thúc hôm nay
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "homeroom", recordId = id, endDate = World.Iso(Today) })).Status);
        // bước 3: bản ghi đã kết thúc từ hôm qua
        var oldCls = await w.NewClassAsync(w.CampusAId, "RS7o" + World.Uniq(), 6);
        await f.WithDbAsync(async db =>
        {
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = oldCls, StaffId = t2.PersonId, StartDate = Today.AddDays(-20), EndDate = Today.AddDays(-1), CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var oldId = await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.ClassId == oldCls).Select(h => h.Id).SingleAsync());
        var again = await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "homeroom", recordId = oldId, endDate = World.Iso(Today) });
        Assert.Equal(400, again.Status);
        Assert.Equal("Trách nhiệm này đã kết thúc từ trước.", again.Message);
    }

    [Fact]
    public async Task RESP_08_And_09_Successor_after_a_future_end_date_and_overlap()
    {
        var p = await PrincipalAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RS8" + World.Uniq(), 6);
        var t = await TeacherAsync(); var n1 = await TeacherAsync(); var n2 = await TeacherAsync();
        await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, t.PersonId, Today));
        var id = await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.ClassId == cls).Select(h => h.Id).SingleAsync());
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "homeroom", recordId = id, endDate = World.Iso(Today.AddDays(7)) })).Status);
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, n1.PersonId, Today.AddDays(8)))).Status);
        var overlap = await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, n2.PersonId, Today.AddDays(5)));
        Assert.Equal(400, overlap.Status);
        // RESP-09: đã có người bắt đầu sau ngày chọn
        var later = await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, n2.PersonId, Today.AddDays(2)));
        Assert.Equal(400, later.Status);
    }

    [Fact]
    public async Task RESP_11_Cancel_a_scheduled_responsibility()
    {
        var p = await PrincipalAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RS11" + World.Uniq(), 6);
        var t = await TeacherAsync(); var next = await TeacherAsync();
        await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, t.PersonId, Today));
        var cur = await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.ClassId == cls).Select(h => h.Id).SingleAsync());
        await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "homeroom", recordId = cur, endDate = World.Iso(Today.AddDays(3)) });
        await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, next.PersonId, Today.AddDays(4)));
        var future = await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.ClassId == cls && h.StartDate > Today).Select(h => h.Id).SingleAsync());
        var started = await p.PostAsync("/api/ppl/responsibilities/cancel", new { kind = "homeroom", recordId = cur });
        Assert.Equal(400, started.Status);
        Assert.Equal("Trách nhiệm này đã bắt đầu nên không hủy được. Dùng Kết thúc trách nhiệm.", started.Message);
        Assert.Equal(404, (await p.PostAsync("/api/ppl/responsibilities/cancel", new { kind = "homeroom", recordId = 99999999 })).Status);
        var badKind = await p.PostAsync("/api/ppl/responsibilities/cancel", new { kind = "x", recordId = future });
        Assert.Equal(400, badKind.Status);
        Assert.Equal("Loại trách nhiệm không hợp lệ.", badKind.Message);
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/cancel", new { kind = "homeroom", recordId = future })).Status);
        Assert.False(await f.WithDbAsync(db => db.HomeroomAssignment.AnyAsync(h => h.Id == future)));
        Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Category == "Trách nhiệm" && a.Action == "Hủy lịch hẹn trách nhiệm")) >= 1);
    }

    [Fact]
    public async Task RESP_12_Vice_principal_cannot_see_history_outside_the_led_campus()
    {
        await w.EnsureAsync();
        var clsB = await w.NewClassAsync(w.CampusBId, "RS12B" + World.Uniq(), 6);
        var vice = await f.LoginAsync(w.ViceA.Username);
        var r = await vice.GetAsync($"/api/ppl/responsibilities/history?kind=homeroom&targetId={clsB}");
        Assert.Equal(403, r.Status);
        Assert.Equal("Không có quyền xem lịch sử trách nhiệm ngoài phân hiệu phụ trách.", r.Message);
    }

    [Fact]
    public async Task RESP_13_And_14_Only_the_principal_may_assign_and_kind_values_are_checked()
    {
        await w.EnsureAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RS13" + World.Uniq(), 6);
        var t = await TeacherAsync();
        foreach (var user in new[] { w.SchoolAdmin.Username, w.ViceA.Username })
            foreach (var path in new[] { "assign", "end", "cancel" })
                Assert.Equal(403, (await (await f.LoginAsync(user)).PostAsync("/api/ppl/responsibilities/" + path, new { })).Status);
        foreach (var name in new[] { "Phạm Minh Tuấn", "Hoàng Gia Bảo", "Vũ Thị Lan" })
            Assert.Equal(403, (await (await f.LoginAsPersonAsync(name)).PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, t.PersonId, Today))).Status);
        foreach (var user in new[] { w.SchoolAdmin.Username, w.Principal.Username, w.ViceA.Username })
            Assert.Equal(200, (await (await f.LoginAsync(user)).GetAsync("/api/ppl/responsibilities?kind=homeroom")).Status);
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        Assert.Equal("Loại trách nhiệm không hợp lệ.", (await admin.GetAsync("/api/ppl/responsibilities?kind=unknown")).Message);
        Assert.Equal("Loại trách nhiệm không hợp lệ.", (await admin.GetAsync("/api/ppl/responsibilities/history?kind=unknown&targetId=1")).Message);
        var p = await PrincipalAsync();
        Assert.Equal("Loại trách nhiệm không hợp lệ.", (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("unknown", cls, t.PersonId, Today))).Message);
    }

    [Fact]
    public async Task RESP_20_A_homeroom_record_cannot_start_after_the_year_end()
    {
        var p = await PrincipalAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RS20" + World.Uniq(), 6);
        var t = await TeacherAsync();
        var end = new DateOnly(2027, 5, 31);
        var r = await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, t.PersonId, end.AddDays(1)));
        Assert.Equal(400, r.Status);
        Assert.Equal("Năm học của lớp đã kết thúc ngày 31/05/2027. Chọn ngày hiệu lực trong năm học của lớp.", r.Message);
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, t.PersonId, end))).Status);
        Assert.Equal(end, await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.ClassId == cls).Select(h => h.EndDate!.Value).SingleAsync()));
    }

    [Fact]
    public async Task RESP_22_Ending_a_campus_leader_is_blocked_without_a_successor_while_classes_are_active()
    {
        var p = await PrincipalAsync();
        // phân hiệu mới: có lớp hoạt động và một Hiệu phó phụ trách
        var campus = await f.WithDbAsync(async db =>
        {
            var c = new Campus { Code = "RS" + World.Uniq(), Name = "Phân hiệu RS22 " + World.Uniq(), IsActive = true, CreatedAt = f.Clock.Now };
            db.Campus.Add(c);
            await db.SaveChangesAsync();
            return c.CampusId;
        });
        await w.NewClassAsync(campus, "RS22" + World.Uniq(), 6);
        var vp = await w.NewStaffAsync("HP RS22 " + World.Uniq(), "VicePrincipal", campus, null);
        var vpNext = await w.NewStaffAsync("HP kế nhiệm " + World.Uniq(), "VicePrincipal", campus, null);
        await p.PostAsync("/api/ppl/responsibilities/assign", Assign("campus", campus, vp.PersonId, Today));
        var rec = await f.WithDbAsync(db => db.CampusLeadership.Where(l => l.CampusId == campus).Select(l => l.Id).SingleAsync());
        // bước 1: kết thúc hôm nay, chưa có người nhận
        var r1 = await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "campus", recordId = rec, endDate = World.Iso(Today) });
        Assert.Equal(400, r1.Status);
        Assert.Contains("còn 1 lớp hoạt động. Dùng Thay người để giao cho Hiệu phó khác, không kết thúc khi chưa có người nhận.", r1.Message);
        Assert.Null(await f.WithDbAsync(db => db.CampusLeadership.Where(l => l.Id == rec).Select(l => l.EndDate).SingleAsync()));
        // bước 2: kết thúc vào ngày mai
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "campus", recordId = rec, endDate = World.Iso(Today.AddDays(1)) })).Status);
        // bước 3: có người kế nhiệm từ ngày mai rồi kết thúc hôm nay -> được
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("campus", campus, vpNext.PersonId, Today.AddDays(2)))).Status);
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "campus", recordId = rec, endDate = World.Iso(Today) })).Status);
        // bước 4: phân hiệu không còn lớp hoạt động được để trống
        var empty = await f.WithDbAsync(async db =>
        {
            var c = new Campus { Code = "RE" + World.Uniq(), Name = "Phân hiệu trống " + World.Uniq(), IsActive = true, CreatedAt = f.Clock.Now };
            db.Campus.Add(c);
            await db.SaveChangesAsync();
            return c.CampusId;
        });
        var vpEmpty = await w.NewStaffAsync("HP phân hiệu trống " + World.Uniq(), "VicePrincipal", empty, null);
        await p.PostAsync("/api/ppl/responsibilities/assign", Assign("campus", empty, vpEmpty.PersonId, Today));
        var rec2 = await f.WithDbAsync(db => db.CampusLeadership.Where(l => l.CampusId == empty).Select(l => l.Id).SingleAsync());
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "campus", recordId = rec2, endDate = World.Iso(Today) })).Status);
        // bước 5: tổ trưởng và chủ nhiệm kết thúc được mà không bị chặn
        var s = await w.NewScenarioAsync();
        var head = await f.WithDbAsync(db => db.DepartmentHead.Where(h => h.StaffId == s.Head.PersonId).Select(h => h.Id).SingleAsync());
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "department", recordId = head, endDate = World.Iso(Today) })).Status);
    }

    [Fact]
    public async Task RESP_16_An_ended_homeroom_teacher_loses_the_homeroom_features_at_once()
    {
        var p = await PrincipalAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RS16" + World.Uniq(), 6);
        var t = await TeacherAsync();
        await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, t.PersonId, Today));
        var tc = await f.LoginAsync(t.Username);
        Assert.Equal(200, (await tc.GetAsync("/api/adm/homeroom")).Status);
        var id = await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.ClassId == cls).Select(h => h.Id).SingleAsync());
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "homeroom", recordId = id, endDate = World.Iso(Today) })).Status);
        var r = await tc.GetAsync("/api/adm/homeroom");
        Assert.Equal(403, r.Status);
        Assert.Equal("Chỉ giáo viên đang chủ nhiệm mới xem được màn hình này.", r.Message);
        var summary = await tc.GetAsync($"/api/att/class-summary?classId={cls}&from={World.Iso(Today)}&to={World.Iso(Today)}");
        Assert.Equal(403, summary.Status);
        Assert.Equal("Không có quyền xem chuyên cần của lớp này.", summary.Message);
    }

    [Fact]
    public async Task RESP_18_And_19_Department_board_and_deactivation_guard()
    {
        await w.EnsureAsync();
        var s = await w.NewScenarioAsync();
        var head = await f.LoginAsync(s.Head.Username);
        Assert.Equal(200, (await head.GetAsync("/api/adm/dashboard/department")).Status);
        await f.WithDbAsync(async db => { (await db.Subject.SingleAsync(x => x.SubjectId == s.SubjectId)).IsActive = false; await db.SaveChangesAsync(); });   // tổ không còn môn hoạt động
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var block = await admin.PutAsync($"/api/sch/departments/{s.DepartmentId}/active", new { isActive = false });
        Assert.Equal(400, block.Status);
        Assert.Equal("Tổ đang có tổ trưởng. Kết thúc trách nhiệm tổ trưởng trước.", block.Message);
        var p = await PrincipalAsync();
        var id = await f.WithDbAsync(db => db.DepartmentHead.Where(h => h.StaffId == s.Head.PersonId).Select(h => h.Id).SingleAsync());
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "department", recordId = id, endDate = World.Iso(Today) })).Status);
        var denied = await head.GetAsync("/api/adm/dashboard/department");
        Assert.Equal(403, denied.Status);
        Assert.Equal("Chỉ tổ trưởng đang giữ trách nhiệm mới xem được màn hình này.", denied.Message);
    }
}
