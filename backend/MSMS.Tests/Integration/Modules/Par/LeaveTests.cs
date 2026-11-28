using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Par;

/// <summary>PAR-01: đơn xin nghỉ (BR-19) và nhãn vắng có phép trong điểm danh (BR-18). Tên test bắt đầu bằng mã case (LEV_xx, ATT_xx).</summary>
[Collection("api")]
public class LeaveTests(ApiFactory f)
{
    private World w => f.World;

    private sealed record Family(int ClassId, Who Student, Who Parent, Who Homeroom, Who Other);

    /// <summary>Lớp mới có giáo viên chủ nhiệm, một học sinh, một phụ huynh liên kết và một giáo viên khác không chủ nhiệm.</summary>
    private async Task<Family> FamilyAsync()
    {
        f.Clock.Reset();
        await w.EnsureAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "LV" + World.Uniq(), 6);
        var hr = await w.NewStaffAsync("GVCN " + World.Uniq(), "Teacher", w.CampusAId, null);
        var other = await w.NewStaffAsync("GV khác " + World.Uniq(), "Teacher", w.CampusAId, null);
        var (student, _) = await w.NewStudentAsync(cls, "HS xin nghỉ " + World.Uniq());
        var parent = await w.NewAccountAsync("PH " + World.Uniq(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db =>
        {
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = cls, StaffId = hr.PersonId, StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = student.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        return new Family(cls, student, parent, hr, other);
    }

    private DateOnly Tomorrow => f.Clock.Today.AddDays(1);
    private static object Leave(Who student, DateOnly from, DateOnly to, string session = "AllDay", int[]? slots = null) =>
        new { studentId = student.PersonId, fromDate = World.Iso(from), toDate = World.Iso(to), sessionOfDay = session, slotIds = slots, reason = "Ốm" };

    private async Task<int> SubmitAsync(ApiClient parent, Who student, DateOnly from, DateOnly to, string session = "AllDay", int[]? slots = null)
    {
        var r = await parent.PostAsync("/api/par/leave-requests/bulk", new { studentId = student.PersonId, reason = "Ốm", days = new[] { new { date = World.Iso(from), sessionOfDay = session, slotIds = slots } } });
        Assert.Equal(200, r.Status);
        return await f.WithDbAsync(db => db.LeaveRequest.Where(l => l.StudentId == student.PersonId).OrderByDescending(l => l.LeaveRequestId).Select(l => l.LeaveRequestId).FirstAsync());
    }

    [Fact]
    public async Task LEV_01_To_05_Submit_validate_and_conflict_rules()
    {
        var fam = await FamilyAsync();
        var parent = await f.LoginAsync(fam.Parent.Username);
        // LEV-01: bản đơn qua bulk (đường duy nhất của giao diện)
        var id = await SubmitAsync(parent, fam.Student, Tomorrow, Tomorrow, "Morning");
        var mine = await parent.GetAsync($"/api/par/leave-requests/student/{fam.Student.PersonId}");
        Assert.Equal(200, mine.Status);
        Assert.Contains(id.ToString(), mine.Raw);
        var hr = await f.LoginAsync(fam.Homeroom.Username);
        var inbox = await hr.GetAsync($"/api/par/leave-requests/homeroom?classId={fam.ClassId}");
        Assert.Equal(200, inbox.Status);
        Assert.Contains(id.ToString(), inbox.Raw);
        Assert.Equal(0, await f.WithDbAsync(db => db.Notification.CountAsync(n => n.RecipientPersonId == fam.Homeroom.PersonId)));   // đơn nghỉ không sinh thông báo
        // LEV-02: ngày
        var past = await parent.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, days = new[] { new { date = World.Iso(f.Clock.Today.AddDays(-1)), sessionOfDay = "AllDay", slotIds = (int[]?)null } } });
        Assert.Equal(400, past.Status);
        Assert.Equal($"Ngày {f.Clock.Today.AddDays(-1):dd/MM/yyyy} đã qua, không thể xin nghỉ.", past.Message);
        var empty = await parent.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, days = Array.Empty<object>() });
        Assert.Equal("Chọn ít nhất một ngày nghỉ.", empty.Message);
        // LEV-03 + LEV-04: xung đột buổi (D sáng đã có)
        var sameMorning = await parent.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, days = new[] { new { date = World.Iso(Tomorrow), sessionOfDay = "Morning", slotIds = (int[]?)null } } });
        Assert.Equal(400, sameMorning.Status);
        Assert.StartsWith("Ngày", sameMorning.Message);
        Assert.Equal(200, (await parent.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, days = new[] { new { date = World.Iso(Tomorrow), sessionOfDay = "Afternoon", slotIds = (int[]?)null } } })).Status);
        var allDay = await parent.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, days = new[] { new { date = World.Iso(Tomorrow), sessionOfDay = "AllDay", slotIds = (int[]?)null } } });
        Assert.Equal(400, allDay.Status);
        // LEV-09 + LEV-05: rút đơn rồi nộp lại ngày đó
        Assert.Equal(204, (await parent.PutAsync($"/api/par/leave-requests/{id}/withdraw")).Status);
        Assert.Equal(200, (await parent.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, days = new[] { new { date = World.Iso(Tomorrow), sessionOfDay = "Morning", slotIds = (int[]?)null } } })).Status);
    }

    [Fact]
    public async Task LEV_03_Period_validations()
    {
        var fam = await FamilyAsync();
        var parent = await f.LoginAsync(fam.Parent.Username);
        async Task<ApiResult> One(string session, int[]? slots, DateOnly d) =>
            await parent.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, days = new[] { new { date = World.Iso(d), sessionOfDay = session, slotIds = slots } } });
        Assert.Equal("Buổi nghỉ không hợp lệ.", (await One("Evening", null, Tomorrow)).Message);
        Assert.Equal("Nghỉ theo tiết chỉ áp dụng cho buổi sáng hoặc buổi chiều.", (await One("AllDay", [1], Tomorrow)).Message);
        Assert.Equal("Số tiết không hợp lệ hoặc bị trùng (tiết từ 1 đến 5).", (await One("Morning", [0], Tomorrow)).Message);
        Assert.Equal("Số tiết không hợp lệ hoặc bị trùng (tiết từ 1 đến 5).", (await One("Morning", [6], Tomorrow)).Message);
        Assert.Equal("Số tiết không hợp lệ hoặc bị trùng (tiết từ 1 đến 5).", (await One("Morning", [2, 2], Tomorrow)).Message);
        Assert.Equal(200, (await One("Morning", [1, 2], Tomorrow.AddDays(2))).Status);
    }

    [Fact]
    public async Task LEV_07_And_08_Only_the_linked_parent_can_submit_and_read()
    {
        var fam = await FamilyAsync();
        var stranger = await w.NewAccountAsync("PH lạ " + World.Uniq(), "Parent", "Parent", "Active");
        var sc = await f.LoginAsync(stranger.Username);
        var r1 = await sc.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, days = new[] { new { date = World.Iso(Tomorrow), sessionOfDay = "AllDay", slotIds = (int[]?)null } } });
        Assert.Equal(403, r1.Status);
        Assert.Equal("Bạn không có quyền tạo đơn xin nghỉ cho học sinh này.", r1.Message);
        var r2 = await sc.GetAsync($"/api/par/leave-requests/student/{fam.Student.PersonId}");
        Assert.Equal(403, r2.Status);
        Assert.Equal("Bạn không có quyền xem danh sách xin nghỉ của học sinh này.", r2.Message);
        // học sinh không thuộc lớp nào (đã rời lớp)
        var left = await f.WithDbAsync(async db => { var e = await db.Enrollment.SingleAsync(x => x.StudentId == fam.Student.PersonId); e.EndDate = f.Clock.Today.AddDays(-1); await db.SaveChangesAsync(); return 0; });
        var pc = await f.LoginAsync(fam.Parent.Username);
        var r3 = await pc.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, days = new[] { new { date = World.Iso(Tomorrow), sessionOfDay = "AllDay", slotIds = (int[]?)null } } });
        Assert.Equal(400, r3.Status);
        Assert.Equal("Học sinh hiện không thuộc lớp nào. Không thể tạo đơn xin nghỉ.", r3.Message);
        Assert.Equal(0, left);
    }

    [Fact]
    public async Task LEV_10_To_16_Withdraw_decide_and_role_rules()
    {
        var fam = await FamilyAsync();
        var parent = await f.LoginAsync(fam.Parent.Username);
        var hr = await f.LoginAsync(fam.Homeroom.Username);
        var other = await f.LoginAsync(fam.Other.Username);
        var r1 = await SubmitAsync(parent, fam.Student, Tomorrow, Tomorrow);
        var r2 = await SubmitAsync(parent, fam.Student, Tomorrow.AddDays(1), Tomorrow.AddDays(1));
        var r3 = await SubmitAsync(parent, fam.Student, Tomorrow.AddDays(2), Tomorrow.AddDays(2));
        // LEV-15: giáo viên không chủ nhiệm
        var notHr = await other.PutAsync($"/api/par/leave-requests/{r1}/approve", new { decisionNote = "x" });
        Assert.Equal(403, notHr.Status);
        Assert.Equal("Bạn không phải là giáo viên chủ nhiệm hiện tại của lớp này.", notHr.Message);
        // LEV-12: duyệt
        Assert.Equal(204, (await hr.PutAsync($"/api/par/leave-requests/{r1}/approve", new { decisionNote = "Đồng ý" })).Status);
        var item = (await parent.GetAsync($"/api/par/leave-requests/student/{fam.Student.PersonId}")).Body!["items"]!.AsArray().Single(x => x!["leaveRequestId"]!.GetValue<int>() == r1)!;
        Assert.Equal("Approved", item["status"]!.GetValue<string>());
        Assert.Equal(fam.Homeroom.PersonId, item["decidedByPersonId"]!.GetValue<int>());
        // LEV-13: từ chối cần lý do
        var noReason = await hr.PutAsync($"/api/par/leave-requests/{r2}/reject", new { decisionNote = "" });
        Assert.Equal(400, noReason.Status);
        Assert.Equal("Vui lòng nhập lý do từ chối đơn xin nghỉ.", noReason.Message);
        Assert.Equal(204, (await hr.PutAsync($"/api/par/leave-requests/{r2}/reject", new { decisionNote = "Chưa hợp lý" })).Status);
        // LEV-14: chỉ duyệt đơn đang chờ
        Assert.Equal("Chỉ có thể duyệt đơn xin nghỉ ở trạng thái Chờ phê duyệt.", (await hr.PutAsync($"/api/par/leave-requests/{r1}/approve", new { decisionNote = "x" })).Message);
        Assert.Equal("Chỉ có thể từ chối đơn xin nghỉ ở trạng thái Chờ phê duyệt.", (await hr.PutAsync($"/api/par/leave-requests/{r2}/reject", new { decisionNote = "x" })).Message);
        // LEV-10: rút đơn đã duyệt; phụ huynh khác rút
        Assert.Equal("Chỉ có thể rút đơn xin nghỉ ở trạng thái Chờ phê duyệt.", (await parent.PutAsync($"/api/par/leave-requests/{r1}/withdraw")).Message);
        var stranger = await w.NewAccountAsync("PH ngoài " + World.Uniq(), "Parent", "Parent", "Active");
        Assert.Equal("Bạn không có quyền rút đơn xin nghỉ này.", (await (await f.LoginAsync(stranger.Username)).PutAsync($"/api/par/leave-requests/{r3}/withdraw")).Message);
        // LEV-16: ma trận vai trò
        Assert.Equal(403, (await (await f.LoginAsync(fam.Homeroom.Username)).PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, days = Array.Empty<object>() })).Status);
        foreach (var user in new[] { w.ViceA.Username, w.SchoolAdmin.Username, fam.Parent.Username })
            Assert.Equal(403, (await (await f.LoginAsync(user)).PutAsync($"/api/par/leave-requests/{r3}/approve", new { decisionNote = "x" })).Status);
        // LEV-15 (b): giáo viên chủ nhiệm cũ đã hết trách nhiệm từ hôm qua
        await f.WithDbAsync(async db => { (await db.HomeroomAssignment.SingleAsync(h => h.ClassId == fam.ClassId)).EndDate = f.Clock.Today.AddDays(-1); await db.SaveChangesAsync(); });
        Assert.Equal(403, (await hr.PutAsync($"/api/par/leave-requests/{r3}/approve", new { decisionNote = "x" })).Status);
    }

    // ------------------------------------------------------------------ Vắng có phép (BR-18)

    private async Task<(long Session, ApiClient Teacher)> SessionWithAbsentStudentAsync(Family fam, DateOnly day, int slotNo, string leaveSession, int[]? leaveSlots, string leaveOutcome)
    {
        var s = await w.NewScenarioAsync();
        var sessionId = await w.NewSessionAsync(fam.ClassId, s.SubjectId, s.Absent.PersonId, day, slotNo);
        var parent = await f.LoginAsync(fam.Parent.Username);
        var id = await SubmitAsync(parent, fam.Student, day, day, leaveSession, leaveSlots);
        if (leaveOutcome == "Approved") Assert.Equal(204, (await (await f.LoginAsync(fam.Homeroom.Username)).PutAsync($"/api/par/leave-requests/{id}/approve", new { decisionNote = "ok" })).Status);
        if (leaveOutcome == "Rejected") Assert.Equal(204, (await (await f.LoginAsync(fam.Homeroom.Username)).PutAsync($"/api/par/leave-requests/{id}/reject", new { decisionNote = "không" })).Status);
        if (leaveOutcome == "Withdrawn") Assert.Equal(204, (await parent.PutAsync($"/api/par/leave-requests/{id}/withdraw")).Status);
        var teacher = await f.LoginAsync(s.Absent.Username);
        f.Clock.Set(day.ToDateTime(new TimeOnly(7, 0)));
        return (sessionId, teacher);
    }

    private async Task<string> LabelAsync(ApiClient teacher, long session, int studentId)
    {
        Assert.Equal(200, (await teacher.PutAsync($"/api/att/sessions/{session}", new { items = new[] { new { studentId, status = "Absent", note = (string?)null } } })).Status);
        var sheet = await teacher.GetAsync($"/api/att/sessions/{session}");
        return sheet.Body!["students"]!.AsArray().Single(x => x!["studentId"]!.GetValue<int>() == studentId)!["label"]!.GetValue<string>();
    }

    [Fact]
    public async Task ATT_09_To_12_Absent_with_an_approved_leave_is_excused_otherwise_unexcused()
    {
        try
        {
            var fam = await FamilyAsync();
            var day = Tomorrow;
            // ATT-09: cả ngày được duyệt
            var a = await SessionWithAbsentStudentAsync(fam, day, 1, "AllDay", null, "Approved");
            Assert.Equal("ExcusedAbsent", await LabelAsync(a.Teacher, a.Session, fam.Student.PersonId));
            // ATT-10: nghỉ buổi sáng -> tiết sáng có phép, tiết chiều thì không
            f.Clock.Reset();
            var fam2 = await FamilyAsync();
            var m = await SessionWithAbsentStudentAsync(fam2, day, 1, "Morning", null, "Approved");
            Assert.Equal("ExcusedAbsent", await LabelAsync(m.Teacher, m.Session, fam2.Student.PersonId));
            // ATT-11: nghỉ theo tiết 2,3: tiết 1 không có phép
            f.Clock.Reset();
            var fam3 = await FamilyAsync();
            var p1 = await SessionWithAbsentStudentAsync(fam3, day, 1, "Morning", [2, 3], "Approved");
            Assert.Equal("UnexcusedAbsent", await LabelAsync(p1.Teacher, p1.Session, fam3.Student.PersonId));
            var s2 = await w.NewScenarioAsync();
            var session2 = await w.NewSessionAsync(fam3.ClassId, s2.SubjectId, s2.Absent.PersonId, day, 2);
            f.Clock.Set(day.ToDateTime(new TimeOnly(7, 45)));
            Assert.Equal("ExcusedAbsent", await LabelAsync(await f.LoginAsync(s2.Absent.Username), session2, fam3.Student.PersonId));
            // ATT-12: đơn chờ / từ chối / rút không có phép
            foreach (var outcome in new[] { "Pending", "Rejected", "Withdrawn" })
            {
                f.Clock.Reset();
                var fam4 = await FamilyAsync();
                var x = await SessionWithAbsentStudentAsync(fam4, day, 1, "AllDay", null, outcome);
                Assert.Equal("UnexcusedAbsent", await LabelAsync(x.Teacher, x.Session, fam4.Student.PersonId));
            }
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ATT_13_And_14_Late_is_never_excused_and_a_later_approval_changes_the_label()
    {
        try
        {
            var fam = await FamilyAsync();
            var day = Tomorrow;
            var s = await w.NewScenarioAsync();
            var session = await w.NewSessionAsync(fam.ClassId, s.SubjectId, s.Absent.PersonId, day, 1);
            var parent = await f.LoginAsync(fam.Parent.Username);
            var id = await SubmitAsync(parent, fam.Student, day, day, "AllDay", null);
            var teacher = await f.LoginAsync(s.Absent.Username);
            f.Clock.Set(day.ToDateTime(new TimeOnly(7, 0)));
            Assert.Equal(200, (await teacher.PutAsync($"/api/att/sessions/{session}", new { items = new[] { new { studentId = fam.Student.PersonId, status = "Late", note = (string?)null } } })).Status);
            await (await f.LoginAsync(fam.Homeroom.Username)).PutAsync($"/api/par/leave-requests/{id}/approve", new { decisionNote = "ok" });
            var sheet = await teacher.GetAsync($"/api/att/sessions/{session}");
            Assert.Equal("Late", sheet.Body!["students"]!.AsArray().Single()!["label"]!.GetValue<string>());   // ATT-13
            // ATT-14: Vắng lưu trước, duyệt đơn sau -> nhãn tính lại khi đọc
            f.Clock.Reset();
            var fam2 = await FamilyAsync();
            var s2 = await w.NewScenarioAsync();
            var session2 = await w.NewSessionAsync(fam2.ClassId, s2.SubjectId, s2.Absent.PersonId, day, 1);
            var parent2 = await f.LoginAsync(fam2.Parent.Username);
            var id2 = await SubmitAsync(parent2, fam2.Student, day, day, "AllDay", null);
            var teacher2 = await f.LoginAsync(s2.Absent.Username);
            f.Clock.Set(day.ToDateTime(new TimeOnly(7, 0)));
            Assert.Equal("UnexcusedAbsent", await LabelAsync(teacher2, session2, fam2.Student.PersonId));
            await (await f.LoginAsync(fam2.Homeroom.Username)).PutAsync($"/api/par/leave-requests/{id2}/approve", new { decisionNote = "ok" });
            var after = await teacher2.GetAsync($"/api/att/sessions/{session2}");
            Assert.Equal("ExcusedAbsent", after.Body!["students"]!.AsArray().Single()!["label"]!.GetValue<string>());
        }
        finally { f.Clock.Reset(); }
    }
}
