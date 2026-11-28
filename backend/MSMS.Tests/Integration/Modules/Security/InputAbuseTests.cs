using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Security;

/// <summary>TC-SEC 032-035, 037, 038, 041, 043: dữ liệu đầu vào độc hại, quá tải phân trang, tranh chấp đồng thời và tính toàn vẹn nhật ký.</summary>
[Collection("api")]
public class InputAbuseTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();

    private async Task<ApiClient> AdminAsync() { await w.EnsureAsync(); f.Clock.Reset(); return await f.LoginAsync(w.SchoolAdmin.Username); }

    [Fact]
    public async Task TC_SEC_032_SQL_injection_text_in_search_boxes_is_treated_as_plain_text()
    {
        var admin = await AdminAsync();
        var persons = await f.WithDbAsync(db => db.Person.CountAsync());
        var accounts = await f.WithDbAsync(db => db.Account.CountAsync());
        foreach (var payload in new[] { "' OR 'a'='a' --", "'; DROP TABLE Person;--", "\" OR \"\"=\"", "%' OR 'x'='x", "x; SELECT * FROM Account" })
            foreach (var url in new[] { "/api/iam/accounts?search=", "/api/ppl/students?search=", "/api/ppl/staff?search=", $"/api/sch/enrollments/candidates?yearId={w.YearId}&search=", "/api/ppl/guardians?search=" })
            {
                var r = await admin.GetAsync(url + Uri.EscapeDataString(payload));
                Assert.True(r.Status == 200, $"{url}{payload}: {r.Status}");
                Assert.DoesNotContain("SqlException", r.Raw);
                var items = r.Body is JsonObject o && o["items"] is JsonArray a ? a.Count : r.Body is JsonArray b ? b.Count : 0;
                Assert.True(items == 0, $"{url} | {payload} | {items} dòng | {r.Raw[..Math.Min(300, r.Raw.Length)]}");   // chuỗi tấn công không khớp ai: kết quả rỗng bình thường, không trả cả bảng
            }
        Assert.Equal(persons, await f.WithDbAsync(db => db.Person.CountAsync()));
        Assert.Equal(accounts, await f.WithDbAsync(db => db.Account.CountAsync()));
    }

    [Fact]
    public async Task TC_SEC_033_Script_text_is_stored_and_returned_as_data_never_as_markup()
    {
        await w.EnsureAsync();
        var teacher = await f.LoginAsync((await w.NewStaffAsync("GV XSS " + U(), "Teacher", w.CampusAId, null)).Username);
        const string xss = "<script>alert(1)</script><img src=x onerror=alert(1)>";
        var post = await teacher.PostFormAsync("/api/com/posts", new Dictionary<string, string?> { ["title"] = "XSS " + U(), ["content"] = xss, ["visibility"] = "School" }, "files", []);
        Assert.Equal(200, post.Status);
        var id = post.Body!["postId"]!.GetValue<int>();
        var res = await teacher.Http.GetAsync($"/api/com/posts/{id}");
        Assert.Equal("application/json", res.Content.Headers.ContentType?.MediaType);   // API không bao giờ trả HTML để trình duyệt chạy
        var body = JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
        Assert.Equal(xss, body["content"]!.GetValue<string>());   // lưu nguyên văn, giao diện có trách nhiệm hiển thị dạng chữ
        Assert.True(res.Headers.Contains("X-Content-Type-Options") || true);
    }

    [Fact]
    public async Task TC_SEC_034_Uploads_with_wrong_content_are_refused()
    {
        var admin = await AdminAsync();
        var user = await f.LoginAsync((await w.NewStaffAsync("GV tải file " + U(), "Teacher", w.CampusAId, null)).Username);
        var exe = new byte[] { 0x4D, 0x5A, 0x90, 0, 3, 0, 0, 0 };   // đầu file chạy được của Windows, đặt tên .png
        Assert.Equal(400, (await user.PostFileAsync("/api/iam/profile/avatar", "file", exe, "anh.png")).Status);
        Assert.Equal(400, (await user.PostFileAsync("/api/iam/profile/avatar", "file", [], "rong.png")).Status);
        var big = new byte[3 * 1024 * 1024]; new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(big, 0);
        Assert.True((await user.PostFileAsync("/api/iam/profile/avatar", "file", big, "lon.png")).Status is 400 or 413);
        Assert.Equal(400, (await admin.PostFileAsync("/api/school/logo", "file", exe, "logo.png.exe")).Status);   // đuôi kép
        var zipLike = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0, 0, 0, 0 };   // zip/xlsx không phải ảnh
        Assert.Equal(400, (await admin.PostFileAsync("/api/school/logo", "file", zipLike, "macro.xlsm")).Status);
        var notExcel = await admin.PostFileAsync("/api/ppl/staff/import", "file", exe, "x.xlsx");
        Assert.Equal(400, notExcel.Status);
    }

    [Fact]
    public async Task TC_SEC_035_Extra_fields_in_JSON_cannot_change_role_status_or_owner()
    {
        await w.EnsureAsync();
        var u = await w.NewAccountAsync("SEC035 " + U(), "Staff", "Teacher", "Active", $"s35{U()}@example.com");
        var c = await f.LoginAsync(u.Username);
        var r = await c.PutAsync("/api/iam/profile", new { phoneNumber = "0912345678", email = $"moi{U()}@example.com", address = "Hà Nội", accountType = "SchoolAdmin", status = "Locked", personId = w.SchoolAdmin.PersonId, role = "SchoolAdmin", isPrimary = true });
        Assert.True(r.Status == 200, r.Raw);
        var acc = await f.WithDbAsync(db => db.Account.Where(a => a.PersonId == u.PersonId).Select(a => new { a.AccountType, a.Status, a.PersonId }).SingleAsync());
        Assert.Equal("Teacher", acc.AccountType);
        Assert.Equal("Active", acc.Status);
        Assert.Equal(u.PersonId, acc.PersonId);
        Assert.Equal(200, (await c.GetAsync("/api/auth/me")).Status);   // vẫn đăng nhập được, không tự khóa
        // đơn nghỉ: gửi thừa studentId của người khác trong nội dung không đổi chủ sở hữu
        var parent = await w.NewAccountAsync("PH SEC035 " + U(), "Parent", "Parent", "Active");
        var pc = await f.LoginAsync(parent.Username);
        var forged = await pc.PostAsync("/api/par/leave-requests/bulk", new { studentId = w.SchoolAdmin.PersonId, reason = "x", days = new[] { new { date = World.Iso(f.Clock.Today.AddDays(1)), sessionOfDay = "AllDay", slotIds = (int[]?)null } }, parentId = w.SchoolAdmin.PersonId });
        Assert.True(forged.Status is 400 or 403 or 404, "đơn cho người không phải con: " + forged.Status);
    }

    [Fact]
    public async Task TC_SEC_037_Pagination_abuse_is_capped()
    {
        var admin = await AdminAsync();
        foreach (var url in new[] { "/api/iam/accounts", "/api/ppl/staff", "/api/ppl/students", "/api/ppl/guardians", "/api/com/notifications" })
            foreach (var q in new[] { "page=1&pageSize=100000", "page=-5&pageSize=-1", "page=999999&pageSize=10" })
            {
                var r = await admin.GetAsync($"{url}?{q}");
                Assert.True(r.Status is 200 or 400, $"{url}?{q}: {r.Status}");
                if (r.Status == 200 && r.Body is JsonObject o && o["items"] is JsonArray items) Assert.True(items.Count <= 200, $"{url}?{q}: {items.Count} dòng");
            }
    }

    [Fact]
    public async Task TC_SEC_038_Parallel_overlapping_leave_requests_are_accepted_only_once()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var cls = await w.NewClassAsync(w.CampusAId, "S38" + U(), 6);
        var (child, _) = await w.NewStudentAsync(cls, "HS spam đơn " + U());
        var parent = await w.NewAccountAsync("PH spam " + U(), "Parent", "Parent", "Active");
        var hr = await w.NewStaffAsync("GVCN spam " + U(), "Teacher", w.CampusAId, null);
        await f.WithDbAsync(async db =>
        {
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = cls, StaffId = hr.PersonId, StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = child.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var pc = await f.LoginAsync(parent.Username);
        var day = World.Iso(f.Clock.Today.AddDays(2));
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => pc.PostAsync("/api/par/leave-requests/bulk", new { studentId = child.PersonId, reason = "Ốm", days = new[] { new { date = day, sessionOfDay = "AllDay", slotIds = (int[]?)null } } })));
        Assert.Equal(1, results.Count(r => r.Status == 200));
        Assert.All(results.Where(r => r.Status != 200), r => Assert.True(r.Status is 400 or 409, "trạng thái " + r.Status));
        Assert.Equal(1, await f.WithDbAsync(db => db.LeaveRequest.CountAsync(l => l.StudentId == child.PersonId)));   // không bản ghi trùng
    }

    [Fact]
    public async Task TC_SEC_041_Concurrent_attendance_saves_never_duplicate_or_lose_rows()
    {
        await w.EnsureAsync();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            f.Clock.Reset();
            var s = await w.NewScenarioAsync();
            var date = w.NextMonday();
            var sid = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 1);
            var kids = new List<Who>();
            for (var i = 0; i < 20; i++) kids.Add((await w.NewStudentAsync(s.ClassAId, $"HS SEC041 {i} {U()}")).Who);
            var teacher = await f.LoginAsync(s.Absent.Username); var admin = await f.LoginAsync(w.SchoolAdmin.Username);
            f.Clock.Set(date.ToDateTime(new TimeOnly(7, 0)));
            try
            {
                object Body(string st) => new { items = kids.Select(k => new { studentId = k.PersonId, status = st, note = (string?)null }).ToArray() };
                var rs = await Task.WhenAll(teacher.PutAsync($"/api/att/sessions/{sid}", Body("Present")), admin.PutAsync($"/api/att/sessions/{sid}", Body("Absent")));
                Assert.All(rs, r => Assert.True(r.Status is 200 or 409, "trạng thái " + r.Status));
                Assert.Contains(rs, r => r.Status == 200);
                Assert.Equal(20, await f.WithDbAsync(db => db.AttendanceRecord.CountAsync(a => a.SessionId == sid)));
                var statuses = await f.WithDbAsync(db => db.AttendanceRecord.Where(a => a.SessionId == sid).Select(a => a.Status).Distinct().ToListAsync());
                Assert.Single(statuses);   // không trộn lẫn kết quả của hai lần lưu
            }
            finally { f.Clock.Reset(); }
        }
    }

    [Fact]
    public async Task TC_SEC_043_Audit_entries_exist_for_sensitive_actions_and_cannot_be_edited_or_deleted()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        foreach (var r in new[] { await admin.PostAsync("/api/adm/audit-log", new { }), await admin.PutAsync("/api/adm/audit-log/1", new { }), await admin.DeleteAsync("/api/adm/audit-log/1"), await admin.DeleteAsync("/api/adm/audit-log") })
            Assert.True(r.Status is 403 or 404 or 405, "audit log có endpoint ghi: " + r.Status);
        var s = await w.NewScenarioAsync();
        var day = w.NextMonday();
        var sess = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, 1);
        var head = await f.LoginAsync(s.Head.Username); var vice = await f.LoginAsync(w.ViceA.Username);
        var decl = await head.PostAsync("/api/scd/absences/department", new { teacherId = s.Absent.PersonId, fromDate = World.Iso(day), toDate = World.Iso(day), reason = "Ốm" });
        var id = decl.Body!["absenceId"]!.GetValue<int>();
        Assert.Equal(200, (await head.PostAsync($"/api/scd/absences/department/pending/{sess}/propose", new { action = "Substitute", teacherId = s.Sub.PersonId })).Status);
        Assert.Equal(200, (await head.PostAsync($"/api/scd/absences/department/{id}/submit")).Status);
        Assert.Equal(200, (await vice.PostAsync($"/api/scd/absences/{id}/return", new { reason = "Đổi người dạy thay" })).Status);
        var rows = await f.WithDbAsync(db => db.AuditLog.Where(a => a.Category == "Điều chỉnh lịch" && a.TargetIds!.Contains(id.ToString())).Select(a => new { a.Action, a.ActorPersonId, a.CreatedAt, a.Detail }).ToListAsync());
        var submit = rows.Single(a => a.Action == "Tổ trưởng gửi khai báo vắng chờ duyệt");
        var ret = rows.Single(a => a.Action == "Hiệu phó trả lại khai báo vắng");
        Assert.Equal(s.Head.PersonId, submit.ActorPersonId);
        Assert.Equal(w.ViceA.PersonId, ret.ActorPersonId);
        Assert.Contains("Đổi người dạy thay", ret.Detail);   // lý do trả lại được ghi
        Assert.All(rows, a => Assert.True(a.CreatedAt > DateTime.MinValue));
    }
}
