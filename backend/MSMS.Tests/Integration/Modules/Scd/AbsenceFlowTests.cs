using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Scd;

/// <summary>
/// SCD-03 qua HTTP thật: tổ trưởng khai báo vắng, đề xuất, gửi duyệt; Hiệu phó duyệt hoặc trả lại (BR-16).
/// Tên test bắt đầu bằng mã case trong báo cáo (ADJ_xx) để đối chiếu kết quả.
/// </summary>
[Collection("api")]
public class AbsenceFlowTests(ApiFactory f)
{
    private World w => f.World;

    private sealed record Flow(Scenario S, ApiClient Head, DateOnly Date, int AbsenceId, long SessA1, long SessA2, long SessB1);

    /// <summary>Tổ trưởng khai vắng ngày Thứ Hai tuần sau cho giáo viên vắng: 2 tiết ở phân hiệu A, 1 tiết ở phân hiệu B (đều thuộc môn của tổ).</summary>
    private async Task<Flow> DeclaredAsync()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var date = w.NextMonday();
        var a1 = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 1);
        var a2 = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 2);
        var b1 = await w.NewSessionAsync(s.ClassBId, s.SubjectId, s.Absent.PersonId, date, 3);
        var head = await f.LoginAsync(s.Head.Username);
        var r = await head.PostAsync("/api/scd/absences/department", new { teacherId = s.Absent.PersonId, fromDate = World.Iso(date), toDate = World.Iso(date), reason = "Ốm" });
        Assert.Equal(200, r.Status);
        return new Flow(s, head, date, r.Body!["absenceId"]!.GetValue<int>(), a1, a2, b1);
    }

    private static Task<ApiResult> Propose(Flow x, long session, string action, int? teacherId = null) =>
        x.Head.PostAsync($"/api/scd/absences/department/pending/{session}/propose", new { action, teacherId });

    private async Task ProposeAllAsync(Flow x)
    {
        Assert.Equal(200, (await Propose(x, x.SessA1, "Substitute", x.S.Sub.PersonId)).Status);
        Assert.Equal(200, (await Propose(x, x.SessA2, "Cancel")).Status);
        // tiết ở phân hiệu B dạy cùng khung giờ 3 bởi giáo viên thay: không trùng với 2 tiết trên
        Assert.Equal(200, (await Propose(x, x.SessB1, "Substitute", x.S.Sub.PersonId)).Status);
    }

    private Task<int> NotificationsOf(int personId, string title, string about) => f.WithDbAsync(db =>
        db.Notification.CountAsync(n => n.RecipientPersonId == personId && n.NotificationType == "SessionPending" && n.Title == title && n.Body!.Contains(about)));

    private Task<int> AuditCount(string action, int absenceId) => f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Action == action && a.TargetIds!.Contains(absenceId.ToString())));

    // ------------------------------------------------------------------ Khai báo

    [Fact]
    public async Task ADJ_01_Head_declares_a_teacher_absent_for_whole_days()
    {
        var x = await DeclaredAsync();
        foreach (var id in new[] { x.SessA1, x.SessA2, x.SessB1 })
        {
            var s = await w.SessionAsync(id);
            Assert.Equal("Pending", s.Status);
            Assert.Null(s.TeacherId);
            Assert.Equal(x.AbsenceId, s.TeacherAbsenceId);
        }
        var list = await x.Head.GetAsync("/api/scd/absences/department");
        Assert.Contains(list.Body!.AsArray(), a => a!["absenceId"]!.GetValue<int>() == x.AbsenceId);
        var pending = await x.Head.GetAsync("/api/scd/absences/department/pending");
        Assert.Equal(3, pending.Body!.AsArray().Count(p => p!["absenceId"]?.GetValue<int>() == x.AbsenceId));
    }

    [Fact]
    public async Task ADJ_05_Only_a_current_department_head_may_declare_and_only_for_own_department()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var date = w.NextMonday();
        await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 1);
        var body = new { teacherId = s.Absent.PersonId, fromDate = World.Iso(date), toDate = World.Iso(date), reason = "Ốm" };
        // giáo viên thường (không giữ trách nhiệm tổ trưởng)
        var plain = await f.LoginAsync(s.Sub.Username);
        var r1 = await plain.PostAsync("/api/scd/absences/department", body);
        Assert.Equal(403, r1.Status);
        Assert.Equal("Chỉ tổ trưởng đang giữ trách nhiệm mới dùng được chức năng này.", r1.Message);
        Assert.Equal(403, (await plain.GetAsync("/api/scd/absences/department")).Status);
        // tổ trưởng của tổ khác khai cho giáo viên tổ này
        var other = await w.NewScenarioAsync();
        var otherHead = await f.LoginAsync(other.Head.Username);
        var r2 = await otherHead.PostAsync("/api/scd/absences/department", body);
        Assert.Equal(403, r2.Status);
        Assert.Equal("Tổ trưởng chỉ khai vắng cho giáo viên thuộc tổ mình.", r2.Message);
        // các loại tài khoản khác
        foreach (var user in new[] { w.SchoolAdmin.Username, w.Principal.Username, w.ViceA.Username })
            Assert.Equal(403, (await (await f.LoginAsync(user)).PostAsync("/api/scd/absences/department", body)).Status);
        // tổ trưởng đã hết trách nhiệm từ hôm qua
        await f.WithDbAsync(async db =>
        {
            var h = await db.DepartmentHead.SingleAsync(d => d.StaffId == s.Head.PersonId);
            h.EndDate = f.Clock.Today.AddDays(-1);
            await db.SaveChangesAsync();
        });
        var ended = await f.LoginAsync(s.Head.Username);
        Assert.Equal(403, (await ended.PostAsync("/api/scd/absences/department", body)).Status);
    }

    // ------------------------------------------------------------------ Đề xuất và gửi duyệt

    [Fact]
    public async Task ADJ_24_Head_proposes_without_changing_the_schedule_and_cannot_edit_after_submit()
    {
        var x = await DeclaredAsync();
        Assert.Equal(200, (await Propose(x, x.SessA1, "Substitute", x.S.Sub.PersonId)).Status);
        Assert.Equal(200, (await Propose(x, x.SessA2, "Cancel")).Status);
        var a1 = await w.SessionAsync(x.SessA1);
        Assert.Equal("Pending", a1.Status);
        Assert.Equal("Substitute", a1.ProposedAction);
        Assert.Equal(200, (await Propose(x, x.SessA1, "Clear")).Status);
        Assert.Null((await w.SessionAsync(x.SessA1)).ProposedAction);

        // người dạy thay thuộc tổ khác, thiếu giáo viên, hành động lạ
        var otherDept = await w.NewScenarioAsync();
        var r4a = await Propose(x, x.SessA1, "Substitute", otherDept.Sub.PersonId);
        Assert.Equal(400, r4a.Status);
        Assert.Equal("Chỉ đề xuất giáo viên cùng tổ chuyên môn với môn của tiết.", r4a.Message);
        Assert.Equal("Chọn giáo viên dạy thay.", (await Propose(x, x.SessA1, "Substitute")).Message);
        Assert.Equal("Chọn một trong các cách xử lý: giáo viên dạy thay, hủy tiết hoặc bỏ đề xuất.", (await Propose(x, x.SessA1, "Dance")).Message);

        // tiết không thuộc khai báo nào
        var lone = await w.NewSessionAsync(x.S.ClassAId, x.S.SubjectId, null, x.Date, 5, "Pending");
        var r5 = await Propose(x, lone, "Cancel");
        Assert.Equal(400, r5.Status);
        Assert.Equal("Tiết này không thuộc khai báo vắng nào nên hiệu phó xử lý riêng, tổ trưởng không quyết định được.", r5.Message);

        // đề xuất nốt: Hiệu phó chưa được báo
        await Propose(x, x.SessA1, "Substitute", x.S.Sub.PersonId);
        await Propose(x, x.SessB1, "Cancel");
        Assert.Equal(0, await NotificationsOf(w.ViceA.PersonId, "Khai báo vắng chờ duyệt", x.S.Absent.FullName));

        // gửi duyệt rồi sửa đề xuất: bị khóa
        Assert.Equal(200, (await x.Head.PostAsync($"/api/scd/absences/department/{x.AbsenceId}/submit")).Status);
        var r7 = await Propose(x, x.SessA1, "Clear");
        Assert.Equal(400, r7.Status);
        Assert.Equal("Khai báo đã gửi hiệu phó duyệt nên không sửa đề xuất được. Chờ hiệu phó duyệt hoặc trả lại.", r7.Message);
    }

    [Fact]
    public async Task ADJ_26_Head_submits_the_absence_to_the_vice_principal()
    {
        var x = await DeclaredAsync();
        var submit = $"/api/scd/absences/department/{x.AbsenceId}/submit";
        // bước 1: còn tiết chưa quyết định
        await Propose(x, x.SessA1, "Substitute", x.S.Sub.PersonId);
        var r1 = await x.Head.PostAsync(submit);
        Assert.Equal(400, r1.Status);
        Assert.Equal("Còn 2/3 tiết chưa quyết định. Quyết định hết các tiết của tổ rồi gửi duyệt.", r1.Message);
        var before = (await x.Head.GetAsync("/api/scd/absences/department")).Body!.AsArray().Single(a => a!["absenceId"]!.GetValue<int>() == x.AbsenceId)!;
        Assert.Equal("Draft", before["status"]!.GetValue<string>());

        // bước 2: quyết định nốt rồi gửi
        await Propose(x, x.SessA2, "Cancel");
        await Propose(x, x.SessB1, "Substitute", x.S.Sub.PersonId);
        var r2 = await x.Head.PostAsync(submit);
        Assert.Equal(200, r2.Status);
        Assert.Equal("Submitted", r2.Body!["status"]!.GetValue<string>());
        Assert.Equal(1, await AuditCount("Tổ trưởng gửi khai báo vắng chờ duyệt", x.AbsenceId));

        // bước 3: gửi lại
        var r3 = await x.Head.PostAsync(submit);
        Assert.Equal(400, r3.Status);
        Assert.Equal("Khai báo này đã gửi hiệu phó duyệt rồi.", r3.Message);

        // bước 4: tổ trưởng của tổ khác, giáo viên thường, Hiệu phó
        var other = await w.NewScenarioAsync();
        var r4 = await (await f.LoginAsync(other.Head.Username)).PostAsync(submit);
        Assert.Equal(403, r4.Status);
        Assert.Equal("Chỉ tổ trưởng của giáo viên vắng mới gửi duyệt được.", r4.Message);
        Assert.Equal(403, (await (await f.LoginAsync(x.S.Sub.Username)).PostAsync(submit)).Status);
        Assert.Equal(403, (await (await f.LoginAsync(w.ViceA.Username)).PostAsync(submit)).Status);

        // bước 6: trạng thái, canSubmit và thông báo cho Hiệu phó của cả hai phân hiệu
        var after = (await x.Head.GetAsync("/api/scd/absences/department")).Body!.AsArray().Single(a => a!["absenceId"]!.GetValue<int>() == x.AbsenceId)!;
        Assert.Equal("Submitted", after["status"]!.GetValue<string>());
        Assert.False(after["canSubmit"]!.GetValue<bool>());
        Assert.Equal(1, await NotificationsOf(w.ViceA.PersonId, "Khai báo vắng chờ duyệt", x.S.Absent.FullName));
        Assert.Equal(1, await NotificationsOf(w.ViceB.PersonId, "Khai báo vắng chờ duyệt", x.S.Absent.FullName));
    }

    [Fact]
    public async Task ADJ_26_Submit_is_refused_when_only_sessions_of_another_department_are_left()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var date = w.NextMonday();
        var foreignSubject = await f.WithDbAsync(db => db.Subject.Where(x => x.Abbreviation == "TOAN").Select(x => x.SubjectId).SingleAsync());
        var foreign = await w.NewSessionAsync(s.ClassAId, foreignSubject, s.Absent.PersonId, date, 1);
        var head = await f.LoginAsync(s.Head.Username);
        var d = await head.PostAsync("/api/scd/absences/department", new { teacherId = s.Absent.PersonId, fromDate = World.Iso(date), toDate = World.Iso(date), reason = "Ốm" });
        var id = d.Body!["absenceId"]!.GetValue<int>();
        Assert.Equal("Pending", (await w.SessionAsync(foreign)).Status);
        var r = await head.PostAsync($"/api/scd/absences/department/{id}/submit");
        Assert.Equal(400, r.Status);
        Assert.Equal("Khai báo này chỉ còn tiết môn của tổ khác, tổ trưởng không quyết định được. Hiệu phó xử lý riêng các tiết đó.", r.Message);
    }

    // ------------------------------------------------------------------ Hiệu phó duyệt, trả lại

    [Fact]
    public async Task ADJ_25_Vice_principal_approves_a_submitted_absence_per_campus()
    {
        var x = await DeclaredAsync();
        var viceA = await f.LoginAsync(w.ViceA.Username);
        var viceB = await f.LoginAsync(w.ViceB.Username);
        await ProposeAllAsync(x);
        // bước 1: chưa gửi duyệt
        var r1 = await viceA.PostAsync($"/api/scd/absences/{x.AbsenceId}/approve");
        Assert.Equal(400, r1.Status);
        Assert.Equal("Tổ trưởng chưa gửi khai báo này duyệt. Chờ tổ trưởng quyết định và gửi.", r1.Message);
        // bước 2: gửi rồi duyệt phân hiệu A
        Assert.Equal(200, (await x.Head.PostAsync($"/api/scd/absences/department/{x.AbsenceId}/submit")).Status);
        var r2 = await viceA.PostAsync($"/api/scd/absences/{x.AbsenceId}/approve");
        Assert.Equal(200, r2.Status);
        Assert.Equal(1, r2.Body!["substitutedCount"]!.GetValue<int>());
        Assert.Equal(1, r2.Body["cancelledCount"]!.GetValue<int>());
        Assert.Equal(1, r2.Body["otherCampusPendingCount"]!.GetValue<int>());
        var a1 = await w.SessionAsync(x.SessA1);
        Assert.Equal("Scheduled", a1.Status);
        Assert.Equal(x.S.Sub.PersonId, a1.TeacherId);
        Assert.Equal("Cancelled", (await w.SessionAsync(x.SessA2)).Status);
        Assert.Equal("Pending", (await w.SessionAsync(x.SessB1)).Status);
        Assert.Equal(1, await AuditCount("Hiệu phó duyệt khai báo vắng", x.AbsenceId));
        // thông báo ScheduleChanged tới giáo viên dạy thay
        var scheduleChanged = await f.WithDbAsync(db => db.Notification.CountAsync(n => n.RecipientPersonId == x.S.Sub.PersonId && n.NotificationType == "ScheduleChanged"));
        Assert.True(scheduleChanged >= 1);
        // bước 4: duyệt lại phân hiệu A: không còn tiết nào chờ ở phân hiệu A
        var r4 = await viceA.PostAsync($"/api/scd/absences/{x.AbsenceId}/approve");
        Assert.Equal(403, r4.Status);
        Assert.Equal("Khai báo này không có tiết ở phân hiệu bạn phụ trách.", r4.Message);
        // bước 5: phân hiệu B duyệt độc lập
        var r5 = await viceB.PostAsync($"/api/scd/absences/{x.AbsenceId}/approve");
        Assert.Equal(200, r5.Status);
        Assert.Equal("Scheduled", (await w.SessionAsync(x.SessB1)).Status);
    }

    [Fact]
    public async Task ADJ_25_Approve_applies_nothing_when_a_substitute_became_busy()
    {
        var x = await DeclaredAsync();
        var viceA = await f.LoginAsync(w.ViceA.Username);
        await ProposeAllAsync(x);
        Assert.Equal(200, (await x.Head.PostAsync($"/api/scd/absences/department/{x.AbsenceId}/submit")).Status);
        // giáo viên dạy thay bận tiết 1 cùng ngày ở lớp khác
        await w.NewSessionAsync(await w.NewClassAsync(w.CampusAId, "KX" + World.Uniq(), 6), x.S.SubjectId, x.S.Sub.PersonId, x.Date, 1);
        var r = await viceA.PostAsync($"/api/scd/absences/{x.AbsenceId}/approve");
        Assert.Equal(400, r.Status);
        Assert.StartsWith("Chưa duyệt được vì 1 tiết không còn hợp lệ:", r.Message);
        Assert.Equal("Pending", (await w.SessionAsync(x.SessA1)).Status);
        Assert.Equal("Pending", (await w.SessionAsync(x.SessA2)).Status);
    }

    [Fact]
    public async Task ADJ_27_Vice_principal_returns_a_submitted_absence_with_a_reason()
    {
        var x = await DeclaredAsync();
        var viceA = await f.LoginAsync(w.ViceA.Username);
        var returnUrl = $"/api/scd/absences/{x.AbsenceId}/return";
        await ProposeAllAsync(x);
        // bước 3: chưa gửi thì chưa có gì để trả
        var notSent = await viceA.PostAsync(returnUrl, new { reason = "x" });
        Assert.Equal(400, notSent.Status);
        Assert.Equal("Tổ trưởng chưa gửi khai báo này, chưa có gì để trả lại.", notSent.Message);
        await x.Head.PostAsync($"/api/scd/absences/department/{x.AbsenceId}/submit");

        // bước 2: lý do rỗng hoặc quá dài
        Assert.Equal(400, (await viceA.PostAsync(returnUrl, new { reason = "" })).Status);
        Assert.Equal(400, (await viceA.PostAsync(returnUrl, new { reason = new string('a', 301) })).Status);
        // bước 5: loại tài khoản khác
        foreach (var user in new[] { w.Principal.Username, w.SchoolAdmin.Username, x.S.Sub.Username })
            Assert.Equal(403, (await (await f.LoginAsync(user)).PostAsync(returnUrl, new { reason = "x" })).Status);

        // bước 1: trả lại
        var r1 = await viceA.PostAsync(returnUrl, new { reason = "Đổi người dạy thay tiết 3" });
        Assert.Equal(200, r1.Status);
        Assert.Equal("Returned", r1.Body!["status"]!.GetValue<string>());
        Assert.Equal("Pending", (await w.SessionAsync(x.SessA1)).Status);
        Assert.Equal(1, await AuditCount("Hiệu phó trả lại khai báo vắng", x.AbsenceId));

        // bước 4: tổ trưởng nhận thông báo kèm lý do, thấy trạng thái Returned, sửa rồi gửi lại
        Assert.Equal(1, await NotificationsOf(x.S.Head.PersonId, "Khai báo vắng bị trả lại", x.S.Absent.FullName));
        var body = await f.WithDbAsync(db => db.Notification.Where(n => n.RecipientPersonId == x.S.Head.PersonId && n.Title == "Khai báo vắng bị trả lại").Select(n => n.Body).SingleAsync());
        Assert.Contains("Đổi người dạy thay tiết 3", body);
        var row = (await x.Head.GetAsync("/api/scd/absences/department")).Body!.AsArray().Single(a => a!["absenceId"]!.GetValue<int>() == x.AbsenceId)!;
        Assert.Equal("Returned", row["status"]!.GetValue<string>());
        Assert.Equal("Đổi người dạy thay tiết 3", row["returnReason"]!.GetValue<string>());
        Assert.Equal(200, (await Propose(x, x.SessA2, "Substitute", x.S.Sub.PersonId)).Status);
        Assert.Equal(200, (await x.Head.PostAsync($"/api/scd/absences/department/{x.AbsenceId}/submit")).Status);
        var again = (await x.Head.GetAsync("/api/scd/absences/department")).Body!.AsArray().Single(a => a!["absenceId"]!.GetValue<int>() == x.AbsenceId)!;
        Assert.Equal("Submitted", again["status"]!.GetValue<string>());
        Assert.Null(again["returnReason"]);
    }

    [Fact]
    public async Task ADJ_27_A_vice_principal_without_sessions_in_the_campus_cannot_return()
    {
        var x = await DeclaredAsync();
        // khai báo chỉ có tiết ở phân hiệu A
        await f.WithDbAsync(async db =>
        {
            db.TeachingSession.Remove(await db.TeachingSession.SingleAsync(s => s.SessionId == x.SessB1));
            await db.SaveChangesAsync();
        });
        await Propose(x, x.SessA1, "Cancel");
        await Propose(x, x.SessA2, "Cancel");
        Assert.Equal(200, (await x.Head.PostAsync($"/api/scd/absences/department/{x.AbsenceId}/submit")).Status);
        var r = await (await f.LoginAsync(w.ViceB.Username)).PostAsync($"/api/scd/absences/{x.AbsenceId}/return", new { reason = "x" });
        Assert.Equal(403, r.Status);
        Assert.Equal("Khai báo này không có tiết ở phân hiệu bạn phụ trách.", r.Message);
    }

    [Fact]
    public async Task ADJ_28_Approval_leaves_sessions_of_another_department_pending()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var date = w.NextMonday();
        var foreignSubject = await f.WithDbAsync(db => db.Subject.Where(x => x.Abbreviation == "TOAN").Select(x => x.SubjectId).SingleAsync());
        var own1 = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 1);
        var own2 = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 2);
        var foreign = await w.NewSessionAsync(s.ClassAId, foreignSubject, s.Absent.PersonId, date, 3);
        var head = await f.LoginAsync(s.Head.Username);
        var viceA = await f.LoginAsync(w.ViceA.Username);
        var d = await head.PostAsync("/api/scd/absences/department", new { teacherId = s.Absent.PersonId, fromDate = World.Iso(date), toDate = World.Iso(date), reason = "Ốm" });
        var id = d.Body!["absenceId"]!.GetValue<int>();
        await head.PostAsync($"/api/scd/absences/department/pending/{own1}/propose", new { action = "Substitute", teacherId = s.Sub.PersonId });
        await head.PostAsync($"/api/scd/absences/department/pending/{own2}/propose", new { action = "Cancel" });
        // bước 5: chưa gửi
        Assert.Equal("Tổ trưởng chưa gửi khai báo này duyệt. Chờ tổ trưởng quyết định và gửi.", (await viceA.PostAsync($"/api/scd/absences/{id}/approve")).Message);
        Assert.Equal(200, (await head.PostAsync($"/api/scd/absences/department/{id}/submit")).Status);
        // bước 1
        var r = await viceA.PostAsync($"/api/scd/absences/{id}/approve");
        Assert.Equal(200, r.Status);
        Assert.Equal(1, r.Body!["leftPendingCount"]!.GetValue<int>());
        Assert.Equal("Scheduled", (await w.SessionAsync(own1)).Status);
        Assert.Equal("Cancelled", (await w.SessionAsync(own2)).Status);
        Assert.Equal("Pending", (await w.SessionAsync(foreign)).Status);
        // bước 2: còn trong danh sách chờ
        var pending = await viceA.GetAsync($"/api/scd/absences/pending?campusId={w.CampusAId}&classId={s.ClassAId}");
        Assert.Contains(pending.Body!.AsArray(), p => p!["sessionId"]!.GetValue<long>() == foreign);
        // bước 3: Hiệu phó xử lý riêng (dạy thay) tiết còn lại
        var res = await viceA.PostAsync($"/api/scd/absences/pending/{foreign}/resolve", new { action = "Cancel" });
        Assert.Equal(200, res.Status);
        Assert.Equal("Cancelled", (await w.SessionAsync(foreign)).Status);
    }

    [Fact]
    public async Task ADJ_28_Approval_without_any_proposal_in_the_campus_is_refused()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var date = w.NextMonday();
        var foreignSubject = await f.WithDbAsync(db => db.Subject.Where(x => x.Abbreviation == "TOAN").Select(x => x.SubjectId).SingleAsync());
        var own = await w.NewSessionAsync(s.ClassBId, s.SubjectId, s.Absent.PersonId, date, 1);       // phân hiệu B, có đề xuất
        await w.NewSessionAsync(s.ClassAId, foreignSubject, s.Absent.PersonId, date, 2);               // phân hiệu A, môn tổ khác, không đề xuất
        var head = await f.LoginAsync(s.Head.Username);
        var d = await head.PostAsync("/api/scd/absences/department", new { teacherId = s.Absent.PersonId, fromDate = World.Iso(date), toDate = World.Iso(date), reason = "Ốm" });
        var id = d.Body!["absenceId"]!.GetValue<int>();
        await head.PostAsync($"/api/scd/absences/department/pending/{own}/propose", new { action = "Cancel" });
        Assert.Equal(200, (await head.PostAsync($"/api/scd/absences/department/{id}/submit")).Status);
        var r = await (await f.LoginAsync(w.ViceA.Username)).PostAsync($"/api/scd/absences/{id}/approve");
        Assert.Equal(400, r.Status);
        Assert.Equal("Khai báo này không có đề xuất nào ở phân hiệu bạn (các tiết còn lại thuộc môn của tổ khác). Xử lý riêng các tiết đó trên lưới Điều chỉnh.", r.Message);
    }

    [Fact]
    public async Task ADJ_29_A_submitted_absence_cannot_be_deleted_and_any_current_head_may_delete_a_draft()
    {
        var x = await DeclaredAsync();
        await ProposeAllAsync(x);
        Assert.Equal(200, (await x.Head.PostAsync($"/api/scd/absences/department/{x.AbsenceId}/submit")).Status);
        var r2 = await x.Head.DeleteAsync($"/api/scd/absences/department/{x.AbsenceId}");
        Assert.Equal(400, r2.Status);
        Assert.Equal("Khai báo đã gửi hiệu phó duyệt nên không xóa được. Chờ hiệu phó duyệt, hoặc nhờ hiệu phó trả lại rồi xóa.", r2.Message);
        // bước 3: trả lại rồi xóa
        Assert.Equal(200, (await (await f.LoginAsync(w.ViceA.Username)).PostAsync($"/api/scd/absences/{x.AbsenceId}/return", new { reason = "Làm lại" })).Status);
        var del = await x.Head.DeleteAsync($"/api/scd/absences/department/{x.AbsenceId}");
        Assert.Equal(200, del.Status);
        Assert.Equal(3, del.Body!["restoredCount"]!.GetValue<int>());
        Assert.Equal("Scheduled", (await w.SessionAsync(x.SessA1)).Status);
        Assert.Equal(x.S.Absent.PersonId, (await w.SessionAsync(x.SessA1)).TeacherId);
    }

    [Fact]
    public async Task ADJ_29_A_new_head_deletes_an_absence_declared_by_the_previous_head()
    {
        var x = await DeclaredAsync();
        var newHead = await w.NewStaffAsync("Tổ trưởng mới " + World.Uniq(), "Teacher", w.CampusAId, x.S.DepartmentId);
        await f.WithDbAsync(async db =>
        {
            var old = await db.DepartmentHead.SingleAsync(h => h.StaffId == x.S.Head.PersonId);
            old.EndDate = f.Clock.Today.AddDays(-1);
            db.DepartmentHead.Add(new Api.Data.Entities.DepartmentHead { DepartmentId = x.S.DepartmentId, StaffId = newHead.PersonId, StartDate = f.Clock.Today, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        // tổ trưởng cũ (đã hết trách nhiệm) bị từ chối
        var oldHead = await f.LoginAsync(x.S.Head.Username);
        var refused = await oldHead.DeleteAsync($"/api/scd/absences/department/{x.AbsenceId}");
        Assert.Equal(403, refused.Status);
        Assert.Equal("Chỉ tổ trưởng đương nhiệm của tổ giáo viên vắng mới xóa được khai báo.", refused.Message);
        var ok = await (await f.LoginAsync(newHead.Username)).DeleteAsync($"/api/scd/absences/department/{x.AbsenceId}");
        Assert.Equal(200, ok.Status);
    }

    [Fact]
    public async Task ADJ_30_Sessions_waiting_for_the_department_head_cannot_be_handled_by_the_vice_principal()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var date = w.NextMonday();
        var foreignSubject = await f.WithDbAsync(db => db.Subject.Where(x => x.Abbreviation == "TOAN").Select(x => x.SubjectId).SingleAsync());
        var own = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 1);
        var foreign = await w.NewSessionAsync(s.ClassAId, foreignSubject, s.Absent.PersonId, date, 2);
        var head = await f.LoginAsync(s.Head.Username);
        var viceA = await f.LoginAsync(w.ViceA.Username);
        var d = await head.PostAsync("/api/scd/absences/department", new { teacherId = s.Absent.PersonId, fromDate = World.Iso(date), toDate = World.Iso(date), reason = "Ốm" });
        var id = d.Body!["absenceId"]!.GetValue<int>();

        // bước 1: cờ awaitingHead trên lưới Điều chỉnh
        var week = await viceA.GetAsync($"/api/scd/adjust/week?classId={s.ClassAId}&weekStart={World.Iso(date)}");
        Assert.Equal(200, week.Status);
        var sessions = week.Body!["sessions"]!.AsArray();
        Assert.True(sessions.Single(x => x!["sessionId"]!.GetValue<long>() == own)!["awaitingHead"]!.GetValue<bool>());
        Assert.False(sessions.Single(x => x!["sessionId"]!.GetValue<long>() == foreign)!["awaitingHead"]!.GetValue<bool>());
        // bước 2: Hiệu phó xử lý tiết của tổ trưởng: bị chặn
        var r2 = await viceA.PostAsync($"/api/scd/absences/pending/{own}/resolve", new { action = "Cancel" });
        Assert.Equal(400, r2.Status);
        Assert.Equal("Tiết này đang chờ tổ trưởng quyết định. Hiệu phó xử lý được sau khi tổ trưởng gửi khai báo duyệt.", r2.Message);
        // bước 3: tiết môn của tổ khác thì được
        Assert.Equal(200, (await viceA.PostAsync($"/api/scd/absences/pending/{foreign}/resolve", new { action = "Cancel" })).Status);
        // bước 5: tổ trưởng gửi duyệt rồi Hiệu phó xử lý lại được
        await head.PostAsync($"/api/scd/absences/department/pending/{own}/propose", new { action = "Cancel" });
        Assert.Equal(200, (await head.PostAsync($"/api/scd/absences/department/{id}/submit")).Status);
        Assert.Equal(200, (await viceA.PostAsync($"/api/scd/absences/pending/{own}/resolve", new { action = "Cancel" })).Status);
    }

    [Fact]
    public async Task ADJ_30_A_department_without_a_head_is_handled_by_the_vice_principal_at_once()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var date = w.NextMonday();
        var sess = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 1);
        // đưa khai báo vào bằng cách khai khi còn tổ trưởng, rồi tổ trưởng hết trách nhiệm
        var head = await f.LoginAsync(s.Head.Username);
        await head.PostAsync("/api/scd/absences/department", new { teacherId = s.Absent.PersonId, fromDate = World.Iso(date), toDate = World.Iso(date), reason = "Ốm" });
        await f.WithDbAsync(async db =>
        {
            (await db.DepartmentHead.SingleAsync(h => h.StaffId == s.Head.PersonId)).EndDate = f.Clock.Today.AddDays(-1);
            await db.SaveChangesAsync();
        });
        var r = await (await f.LoginAsync(w.ViceA.Username)).PostAsync($"/api/scd/absences/pending/{sess}/resolve", new { action = "Cancel" });
        Assert.Equal(200, r.Status);
    }
}
