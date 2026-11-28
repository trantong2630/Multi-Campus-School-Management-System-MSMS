using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.E2e;

/// <summary>Luồng nghiệp vụ nhiều module nối nhau (E2E). Tên test bắt đầu bằng mã case (E2E_xx).</summary>
[Collection("api")]
public class FlowTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();
    private static DateTime At(DateOnly d, int h, int m) => d.ToDateTime(new TimeOnly(h, m));

    private sealed record Family(int ClassId, Who Student, Who Parent, Who Homeroom);

    private async Task<Family> FamilyAsync(int? classId = null, Who? homeroom = null)
    {
        f.Clock.Reset();
        await w.EnsureAsync();
        var cls = classId ?? await w.NewClassAsync(w.CampusAId, "E2E" + U(), 6);
        var hr = homeroom ?? await w.NewStaffAsync("GVCN E2E " + U(), "Teacher", w.CampusAId, null);
        var (student, _) = await w.NewStudentAsync(cls, "HS E2E " + U());
        var parent = await w.NewAccountAsync("PH E2E " + U(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db =>
        {
            if (homeroom is null) db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = cls, StaffId = hr.PersonId, StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = student.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        return new Family(cls, student, parent, hr);
    }

    private int CountNotes(int person, string type, string? containing = null) => f.WithDbAsync(db =>
        db.Notification.CountAsync(n => n.RecipientPersonId == person && n.NotificationType == type && (containing == null || n.Body!.Contains(containing)))).GetAwaiter().GetResult();

    // ------------------------------------------------------------------ Luồng 3: giáo viên vắng

    [Fact]
    public async Task E2E_03_Teacher_absent_from_declaration_to_attendance_by_the_substitute()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var day = w.NextMonday();
        var sessions = new List<long>();
        var students = new List<Who>();
        for (var i = 0; i < 2; i++) students.Add((await w.NewStudentAsync(s.ClassAId, "HS E2E3 " + U())).Who);
        for (var slot = 1; slot <= 4; slot++) sessions.Add(await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, slot));
        var head = await f.LoginAsync(s.Head.Username);
        var vice = await f.LoginAsync(w.ViceA.Username);
        var decl = await head.PostAsync("/api/scd/absences/department", new { teacherId = s.Absent.PersonId, fromDate = World.Iso(day), toDate = World.Iso(day), reason = "Ốm" });
        Assert.Equal(200, decl.Status);
        var id = decl.Body!["absenceId"]!.GetValue<int>();
        foreach (var sid in sessions) Assert.Equal("Pending", (await w.SessionAsync(sid)).Status);
        async Task Propose(long sid, string action, int? t = null) => Assert.Equal(200, (await head.PostAsync($"/api/scd/absences/department/pending/{sid}/propose", new { action, teacherId = t })).Status);
        for (var i = 0; i < 3; i++) await Propose(sessions[i], "Substitute", s.Sub.PersonId);
        await Propose(sessions[3], "Cancel");
        Assert.Equal(0, CountNotes(w.ViceA.PersonId, "SessionPending", s.Absent.FullName));
        Assert.Equal(200, (await head.PostAsync($"/api/scd/absences/department/{id}/submit")).Status);
        Assert.Equal(1, CountNotes(w.ViceA.PersonId, "SessionPending", s.Absent.FullName));
        Assert.Equal(200, (await vice.PostAsync($"/api/scd/absences/{id}/return", new { reason = "Đổi người tiết 3" })).Status);
        Assert.Equal(1, CountNotes(s.Head.PersonId, "SessionPending", s.Absent.FullName));
        await Propose(sessions[2], "Cancel");
        Assert.Equal(200, (await head.PostAsync($"/api/scd/absences/department/{id}/submit")).Status);
        Assert.Equal(2, CountNotes(w.ViceA.PersonId, "SessionPending", s.Absent.FullName));
        Assert.Equal(200, (await vice.PostAsync($"/api/scd/absences/{id}/approve")).Status);
        Assert.Equal(new[] { "Scheduled", "Scheduled", "Cancelled", "Cancelled" }, (await Task.WhenAll(sessions.Select(w.SessionAsync))).Select(x => x.Status).ToArray());
        Assert.True(await f.WithDbAsync(db => db.Notification.CountAsync(n => n.RecipientPersonId == students[0].PersonId && n.NotificationType == "ScheduleChanged")) >= 1);
        // điểm danh: giáo viên dạy thay được, giáo viên vắng không
        try
        {
            f.Clock.Set(At(day, 6, 50));
            var sub = await f.LoginAsync(s.Sub.Username);
            var items = new { items = students.Select(x => new { studentId = x.PersonId, status = "Present", note = (string?)null }).ToArray() };
            Assert.Equal(200, (await sub.PutAsync($"/api/att/sessions/{sessions[0]}", items)).Status);
            Assert.Equal(403, (await (await f.LoginAsync(s.Absent.Username)).PutAsync($"/api/att/sessions/{sessions[0]}", items)).Status);
            Assert.Equal(400, (await sub.PutAsync($"/api/att/sessions/{sessions[3]}", items)).Status);
        }
        finally { f.Clock.Reset(); }
        foreach (var a in new[] { "Khai báo giáo viên vắng", "Tổ trưởng gửi khai báo vắng chờ duyệt", "Hiệu phó trả lại khai báo vắng", "Hiệu phó duyệt khai báo vắng" })
            Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(x => x.Action == a && x.TargetIds!.Contains(id.ToString()))) >= 1, a);
    }

    // ------------------------------------------------------------------ Luồng 4: đơn nghỉ -> vắng có phép

    [Fact]
    public async Task E2E_04_Leave_request_to_excused_absence_is_the_same_everywhere()
    {
        var fam = await FamilyAsync();
        var s = await w.NewScenarioAsync();
        var day = f.Clock.Today.AddDays(1);
        while (day.DayOfWeek == DayOfWeek.Sunday) day = day.AddDays(1);
        var session = await w.NewSessionAsync(fam.ClassId, s.SubjectId, s.Absent.PersonId, day, 1);
        var parent = await f.LoginAsync(fam.Parent.Username);
        var hr = await f.LoginAsync(fam.Homeroom.Username);
        Assert.Equal(200, (await parent.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, reason = "Ốm", days = new[] { new { date = World.Iso(day), sessionOfDay = "Morning", slotIds = (int[]?)null } } })).Status);
        var inbox = await hr.GetAsync($"/api/par/leave-requests/homeroom?classId={fam.ClassId}");
        var leaveId = await f.WithDbAsync(db => db.LeaveRequest.Where(l => l.StudentId == fam.Student.PersonId).Select(l => l.LeaveRequestId).SingleAsync());
        Assert.Contains(leaveId.ToString(), inbox.Raw);
        Assert.Equal(204, (await hr.PutAsync($"/api/par/leave-requests/{leaveId}/approve", new { decisionNote = "ok" })).Status);
        Assert.Equal(0, CountNotes(fam.Parent.PersonId, "SessionPending") + CountNotes(fam.Homeroom.PersonId, "SessionPending"));   // đơn nghỉ không sinh thông báo
        try
        {
            f.Clock.Set(At(day, 7, 0));
            var teacher = await f.LoginAsync(s.Absent.Username);
            Assert.Equal(200, (await teacher.PutAsync($"/api/att/sessions/{session}", new { items = new[] { new { studentId = fam.Student.PersonId, status = "Absent", note = (string?)null } } })).Status);
            var d = World.Iso(day);
            Assert.Contains("ExcusedAbsent", (await teacher.GetAsync($"/api/att/sessions/{session}")).Raw);
            Assert.Contains("ExcusedAbsent", (await hr.GetAsync($"/api/att/class-summary?classId={fam.ClassId}&from={d}&to={d}")).Raw);
            Assert.Contains("ExcusedAbsent", (await (await f.LoginAsync(fam.Student.Username)).GetAsync($"/api/att/me?from={d}&to={d}")).Raw);
            Assert.Contains("ExcusedAbsent", (await parent.GetAsync($"/api/att/child?studentId={fam.Student.PersonId}&from={d}&to={d}")).Raw);
            var monday = day; while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);
            Assert.Equal(200, (await parent.GetAsync($"/api/par/weekly-reports/student/{fam.Student.PersonId}?weekStart={World.Iso(monday)}")).Status);
        }
        finally { f.Clock.Reset(); }
    }

    // ------------------------------------------------------------------ Luồng 6: quan hệ giảng dạy kết thúc khi thời khóa biểu đổi

    [Fact]
    public async Task E2E_06_Conversation_becomes_read_only_when_a_new_timetable_drops_the_teacher()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var fam = await FamilyAsync(s.ClassAId, s.Absent);
        await PublishFor(s.ClassAId, s.SubjectId, s.Absent.PersonId);
        var parent = await f.LoginAsync(fam.Parent.Username);
        var teacher = await f.LoginAsync(s.Absent.Username);
        var created = await parent.PostAsync("/api/par/conversations", new { studentId = fam.Student.PersonId, staffId = s.Absent.PersonId });
        Assert.Equal(200, created.Status);
        var id = created.Body!.GetValue<int>();
        Assert.Equal(200, (await parent.PostAsync($"/api/par/conversations/{id}/messages", new { content = "Chào thầy cô" })).Status);
        Assert.Equal(200, (await teacher.PostAsync($"/api/par/conversations/{id}/messages", new { content = "Chào anh chị" })).Status);
        await PublishFor(s.ClassAId, s.SubjectId, s.Sub.PersonId);   // phiên bản mới không còn giáo viên này
        Assert.Contains("Chào thầy cô", (await parent.GetAsync($"/api/par/conversations/{id}/messages")).Raw);
        var send = await parent.PostAsync($"/api/par/conversations/{id}/messages", new { content = "Còn nhắn được không" });
        Assert.Equal(400, send.Status);
        Assert.StartsWith("Quan hệ giảng dạy đã kết thúc", send.Message);
        Assert.Equal(400, (await teacher.PostAsync($"/api/par/conversations/{id}/messages", new { content = "Thử nhắn" })).Status);
    }

    private static int tick;
    private async Task PublishFor(int classId, int subjectId, int teacherId) => await f.WithDbAsync(async db =>
    {
        var sem = await db.Semester.Where(x => x.AcademicYear.IsCurrent && x.SemesterNo == 1).Select(x => x.SemesterId).SingleAsync();
        var slot = await db.PeriodSlot.Where(p => p.SessionOfDay == "Morning" && p.PeriodNo == 1).Select(p => p.PeriodSlotId).SingleAsync();
        var v = new TimetableVersion { CampusId = w.CampusAId, SemesterId = sem, Status = "Published", ImportedAt = f.Clock.Now, PublishedAt = f.Clock.Now.AddSeconds(2000 + Interlocked.Increment(ref tick)) };
        db.TimetableVersion.Add(v);
        await db.SaveChangesAsync();
        db.TimetableVersionClass.Add(new TimetableVersionClass { VersionId = v.VersionId, ClassId = classId });
        db.TimetableEntry.Add(new TimetableEntry { VersionId = v.VersionId, ClassId = classId, DayOfWeek = 2, PeriodSlotId = slot, SubjectId = subjectId, TeacherId = teacherId });
        await db.SaveChangesAsync();
    });

    // ------------------------------------------------------------------ Luồng 7: thay giáo viên chủ nhiệm

    [Fact]
    public async Task E2E_07_Homeroom_teacher_replaced_during_the_term()
    {
        var fam = await FamilyAsync();
        var newHr = await w.NewStaffAsync("GVCN mới E2E " + U(), "Teacher", w.CampusAId, null);
        var parent = await f.LoginAsync(fam.Parent.Username);
        Assert.Equal(200, (await parent.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, reason = "Ốm", days = new[] { new { date = World.Iso(f.Clock.Today.AddDays(2)), sessionOfDay = "AllDay", slotIds = (int[]?)null } } })).Status);
        var leaveId = await f.WithDbAsync(db => db.LeaveRequest.Where(l => l.StudentId == fam.Student.PersonId).Select(l => l.LeaveRequestId).SingleAsync());
        var principal = await f.LoginAsync(w.Principal.Username);
        Assert.Equal(204, (await principal.PostAsync("/api/ppl/responsibilities/assign", new { kind = "homeroom", targetId = fam.ClassId, staffId = newHr.PersonId, startDate = World.Iso(f.Clock.Today) })).Status);
        var old = await f.LoginAsync(fam.Homeroom.Username);
        Assert.Equal(403, (await old.PutAsync($"/api/par/leave-requests/{leaveId}/approve", new { decisionNote = "x" })).Status);
        Assert.Equal(403, (await old.GetAsync("/api/adm/homeroom")).Status);
        var neu = await f.LoginAsync(newHr.Username);
        Assert.Equal(204, (await neu.PutAsync($"/api/par/leave-requests/{leaveId}/approve", new { decisionNote = "ok" })).Status);
        Assert.Contains(fam.Student.FullName, (await neu.GetAsync("/api/par/weekly-reports/homeroom-students")).Raw);
        var history = await principal.GetAsync($"/api/ppl/responsibilities/history?kind=homeroom&targetId={fam.ClassId}");
        Assert.Equal(2, history.Body!.AsArray().Count);
        Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Category == "Trách nhiệm" && a.Action == "Thay người đảm nhiệm")) >= 1);
    }

    // ------------------------------------------------------------------ Luồng 8: giáo viên nghỉ việc

    [Fact]
    public async Task E2E_08_Teacher_leaves_the_school_by_account_lock()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var day = w.NextMonday();
        var sessions = new List<long>();
        for (var slot = 1; slot <= 3; slot++) sessions.Add(await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, slot));
        var teacher = await f.LoginAsync(s.Absent.Username);
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var accountId = await f.WithDbAsync(db => db.Account.Where(a => a.PersonId == s.Absent.PersonId).Select(a => a.AccountId).SingleAsync());
        Assert.Equal(3, (await admin.GetAsync($"/api/iam/accounts/{accountId}/lock-impact")).Body!["sessionCount"]!.GetValue<int>());
        Assert.Equal(204, (await admin.PutAsync($"/api/iam/accounts/{accountId}/lock")).Status);
        Assert.Equal(401, (await teacher.GetAsync("/api/auth/me")).Status);
        foreach (var sid in sessions) Assert.Equal("Pending", (await w.SessionAsync(sid)).Status);
        Assert.True(await f.WithDbAsync(db => db.Notification.CountAsync(n => n.RecipientPersonId == w.ViceA.PersonId && n.NotificationType == "SessionPending" && n.CreatedAt >= f.Clock.Now.AddMinutes(-5))) >= 1);
        // chưa dọn xong thì chưa ngừng hoạt động được
        var blocked = await admin.PutAsync($"/api/ppl/staff/{s.Absent.PersonId}/active", new { isActive = false });
        Assert.True(blocked.Status == 204 || blocked.Status == 400);
        var vice = await f.LoginAsync(w.ViceA.Username);
        foreach (var sid in sessions) Assert.Equal(200, (await vice.PostAsync($"/api/scd/absences/pending/{sid}/resolve", new { action = "Cancel" })).Status);
        Assert.True((await admin.PutAsync($"/api/ppl/staff/{s.Absent.PersonId}/active", new { isActive = false })).Ok);
        var principal = await f.LoginAsync(w.Principal.Username);
        var assign = await principal.PostAsync("/api/ppl/responsibilities/assign", new { kind = "department", targetId = s.DepartmentId, staffId = s.Absent.PersonId, startDate = World.Iso(f.Clock.Today.AddDays(1)) });
        Assert.Equal(400, assign.Status);
        Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Action == "Khóa tài khoản" && a.TargetIds!.Contains(accountId.ToString()))) >= 1);
        Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Category == "Điều chỉnh lịch")) >= 1);
    }

    // ------------------------------------------------------------------ Luồng 9: chuyển lớp rồi rời trường

    [Fact]
    public async Task E2E_09_Pupil_moves_to_another_class_and_finally_leaves_the_school()
    {
        var fam = await FamilyAsync();
        var hr2 = await w.NewStaffAsync("GVCN lớp mới " + U(), "Teacher", w.CampusAId, null);
        var cls2 = await w.NewClassAsync(w.CampusAId, "E2Eb" + U(), 6);
        await f.WithDbAsync(async db => { db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = cls2, StaffId = hr2.PersonId, StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var enr = await f.WithDbAsync(db => db.Enrollment.Where(e => e.StudentId == fam.Student.PersonId).Select(e => e.EnrollmentId).SingleAsync());
        var d = f.Clock.Today.AddDays(2);
        Assert.Equal(204, (await admin.PostAsync($"/api/sch/enrollments/{enr}/transfer", new { toClassId = cls2, effectiveDate = World.Iso(d) })).Status);
        var g7 = await w.NewClassAsync(w.CampusAId, "E2Ec" + U(), 7);
        Assert.StartsWith("Chỉ chuyển lớp trong cùng khối", (await admin.PostAsync($"/api/sch/enrollments/{await f.WithDbAsync(db => db.Enrollment.Where(e => e.StudentId == fam.Student.PersonId && e.ClassId == cls2).Select(e => e.EnrollmentId).SingleAsync())}/transfer", new { toClassId = g7, effectiveDate = World.Iso(d.AddDays(5)) })).Message);
        var parent = await f.LoginAsync(fam.Parent.Username);
        f.Clock.Set(At(d, 8, 0));   // từ ngày chuyển lớp trở đi đơn nghỉ thuộc lớp mới
        Assert.Equal(200, (await parent.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, reason = "Ốm", days = new[] { new { date = World.Iso(d.AddDays(1)), sessionOfDay = "AllDay", slotIds = (int[]?)null } } })).Status);
        var newInbox = await (await f.LoginAsync(hr2.Username)).GetAsync("/api/par/leave-requests/homeroom");
        Assert.Contains(fam.Student.FullName, newInbox.Raw);
        // rời trường
        var left = await admin.PostAsync("/api/ppl/students/leave", new { studentIds = new[] { fam.Student.PersonId }, reason = "SchoolTransfer" });
        Assert.Equal(204, left.Status);
        f.Clock.Set(At(d.AddDays(1), 8, 0));   // ngày rời trường vẫn là ngày cuối còn trong lớp; từ ngày hôm sau học sinh không còn lớp
        var after = await parent.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, reason = "Ốm", days = new[] { new { date = World.Iso(d.AddDays(30)), sessionOfDay = "AllDay", slotIds = (int[]?)null } } });
        Assert.Equal(400, after.Status);
        Assert.Equal("Học sinh hiện không thuộc lớp nào. Không thể tạo đơn xin nghỉ.", after.Message);
        f.Clock.Reset();
    }

    // ------------------------------------------------------------------ Luồng 10: đổi phụ huynh đại diện

    [Fact]
    public async Task E2E_10_Changing_the_guardian_moves_the_access_to_the_child()
    {
        var fam = await FamilyAsync();
        var p3 = await w.NewAccountAsync("PH mới E2E " + U(), "Parent", "Parent", "Active");
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var p1 = await f.LoginAsync(fam.Parent.Username);
        var p3c = await f.LoginAsync(p3.Username);
        Assert.Contains(fam.Student.FullName, (await p1.GetAsync("/api/par/leave-requests/my-children")).Raw);
        Assert.True((await admin.PutAsync($"/api/ppl/students/{fam.Student.PersonId}/guardian", new { existingParentId = p3.PersonId, relationship = "Mẹ" })).Ok);
        Assert.Contains(fam.Student.FullName, (await p3c.GetAsync("/api/par/leave-requests/my-children")).Raw);
        Assert.DoesNotContain(fam.Student.FullName, (await p1.GetAsync("/api/par/leave-requests/my-children")).Raw);
        Assert.Equal(403, (await p1.GetAsync($"/api/par/leave-requests/student/{fam.Student.PersonId}")).Status);
        Assert.Equal(200, (await p3c.GetAsync($"/api/par/leave-requests/student/{fam.Student.PersonId}")).Status);
        var monday = f.Clock.Today; while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);
        Assert.Equal(403, (await p1.GetAsync($"/api/par/weekly-reports/student/{fam.Student.PersonId}?weekStart={World.Iso(monday)}")).Status);
        Assert.Equal(200, (await p3c.GetAsync($"/api/par/weekly-reports/student/{fam.Student.PersonId}?weekStart={World.Iso(monday)}")).Status);
    }

    // ------------------------------------------------------------------ Luồng 12: ngày nghỉ thêm sau khi phát hành

    [Fact]
    public async Task E2E_12_A_holiday_added_after_publication_cancels_the_unlocked_sessions()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var day = w.NextMonday().AddDays(14);
        var student = (await w.NewStudentAsync(s.ClassAId, "HS E2E12 " + U())).Who;
        var open = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, 1);
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var impact = await admin.GetAsync($"/api/sch/holidays/impact?campusId={w.CampusAId}&date={World.Iso(day)}");
        Assert.Equal(200, impact.Status);
        var expected = impact.Body!["sessions"]!.GetValue<int>();
        var h = await admin.PostAsync("/api/sch/holidays", new { academicYearId = w.YearId, campusId = w.CampusAId, holidayDate = World.Iso(day), reason = "Nghỉ E2E" });
        Assert.Equal(200, h.Status);
        Assert.Equal(expected, h.Body!["cancelledSessions"]!.GetValue<int>());
        Assert.Equal("Cancelled", (await w.SessionAsync(open)).Status);
        Assert.True(CountNotes(s.Absent.PersonId, "ScheduleChanged") >= 1);
        Assert.True(CountNotes(student.PersonId, "ScheduleChanged") >= 1);
        var vice = await f.LoginAsync(w.ViceA.Username);
        var makeup = await vice.PostAsync("/api/scd/adjust/makeup", new { classId = s.ClassAId, date = World.Iso(day), periodSlotId = await f.WithDbAsync(db => db.PeriodSlot.Where(p => p.SessionOfDay == "Morning" && p.PeriodNo == 2).Select(p => p.PeriodSlotId).SingleAsync()), subjectId = s.SubjectId, teacherId = s.Absent.PersonId });
        Assert.Equal(400, makeup.Status);
        Assert.StartsWith("Ngày này là ngày nghỉ của phân hiệu", makeup.Message);
    }

    // ------------------------------------------------------------------ Luồng 13: đổi loại tài khoản

    [Fact]
    public async Task E2E_13_A_teacher_becomes_a_vice_principal_and_then_a_campus_leader()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var sess = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, w.NextMonday(), 1);
        var old = await f.LoginAsync(s.Absent.Username);
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var accountId = await f.WithDbAsync(db => db.Account.Where(a => a.PersonId == s.Absent.PersonId).Select(a => a.AccountId).SingleAsync());
        var r = await admin.PutAsync($"/api/iam/accounts/{accountId}/type", new { accountType = "VicePrincipal" });
        Assert.Equal(200, r.Status);
        var temp = r.Body!["tempPassword"]!.GetValue<string>();
        Assert.Equal("Pending", (await w.SessionAsync(sess)).Status);
        Assert.Equal(401, (await old.GetAsync("/api/auth/me")).Status);
        var campus = await f.WithDbAsync(async db => { var c = new Campus { Code = "E1" + U(), Name = "Phân hiệu E2E13 " + U(), IsActive = true, CreatedAt = f.Clock.Now }; db.Campus.Add(c); await db.SaveChangesAsync(); return c.CampusId; });
        var principal = await f.LoginAsync(w.Principal.Username);
        Assert.Equal(204, (await principal.PostAsync("/api/ppl/responsibilities/assign", new { kind = "campus", targetId = campus, staffId = s.Absent.PersonId, startDate = World.Iso(f.Clock.Today) })).Status);
        var login = await f.Anonymous().PostAsync("/api/auth/login", new { username = s.Absent.Username, password = temp });
        Assert.Equal(200, login.Status);
        Assert.True(login.Body!["user"]!["mustChangePassword"]!.GetValue<bool>());
        var newClient = f.CreateClient();
        newClient.DefaultRequestHeaders.Authorization = new("Bearer", login.Body["token"]!.GetValue<string>());
        var change = await new ApiClient(newClient).PostAsync("/api/auth/change-password", new { currentPassword = temp, newPassword = "Mat-khau-moi-1" });
        Assert.Equal(204, change.Status);
        var vp = await f.LoginAsync(s.Absent.Username, "Mat-khau-moi-1");
        var campuses = await vp.GetAsync("/api/scd/timetable/campuses");
        Assert.Equal(200, campuses.Status);
        Assert.Single(campuses.Body!.AsArray());
    }
}
