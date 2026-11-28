using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Att;

/// <summary>ATT: điểm danh theo tiết (BR-17, BR-18, BR-09). Tên test bắt đầu bằng mã case (ATT_xx).</summary>
[Collection("api")]
public class AttendanceTests(ApiFactory f)
{
    private World w => f.World;

    private sealed record Ctx(Scenario S, ApiClient Teacher, DateOnly Date, long Session, List<Who> Students);

    private static DateTime At(DateOnly d, int h, int m) => d.ToDateTime(new TimeOnly(h, m));

    /// <summary>Tiết 1 buổi sáng (07:00-07:45) của giáo viên "Vắng" ở lớp A, 3 học sinh đang học; đồng hồ đặt vào 07:00 ngày của tiết.</summary>
    private async Task<Ctx> ArrangeAsync(int students = 3, DateOnly? date = null)
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var d = date ?? w.NextMonday();
        var sid = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, d, 1);
        var list = new List<Who>();
        for (var i = 0; i < students; i++) list.Add((await w.NewStudentAsync(s.ClassAId, $"HS ATT {i} {World.Uniq()}")).Who);
        var teacher = await f.LoginAsync(s.Absent.Username);
        f.Clock.Set(At(d, 7, 0));
        return new Ctx(s, teacher, d, sid, list);
    }

    private static object Items(IEnumerable<Who> students, string status) =>
        new { items = students.Select(x => new { studentId = x.PersonId, status, note = (string?)null }).ToArray() };

    [Fact]
    public async Task ATT_01_Teacher_takes_attendance_for_own_session()
    {
        var c = await ArrangeAsync();
        try
        {
            var today = await c.Teacher.GetAsync("/api/att/today");
            Assert.Equal(200, today.Status);
            Assert.Contains(c.Session.ToString(), today.Raw);
            var sheet = await c.Teacher.GetAsync($"/api/att/sessions/{c.Session}");
            Assert.Equal(200, sheet.Status);
            Assert.Equal(3, sheet.Body!["students"]!.AsArray().Count);
            var save = await c.Teacher.PutAsync($"/api/att/sessions/{c.Session}", new { items = new object[]
            {
                new { studentId = c.Students[0].PersonId, status = "Present", note = (string?)null },
                new { studentId = c.Students[1].PersonId, status = "Late", note = "Muộn 5 phút" },
                new { studentId = c.Students[2].PersonId, status = "Absent", note = (string?)null },
            } });
            Assert.Equal(200, save.Status);
            var rows = save.Body!["students"]!.AsArray();
            Assert.Equal("Present", rows.Single(r => r!["studentId"]!.GetValue<int>() == c.Students[0].PersonId)!["label"]!.GetValue<string>());
            Assert.Equal("Late", rows.Single(r => r!["studentId"]!.GetValue<int>() == c.Students[1].PersonId)!["label"]!.GetValue<string>());
            Assert.Equal("UnexcusedAbsent", rows.Single(r => r!["studentId"]!.GetValue<int>() == c.Students[2].PersonId)!["label"]!.GetValue<string>());
            Assert.Equal("Muộn 5 phút", rows.Single(r => r!["studentId"]!.GetValue<int>() == c.Students[1].PersonId)!["note"]!.GetValue<string>());
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ATT_02_Teacher_cannot_take_attendance_after_the_session_day()
    {
        var c = await ArrangeAsync();
        try
        {
            f.Clock.Set(At(c.Date.AddDays(1), 0, 0));
            var r = await c.Teacher.PutAsync($"/api/att/sessions/{c.Session}", Items(c.Students, "Present"));
            Assert.Equal(400, r.Status);
            Assert.Equal("Tiết đã qua ngày, giáo viên không sửa được nữa. Liên hệ quản trị hệ thống nếu cần sửa.", r.Message);
            f.Clock.Set(At(c.Date, 23, 59));
            Assert.Equal(200, (await c.Teacher.PutAsync($"/api/att/sessions/{c.Session}", Items(c.Students, "Present"))).Status);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ATT_03_Teacher_cannot_take_attendance_for_a_session_of_another_teacher()
    {
        var c = await ArrangeAsync();
        try
        {
            var other = await f.LoginAsync(c.S.Sub.Username);
            var get = await other.GetAsync($"/api/att/sessions/{c.Session}");
            Assert.Equal(403, get.Status);
            Assert.Equal("Không có quyền xem điểm danh của tiết này.", get.Message);
            var put = await other.PutAsync($"/api/att/sessions/{c.Session}", Items(c.Students, "Present"));
            Assert.Equal(403, put.Status);
            Assert.Equal("Chỉ giáo viên của tiết mới điểm danh được tiết này.", put.Message);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ATT_04_School_admin_enters_late_and_cannot_use_a_future_session()
    {
        var past = w.NextMonday().AddDays(-14);
        var c = await ArrangeAsync(date: past);
        var future = await w.NewSessionAsync(c.S.ClassAId, c.S.SubjectId, c.S.Absent.PersonId, past.AddDays(14), 2);
        try
        {
            f.Clock.Set(At(past.AddDays(3), 9, 0));
            var admin = await f.LoginAsync(w.SchoolAdmin.Username);
            var saved = await admin.PutAsync($"/api/att/sessions/{c.Session}", Items(c.Students, "Present"));
            Assert.Equal(200, saved.Status);
            Assert.All(saved.Body!["students"]!.AsArray(), r => Assert.True(r!["isBackfilled"]!.GetValue<bool>()));
            var bad = await admin.PutAsync($"/api/att/sessions/{future}", Items(c.Students, "Present"));
            Assert.Equal(400, bad.Status);
            Assert.Equal("Tiết chưa diễn ra. Chỉ nhập bù cho tiết có ngày từ hôm nay trở về trước.", bad.Message);
            Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Category == "Điểm danh" && a.Action == "Nhập bù hoặc sửa điểm danh")) >= 1);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ATT_05_Only_scheduled_sessions_accept_attendance()
    {
        var c = await ArrangeAsync();
        try
        {
            var pending = await w.NewSessionAsync(c.S.ClassAId, c.S.SubjectId, null, c.Date, 2, "Pending");
            var cancelled = await w.NewSessionAsync(c.S.ClassAId, c.S.SubjectId, c.S.Absent.PersonId, c.Date, 3, "Cancelled");
            var r1 = await c.Teacher.PutAsync($"/api/att/sessions/{pending}", Items(c.Students, "Present"));
            Assert.Equal(400, r1.Status);
            Assert.StartsWith("Tiết đang Chờ xử lý", r1.Message);
            var r2 = await c.Teacher.PutAsync($"/api/att/sessions/{cancelled}", Items(c.Students, "Present"));
            Assert.Equal(400, r2.Status);
            Assert.Equal("Tiết đã hủy nên không điểm danh.", r2.Message);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ATT_06_Save_validation()
    {
        var c = await ArrangeAsync();
        try
        {
            var url = $"/api/att/sessions/{c.Session}";
            var empty = await c.Teacher.PutAsync(url, new { items = Array.Empty<object>() });
            Assert.Equal(400, empty.Status);
            Assert.StartsWith("Chưa có học sinh nào được điểm danh", empty.Message);
            var dup = await c.Teacher.PutAsync(url, new { items = new object[] { new { studentId = c.Students[0].PersonId, status = "Present" }, new { studentId = c.Students[0].PersonId, status = "Late" } } });
            Assert.Equal(400, dup.Status);
            Assert.StartsWith("Một học sinh xuất hiện hai lần", dup.Message);
            var stranger = await w.NewAccountAsync("HS lạ " + World.Uniq(), "Student", "Student", "Active");
            var notIn = await c.Teacher.PutAsync(url, new { items = new object[] { new { studentId = stranger.PersonId, status = "Present" } } });
            Assert.Equal(400, notIn.Status);
            Assert.StartsWith("Có học sinh không thuộc lớp vào ngày của tiết", notIn.Message);
            var status = await c.Teacher.PutAsync(url, new { items = new object[] { new { studentId = c.Students[0].PersonId, status = "ExcusedAbsent" } } });
            Assert.Equal(400, status.Status);
            Assert.Equal("Trạng thái điểm danh chỉ nhận Có mặt, Muộn hoặc Vắng.", status.Message);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ATT_08_Editing_updates_the_same_rows_instead_of_duplicating()
    {
        var c = await ArrangeAsync();
        try
        {
            var url = $"/api/att/sessions/{c.Session}";
            await c.Teacher.PutAsync(url, Items(c.Students, "Present"));
            var second = await c.Teacher.PutAsync(url, Items(c.Students, "Absent"));
            Assert.Equal(200, second.Status);
            Assert.Equal(3, await f.WithDbAsync(db => db.AttendanceRecord.CountAsync(a => a.SessionId == c.Session)));
            Assert.All(second.Body!["students"]!.AsArray(), r => Assert.Equal("Absent", r!["status"]!.GetValue<string>()));
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ATT_15_And_16_Class_summary_scope_and_missing_filter()
    {
        var past = w.NextMonday().AddDays(-14);
        var c = await ArrangeAsync(date: past);
        try
        {
            f.Clock.Set(At(past, 7, 0));
            await c.Teacher.PutAsync($"/api/att/sessions/{c.Session}", Items(c.Students, "Present"));
            var unmarked = await w.NewSessionAsync(c.S.ClassAId, c.S.SubjectId, c.S.Absent.PersonId, past, 2);
            f.Clock.Set(At(past.AddDays(5), 9, 0));
            var from = World.Iso(past); var to = World.Iso(past);
            foreach (var user in new[] { w.SchoolAdmin.Username, w.Principal.Username, w.ViceA.Username })
                Assert.Equal(200, (await (await f.LoginAsync(user)).GetAsync($"/api/att/class-summary?classId={c.S.ClassAId}&from={from}&to={to}")).Status);
            var viceB = await f.LoginAsync(w.ViceB.Username);
            var denied = await viceB.GetAsync($"/api/att/class-summary?classId={c.S.ClassAId}&from={from}&to={to}");
            Assert.Equal(403, denied.Status);
            Assert.StartsWith("Không có quyền xem chuyên cần của lớp này.", denied.Message);
            var other = await f.LoginAsync(c.S.Sub.Username);
            Assert.Equal(403, (await other.GetAsync($"/api/att/class-summary?classId={c.S.ClassAId}&from={from}&to={to}")).Status);
            // ATT-16: chỉ tiết chưa có bản ghi nào
            var missing = await (await f.LoginAsync(w.ViceA.Username)).GetAsync($"/api/att/sessions?classId={c.S.ClassAId}&from={from}&to={to}&missing=true");
            Assert.Equal(200, missing.Status);
            var ids = missing.Body!.AsArray().Select(x => x!["sessionId"]!.GetValue<long>()).ToList();
            Assert.Contains(unmarked, ids);
            Assert.DoesNotContain(c.Session, ids);
            Assert.Equal(403, (await c.Teacher.GetAsync($"/api/att/sessions?classId={c.S.ClassAId}&from={from}&to={to}")).Status);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ATT_17_Student_sees_own_attendance_and_parent_only_the_linked_child()
    {
        var past = w.NextMonday().AddDays(-14);
        var c = await ArrangeAsync(date: past);
        try
        {
            await c.Teacher.PutAsync($"/api/att/sessions/{c.Session}", Items(c.Students, "Absent"));
            f.Clock.Set(At(past.AddDays(2), 9, 0));
            var from = World.Iso(past); var to = World.Iso(past);
            var student = await f.LoginAsync(c.Students[0].Username);
            var mine = await student.GetAsync($"/api/att/me?from={from}&to={to}");
            Assert.Equal(200, mine.Status);
            Assert.Contains("UnexcusedAbsent", mine.Raw);
            // phụ huynh dữ liệu mẫu (Vũ Thị Lan) chỉ xem con của mình
            var parent = await f.LoginAsPersonAsync("Vũ Thị Lan");
            var bad = await parent.GetAsync($"/api/att/child?studentId={c.Students[0].PersonId}&from={from}&to={to}");
            Assert.Equal(403, bad.Status);
            Assert.Equal("Học sinh này không được liên kết với tài khoản phụ huynh này.", bad.Message);
            Assert.Equal(403, (await student.GetAsync($"/api/att/child?studentId={c.Students[0].PersonId}&from={from}&to={to}")).Status);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ATT_18_Date_range_limits()
    {
        await w.EnsureAsync();
        var student = await f.LoginAsPersonAsync("Hoàng Gia Bảo");
        var bad = await student.GetAsync("/api/att/me?from=2026-10-10&to=2026-10-01");
        Assert.Equal(400, bad.Status);
        Assert.Equal("Ngày kết thúc phải sau ngày bắt đầu.", bad.Message);
        Assert.Equal(200, (await student.GetAsync("/api/att/me?from=2026-09-05&to=2026-11-06")).Status);   // 62 ngày
        var tooLong = await student.GetAsync("/api/att/me?from=2026-09-05&to=2026-11-07");               // 63 ngày
        Assert.Equal(400, tooLong.Status);
        Assert.Equal("Chỉ xem tối đa 62 ngày mỗi lần. Chọn khoảng ngắn hơn.", tooLong.Message);
    }

    [Fact]
    public async Task ATT_19_Teacher_today_list_has_own_scheduled_sessions_only()
    {
        var c = await ArrangeAsync();
        try
        {
            var pending = await w.NewSessionAsync(c.S.ClassAId, c.S.SubjectId, null, c.Date, 2, "Pending");
            var cancelled = await w.NewSessionAsync(c.S.ClassAId, c.S.SubjectId, c.S.Absent.PersonId, c.Date, 3, "Cancelled");
            var sub = await w.NewSessionAsync(c.S.ClassAId, c.S.SubjectId, c.S.Sub.PersonId, c.Date, 4);   // tiết của giáo viên khác
            var today = await c.Teacher.GetAsync("/api/att/today");
            Assert.Equal(200, today.Status);
            var ids = today.Body!.AsArray().Select(x => x!["sessionId"]!.GetValue<long>()).ToList();
            Assert.Contains(c.Session, ids);
            Assert.DoesNotContain(pending, ids);
            Assert.DoesNotContain("Pending", today.Body.AsArray().Select(x => x!["status"]!.GetValue<string>()));
            var row = today.Body.AsArray().Single(x => x!["sessionId"]!.GetValue<long>() == cancelled)!;   // tiết đã hủy vẫn hiện nhưng không ghi được
            Assert.Equal("Cancelled", row["status"]!.GetValue<string>());
            Assert.False(row["canWrite"]!.GetValue<bool>());
            Assert.DoesNotContain(sub, ids);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ATT_20_And_21_The_roster_follows_the_enrollment_dates()
    {
        var c = await ArrangeAsync(students: 1);
        try
        {
            var joinsLate = (await w.NewStudentAsync(c.S.ClassAId, "HS vào muộn " + World.Uniq(), c.Date.AddDays(1))).Who;
            var left = (await w.NewStudentAsync(c.S.ClassAId, "HS đã rời " + World.Uniq(), null, c.Date.AddDays(-1))).Who;
            var sheet = await c.Teacher.GetAsync($"/api/att/sessions/{c.Session}");
            var ids = sheet.Body!["students"]!.AsArray().Select(x => x!["studentId"]!.GetValue<int>()).ToList();
            Assert.Contains(c.Students[0].PersonId, ids);
            Assert.DoesNotContain(joinsLate.PersonId, ids);   // ATT-20: chưa tới ngày vào lớp
            Assert.DoesNotContain(left.PersonId, ids);        // ATT-21: đã rời lớp trước ngày của tiết
            var save = await c.Teacher.PutAsync($"/api/att/sessions/{c.Session}", Items([joinsLate], "Present"));
            Assert.Equal(400, save.Status);
            Assert.StartsWith("Có học sinh không thuộc lớp vào ngày của tiết", save.Message);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ATT_22_Teacher_can_take_attendance_only_from_10_minutes_before_the_session_starts()
    {
        var c = await ArrangeAsync();
        try
        {
            var url = $"/api/att/sessions/{c.Session}";
            f.Clock.Set(At(c.Date, 6, 49));
            var sheet = await c.Teacher.GetAsync(url);
            Assert.Equal(200, sheet.Status);
            Assert.Equal("Tiết chưa tới giờ. Điểm danh được từ 10 phút trước giờ bắt đầu tiết (06:50).", sheet.Body!["readOnlyReason"]!.GetValue<string>());
            var early = await c.Teacher.PutAsync(url, Items(c.Students, "Present"));
            Assert.Equal(400, early.Status);
            Assert.Equal("Tiết chưa tới giờ. Điểm danh được từ 10 phút trước giờ bắt đầu tiết (06:50).", early.Message);
            var list = await c.Teacher.GetAsync("/api/att/today");
            Assert.False(list.Body!.AsArray().Single(x => x!["sessionId"]!.GetValue<long>() == c.Session)!["canWrite"]!.GetValue<bool>());
            f.Clock.Set(At(c.Date, 6, 50));
            Assert.Equal(200, (await c.Teacher.PutAsync(url, Items(c.Students, "Present"))).Status);
            f.Clock.Set(At(c.Date, 23, 59));
            Assert.Equal(200, (await c.Teacher.PutAsync(url, Items(c.Students, "Late"))).Status);
            f.Clock.Set(At(c.Date.AddDays(1), 0, 0));
            var late = await c.Teacher.PutAsync(url, Items(c.Students, "Present"));
            Assert.Equal(400, late.Status);
            Assert.Equal("Tiết đã qua ngày, giáo viên không sửa được nữa. Liên hệ quản trị hệ thống nếu cần sửa.", late.Message);
            // Quản trị hệ thống không bị giới hạn 10 phút
            f.Clock.Set(At(c.Date, 5, 0));
            var admin = await f.LoginAsync(w.SchoolAdmin.Username);
            Assert.Equal(200, (await admin.PutAsync(url, Items(c.Students, "Present"))).Status);
        }
        finally { f.Clock.Reset(); }
    }
}
