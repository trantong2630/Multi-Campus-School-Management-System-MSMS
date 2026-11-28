using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;
using static MSMS.Tests.Integration.Infrastructure.Roles;

namespace MSMS.Tests.Integration.Modules.Security;

/// <summary>TC-SEC 008-010, 023-031, 042, 046-049: phiên, lộ tài khoản, cô lập dữ liệu giữa phân hiệu, gia đình, lớp, giáo viên và quyền hạn theo trách nhiệm hiện tại.</summary>
[Collection("api")]
public class IsolationTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();
    private const string Pw = ApiFactory.Password;

    private static void Refused(ApiResult r, string what) => Assert.True(r.Status is 403 or 404, $"{what}: {r.Status} {r.Message}");
    private string Range => $"from={World.Iso(f.Clock.Today)}&to={World.Iso(f.Clock.Today.AddDays(7))}";

    [Fact]
    public async Task TC_SEC_008_A_reused_refresh_token_kills_the_whole_session()
    {
        await w.EnsureAsync();
        var u = await w.NewAccountAsync("SEC008 " + U(), "Staff", "Teacher", "Active");
        var login = await f.Anonymous().PostAsync("/api/auth/login", new { username = u.Username, password = Pw, clientType = "mobile" });
        var rt1 = login.Body!["refreshToken"]!.GetValue<string>();
        var r1 = await f.Anonymous().PostAsync("/api/auth/refresh", new { refreshToken = rt1 });
        Assert.Equal(200, r1.Status);
        var rt2 = r1.Body!["refreshToken"]!.GetValue<string>();
        Assert.Equal(401, (await f.Anonymous().PostAsync("/api/auth/refresh", new { refreshToken = rt1 })).Status);   // dùng lại token cũ
        Assert.Equal(401, (await f.Anonymous().PostAsync("/api/auth/refresh", new { refreshToken = rt2 })).Status);   // token mới cũng chết theo
    }

    [Fact]
    public async Task TC_SEC_009_Login_and_OTP_answers_do_not_reveal_whether_an_account_exists()
    {
        await w.EnsureAsync();
        var real = await w.NewAccountAsync("SEC009 " + U(), "Staff", "Teacher", "Active", $"s9{U()}@example.com");
        var a = f.Anonymous();
        var wrongPw = await a.PostAsync("/api/auth/login", new { username = real.Username, password = "Sai-mat-khau-1" });
        var unknown = await a.PostAsync("/api/auth/login", new { username = "khong.ton.tai." + U(), password = "Sai-mat-khau-1" });
        Assert.Equal(wrongPw.Status, unknown.Status);
        Assert.Equal(wrongPw.Message, unknown.Message);
        Assert.Equal(wrongPw.Code, unknown.Code);
        var locked = await w.NewAccountAsync("SEC009 khóa " + U(), "Staff", "Teacher", "Locked", $"s9l{U()}@example.com");
        var lockedLogin = await a.PostAsync("/api/auth/login", new { username = locked.Username, password = Pw });
        Assert.Equal(wrongPw.Message, lockedLogin.Message);   // tài khoản khóa cũng cùng câu trả lời
        var known = await a.PostAsync("/api/auth/forgot-password", new { username = real.Username });
        var none = await a.PostAsync("/api/auth/forgot-password", new { username = "khong.ton.tai." + U() });
        var lockedOtp = await a.PostAsync("/api/auth/forgot-password", new { username = locked.Username });
        Assert.Equal(known.Status, none.Status); Assert.Equal(known.Message, none.Message);
        Assert.Equal(known.Status, lockedOtp.Status); Assert.Equal(known.Message, lockedOtp.Message);
    }

    [Fact]
    public async Task TC_SEC_010_OTP_brute_force_is_stopped_after_five_wrong_codes_and_resend_is_throttled()
    {
        await w.EnsureAsync();
        var email = $"s10{U()}@example.com";
        var u = await w.NewAccountAsync("SEC010 " + U(), "Staff", "Teacher", "Active", email);
        var a = f.Anonymous();
        await a.PostAsync("/api/auth/forgot-password", new { username = u.Username });
        var good = f.Otp.LastCodeTo(email)!;
        var wrong = good == "000000" ? "111111" : "000000";
        for (var i = 0; i < 6; i++) Assert.Equal(400, (await a.PostAsync("/api/auth/verify-reset-code", new { username = u.Username, code = wrong })).Status);
        Assert.Equal(400, (await a.PostAsync("/api/auth/verify-reset-code", new { username = u.Username, code = good })).Status);   // đúng mã nhưng đã bị vô hiệu
        var before = f.Otp.CountTo(email);
        await a.PostAsync("/api/auth/forgot-password", new { username = u.Username });
        Assert.Equal(before, f.Otp.CountTo(email));   // gửi lại trong 60 giây bị chặn
    }

    [Fact]
    public async Task TC_SEC_023_The_vice_principal_of_campus_A_cannot_reach_campus_B()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var (pupil, _) = await w.NewStudentAsync(s.ClassBId, "HS phân hiệu B " + U());
        var vice = await f.LoginAsync(w.ViceA.Username);
        var day = World.Iso(f.Clock.Today);
        Refused(await vice.GetAsync($"/api/adm/dashboard?campusId={w.CampusBId}"), "dashboard B");
        Refused(await vice.GetAsync($"/api/att/class-summary?classId={s.ClassBId}&from={day}&to={day}"), "chuyên cần lớp B");
        Refused(await vice.GetAsync($"/api/sch/classes/{s.ClassBId}/roster"), "danh sách lớp B");
        Refused(await vice.GetAsync($"/api/ppl/responsibilities/history?kind=homeroom&targetId={s.ClassBId}"), "lịch sử trách nhiệm lớp B");
        Refused(await vice.GetAsync($"/api/ppl/students/{pupil.PersonId}"), "hồ sơ học sinh B");
        var camps = await vice.GetAsync("/api/scd/timetable/campuses");
        Assert.DoesNotContain($"\"campusId\":{w.CampusBId},", camps.Raw);
    }

    [Fact]
    public async Task TC_SEC_024_A_parent_cannot_read_the_child_of_another_family()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var cls = await w.NewClassAsync(w.CampusAId, "S24" + U(), 6);
        var (child, _) = await w.NewStudentAsync(cls, "HS gia đình khác " + U());
        var p1 = await w.NewAccountAsync("PH 1 " + U(), "Parent", "Parent", "Active");
        var p2 = await w.NewAccountAsync("PH 2 " + U(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db => { db.ParentStudentLink.Add(new ParentStudentLink { ParentId = p1.PersonId, StudentId = child.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
        var c2 = await f.LoginAsync(p2.Username);
        var id = child.PersonId;
        foreach (var url in new[]
        {
            $"/api/att/child?studentId={id}&{Range}", $"/api/scd/schedule/child?studentId={id}&{Range}", $"/api/par/leave-requests/student/{id}",
            $"/api/lrn/results/children/{id}", $"/api/par/weekly-reports/student/{id}", $"/api/par/conversations/available-teachers?studentId={id}",
        })
            Assert.Equal(403, (await c2.GetAsync(url)).Status);
        Assert.Equal(200, (await (await f.LoginAsync(p1.Username)).GetAsync($"/api/par/leave-requests/student/{id}")).Status);   // phụ huynh đúng liên kết vẫn xem được
    }

    [Fact]
    public async Task TC_SEC_026_A_teacher_outside_a_class_cannot_read_it()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var (pupil, _) = await w.NewStudentAsync(s.ClassAId, "HS lớp lạ " + U());
        var sid = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, f.Clock.Today, 1);
        var outsider = await f.LoginAsync((await w.NewStaffAsync("GV ngoài lớp " + U(), "Teacher", w.CampusAId, null)).Username);
        var roster = await outsider.GetAsync($"/api/sch/classes/{s.ClassAId}/roster");
        Assert.Equal(403, roster.Status);
        Assert.Equal("Không có quyền xem danh sách học sinh của lớp này.", roster.Message);
        Refused(await outsider.GetAsync($"/api/att/sessions/{sid}"), "bảng điểm danh");
        Refused(await outsider.GetAsync($"/api/att/class-summary?classId={s.ClassAId}&from={World.Iso(f.Clock.Today)}&to={World.Iso(f.Clock.Today)}"), "tổng hợp lớp");
        Refused(await outsider.GetAsync($"/api/par/weekly-reports/student/{pupil.PersonId}"), "báo cáo tuần");
    }

    [Fact]
    public async Task TC_SEC_028_And_029_And_030_Ownership_of_questions_notifications_and_conversations()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        // câu hỏi
        var owner = await f.LoginAsync(s.Absent.Username); var colleague = await f.LoginAsync(s.Sub.Username);
        var q = await owner.PostAsync("/api/lrn/questions", new { subjectId = s.SubjectId, gradeLevel = 6, topic = "Chủ đề", content = "Câu " + U(), explanation = "x", options = new[] { new { label = "A", content = "a", isCorrect = true }, new { label = "B", content = "b", isCorrect = false }, new { label = "C", content = "c", isCorrect = false }, new { label = "D", content = "d", isCorrect = false } }, difficulty = "Recognize" });
        Assert.Equal(200, q.Status);
        var qid = q.Body!["questionId"]!.GetValue<int>();
        var edit = await colleague.PutAsync($"/api/lrn/questions/{qid}", new { subjectId = s.SubjectId, gradeLevel = 6, topic = "x", content = "y", explanation = "x", options = new[] { new { label = "A", content = "a", isCorrect = true }, new { label = "B", content = "b", isCorrect = false }, new { label = "C", content = "c", isCorrect = false }, new { label = "D", content = "d", isCorrect = false } }, difficulty = "Recognize" });
        Assert.Equal(403, edit.Status); Assert.StartsWith("Chỉ người tạo câu hỏi mới", edit.Message);
        Assert.Equal(403, (await colleague.PutAsync($"/api/lrn/questions/{qid}/hide")).Status);
        Assert.Equal(403, (await colleague.PutAsync($"/api/lrn/questions/{qid}/unhide")).Status);
        // thông báo
        var nid = await f.WithDbAsync(async db => { var n = new Notification { RecipientPersonId = s.Absent.PersonId, NotificationType = "ScheduleChanged", Title = "t", Body = "b", CreatedAt = f.Clock.Now }; db.Notification.Add(n); await db.SaveChangesAsync(); return n.NotificationId; });
        Assert.Equal(404, (await colleague.PostAsync($"/api/com/notifications/{nid}/open")).Status);
        Assert.Equal(404, (await colleague.PutAsync($"/api/com/notifications/{nid}/read")).Status);
        // hội thoại
        var cls = await w.NewClassAsync(w.CampusAId, "S30" + U(), 6);
        var (child, _) = await w.NewStudentAsync(cls, "HS hội thoại " + U());
        var parent = await w.NewAccountAsync("PH hội thoại " + U(), "Parent", "Parent", "Active");
        var hr = await w.NewStaffAsync("GVCN hội thoại " + U(), "Teacher", w.CampusAId, null);
        await f.WithDbAsync(async db =>
        {
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = cls, StaffId = hr.PersonId, StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = child.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var conv = await (await f.LoginAsync(parent.Username)).PostAsync("/api/par/conversations", new { studentId = child.PersonId, staffId = hr.PersonId });
        var cid = conv.Body!.GetValue<int>();
        var outsider = await f.LoginAsync((await w.NewAccountAsync("PH ngoài " + U(), "Parent", "Parent", "Active")).Username);
        var get = await outsider.GetAsync($"/api/par/conversations/{cid}/messages");
        Assert.Equal(403, get.Status); Assert.Equal("Bạn không phải thành viên của cuộc hội thoại này.", get.Message);
        Assert.Equal(403, (await outsider.PostAsync($"/api/par/conversations/{cid}/messages", new { content = "chen ngang" })).Status);
        Assert.Equal(403, (await colleague.GetAsync($"/api/par/conversations/{cid}/messages")).Status);
    }

    [Fact]
    public async Task TC_SEC_031_Images_of_internal_posts_do_not_leak_and_avatars_are_the_only_public_images()
    {
        await w.EnsureAsync();
        var author = await f.LoginAsync((await w.NewStaffAsync("GV bài ảnh " + U(), "Teacher", w.CampusAId, null)).Username);
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        var post = await author.PostFormAsync("/api/com/posts", new Dictionary<string, string?> { ["title"] = "Nội bộ " + U(), ["content"] = "x", ["visibility"] = "Internal" }, "files", [("a.png", png, "image/png")]);
        Assert.Equal(200, post.Status);
        var pid = post.Body!["postId"]!.GetValue<int>();
        var imgId = post.Body["images"]![0]!["postImageId"]!.GetValue<int>();
        var student = await f.LoginAsPersonAsync("Hoàng Gia Bảo");
        Assert.Equal(404, (await student.GetAsync($"/api/com/posts/{pid}/images/{imgId}")).Status);
        Assert.Equal(404, (await student.GetAsync($"/api/com/posts/{pid}/images/{imgId + 999}")).Status);   // đoán id
        Assert.Equal(401, (await f.Anonymous().GetAsync($"/api/com/posts/{pid}/images/{imgId}")).Status);
        Assert.True((await student.GetAsync("/api/iam/profile/avatar/987654321")).Status is 404);
    }

    [Fact]
    public async Task TC_SEC_042_A_homeroom_teacher_loses_the_homeroom_endpoints_the_moment_the_duty_ends()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var principal = await f.LoginAsync(w.Principal.Username);
        var cls = await w.NewClassAsync(w.CampusAId, "S42" + U(), 6);
        var t = await w.NewStaffAsync("GVCN SEC042 " + U(), "Teacher", w.CampusAId, null);
        Assert.Equal(204, (await principal.PostAsync("/api/ppl/responsibilities/assign", new { kind = "homeroom", targetId = cls, staffId = t.PersonId, startDate = World.Iso(f.Clock.Today) })).Status);
        var tc = await f.LoginAsync(t.Username);
        Assert.Equal(200, (await tc.GetAsync($"/api/par/leave-requests/homeroom?classId={cls}")).Status);
        Assert.Equal(200, (await tc.GetAsync("/api/adm/homeroom")).Status);
        var rec = await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.ClassId == cls).Select(h => h.Id).SingleAsync());
        Assert.Equal(204, (await principal.PostAsync("/api/ppl/responsibilities/end", new { kind = "homeroom", recordId = rec, endDate = World.Iso(f.Clock.Today) })).Status);
        Assert.Equal(403, (await tc.GetAsync($"/api/par/leave-requests/homeroom?classId={cls}")).Status);
        Assert.Equal(403, (await tc.GetAsync("/api/adm/homeroom")).Status);
    }

    [Fact]
    public async Task TC_SEC_046_Exam_paper_and_matrix_endpoints_are_for_teachers_only()
    {
        var roles = await f.LoginAllAsync();
        var calls = new (string Method, string Url, object? Body)[]
        {
            ("POST", "/api/lrn/papers/export", new { subjectId = 1, title = "x", items = Array.Empty<object>(), format = "pdf" }),
            ("GET", "/api/lrn/assignments/1/paper", null), ("GET", "/api/lrn/questions/matrix?subjectId=1&gradeLevel=6", null),
            ("POST", "/api/lrn/questions/pick", new { subjectId = 1, gradeLevel = 6, cells = Array.Empty<object>() }), ("PUT", "/api/lrn/questions/confirm-drafts", new { ids = Array.Empty<int>() }),
        };
        foreach (var role in new[] { Admin, Principal, Vice, Student, Parent })
            foreach (var (m, url, body) in calls)
            {
                var r = m == "GET" ? await roles[role].GetAsync(url) : m == "POST" ? await roles[role].PostAsync(url, body) : await roles[role].PutAsync(url, body);
                Assert.True(r.Status == 403, $"{m} {url} as {role}: {r.Status}");
            }
    }

    [Fact]
    public async Task TC_SEC_047_Staff_and_student_detail_pages_do_not_reveal_records_outside_the_scope()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var staffB = await w.NewStaffAsync("CB phân hiệu B " + U(), "Teacher", w.CampusBId, null);
        var (pupilB, _) = await w.NewStudentAsync(s.ClassBId, "HS phân hiệu B " + U());
        var vice = await f.LoginAsync(w.ViceA.Username);
        var st = await vice.GetAsync($"/api/ppl/staff/{staffB.PersonId}");
        Assert.Equal(404, st.Status); Assert.Equal("Không tìm thấy hồ sơ cán bộ.", st.Message);
        var sd = await vice.GetAsync($"/api/ppl/students/{pupilB.PersonId}");
        Assert.Equal(404, sd.Status); Assert.Equal("Không tìm thấy hồ sơ học sinh.", sd.Message);
        var ghost = await vice.GetAsync("/api/ppl/students/987654321");
        Assert.Equal(sd.Message, ghost.Message);   // câu trả lời giống hệt hồ sơ không tồn tại
        var teacher = await f.LoginAsync(s.Sub.Username);
        var other = await teacher.GetAsync($"/api/ppl/students/{pupilB.PersonId}");
        Assert.Equal(404, other.Status);
    }

    [Fact]
    public async Task TC_SEC_048_Bulletin_posts_School_Admin_Student_and_Parent_cannot_write()
    {
        var roles = await f.LoginAllAsync();
        foreach (var role in new[] { Admin, Student, Parent })
        {
            Assert.Equal(403, (await roles[role].PostFormAsync("/api/com/posts", new Dictionary<string, string?> { ["title"] = "x", ["content"] = "y", ["visibility"] = "School" }, "files", [])).Status);
            Assert.Equal(403, (await roles[role].PutAsync("/api/com/posts/1", new { title = "x", content = "y", visibility = "School" })).Status);
            Assert.Equal(403, (await roles[role].DeleteAsync("/api/com/posts/1")).Status);
            Assert.Equal(403, (await roles[role].PostAsync("/api/com/posts/1/hide", new { reason = "x" })).Status);
            Assert.Equal(403, (await roles[role].PostAsync("/api/com/posts/1/unhide")).Status);
        }
        foreach (var role in new[] { Principal, Vice, Teacher })
            Assert.Equal(200, (await roles[role].PostFormAsync("/api/com/posts", new Dictionary<string, string?> { ["title"] = "Bài " + U(), ["content"] = "y", ["visibility"] = "School" }, "files", [])).Status);
        Assert.Equal(200, (await roles[Admin].GetAsync("/api/com/posts")).Status);   // chỉ đọc
    }

    [Fact]
    public async Task TC_SEC_049_Department_head_endpoints_need_a_current_head_duty()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var other = await w.NewScenarioAsync();
        var plain = await f.LoginAsync((await w.NewStaffAsync("GV không tổ trưởng " + U(), "Teacher", w.CampusAId, s.DepartmentId)).Username);
        var date = World.Iso(w.NextMonday());
        await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, w.NextMonday(), 1);
        const string msg = "Chỉ tổ trưởng đang giữ trách nhiệm mới dùng được chức năng này.";
        foreach (var url in new[] { "/api/scd/absences/department", "/api/scd/absences/department/pending" })
        {
            var r = await plain.GetAsync(url);
            Assert.Equal(403, r.Status); Assert.Equal(msg, r.Message);
        }
        var decl = await plain.PostAsync("/api/scd/absences/department", new { teacherId = s.Absent.PersonId, fromDate = date, toDate = date, reason = "x" });
        Assert.Equal(403, decl.Status); Assert.Equal(msg, decl.Message);
        // tổ trưởng đã hết nhiệm kỳ
        await f.WithDbAsync(async db => { (await db.DepartmentHead.SingleAsync(h => h.StaffId == other.Head.PersonId)).EndDate = f.Clock.Today.AddDays(-1); await db.SaveChangesAsync(); });
        Assert.Equal(403, (await (await f.LoginAsync(other.Head.Username)).GetAsync("/api/scd/absences/department")).Status);
        Assert.Equal(403, (await plain.GetAsync("/api/adm/dashboard/department")).Status);
        // tổ trưởng tổ khác không đụng được giáo viên của tổ này
        var otherHead = await f.LoginAsync((await w.NewScenarioAsync()).Head.Username);
        var cross = await otherHead.PostAsync("/api/scd/absences/department", new { teacherId = s.Absent.PersonId, fromDate = date, toDate = date, reason = "x" });
        Assert.Equal(403, cross.Status);
    }
}
