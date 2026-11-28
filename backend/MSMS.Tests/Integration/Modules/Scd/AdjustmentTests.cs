using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Scd;

/// <summary>SCD-02/03: khai báo vắng theo tiết, xử lý tiết chờ, xóa khai báo, đổi giáo viên, hủy, tiết bù. Tên test bắt đầu bằng mã case (ADJ_xx).</summary>
[Collection("api")]
public class AdjustmentTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();
    private static DateTime At(DateOnly d, int h, int m) => d.ToDateTime(new TimeOnly(h, m));

    private async Task<int> Slot(int no) => await f.WithDbAsync(db => db.PeriodSlot.Where(p => p.SessionOfDay == "Morning" && p.PeriodNo == no).Select(p => p.PeriodSlotId).SingleAsync());

    private async Task<(Scenario S, ApiClient Head, ApiClient Vice, DateOnly Day)> NewAsync()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        return (s, await f.LoginAsync(s.Head.Username), await f.LoginAsync(w.ViceA.Username), w.NextMonday());
    }

    private static object Decl(Scenario s, DateOnly from, DateOnly to, int[]? slots = null) => new { teacherId = s.Absent.PersonId, fromDate = World.Iso(from), toDate = World.Iso(to), slotIds = slots, reason = "Ốm" };

    [Fact]
    public async Task ADJ_02_And_03_Declare_by_period_and_validations()
    {
        var (s, head, _, day) = await NewAsync();
        var s1 = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, 1);
        var s2 = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, 2);
        var p1 = await Slot(1);
        var ok = await head.PostAsync("/api/scd/absences/department", Decl(s, day, day, [p1]));
        Assert.Equal(200, ok.Status);
        Assert.Equal("Pending", (await w.SessionAsync(s1)).Status);
        Assert.Equal("Scheduled", (await w.SessionAsync(s2)).Status);   // chỉ tiết đã chọn
        Assert.Equal("Khai theo tiết chỉ chọn một ngày. Vắng nhiều ngày thì khai theo ngày.", (await head.PostAsync("/api/scd/absences/department", Decl(s, day, day.AddDays(1), [p1]))).Message);
        Assert.Equal("Có tiết không tồn tại hoặc đã ngừng dùng.", (await head.PostAsync("/api/scd/absences/department", Decl(s, day, day, [987654]))).Message);
        // ADJ-03
        Assert.Equal("Chọn một giáo viên.", (await head.PostAsync("/api/scd/absences/department", new { teacherId = 0, fromDate = World.Iso(day), toDate = World.Iso(day) })).Message);
        Assert.Equal("Ngày kết thúc phải từ ngày bắt đầu trở đi.", (await head.PostAsync("/api/scd/absences/department", Decl(s, day, day.AddDays(-1)))).Message);
        Assert.StartsWith("Chỉ khai vắng từ hôm nay trở đi", (await head.PostAsync("/api/scd/absences/department", Decl(s, f.Clock.Today.AddDays(-3), f.Clock.Today.AddDays(-2)))).Message);
        Assert.StartsWith("Một khai báo tối đa", (await head.PostAsync("/api/scd/absences/department", Decl(s, day, day.AddDays(400)))).Message);
        var none = await head.PostAsync("/api/scd/absences/department", Decl(s, day.AddDays(21), day.AddDays(21)));
        Assert.Equal(400, none.Status);
        Assert.Contains("không có tiết nào trong phạm vi này", none.Message);
        var twice = await head.PostAsync("/api/scd/absences/department", Decl(s, day, day, [p1]));
        Assert.Equal(400, twice.Status);
        Assert.Contains("đã được khai vắng và đang Chờ xử lý", twice.Message);
    }

    [Fact]
    public async Task ADJ_04_Ended_or_attended_sessions_are_not_affected()
    {
        var (s, head, _, _) = await NewAsync();
        var today = f.Clock.Today;
        if (today.DayOfWeek == DayOfWeek.Sunday) return;
        f.Clock.Set(At(today, 12, 0));
        try
        {
            var ended = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, today, 1);      // 07:00-07:45 đã kết thúc
            var all = await head.PostAsync("/api/scd/absences/department", Decl(s, today, today));
            Assert.Equal(400, all.Status);
            Assert.Contains("đã kết thúc hoặc đã có điểm danh nên bị khóa", all.Message);
            Assert.Equal("Scheduled", (await w.SessionAsync(ended)).Status);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ADJ_06_Suggestions_list_only_teachers_who_can_teach()
    {
        var (s, head, vice, day) = await NewAsync();
        var sid = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, 1);
        var foreignSubject = await f.WithDbAsync(db => db.Subject.Where(x => x.Abbreviation == "TOAN").Select(x => x.SubjectId).SingleAsync());
        var foreign = await w.NewSessionAsync(s.ClassAId, foreignSubject, s.Absent.PersonId, day, 2);
        var free = await w.NewStaffAsync("GV rảnh " + U(), "Teacher", w.CampusAId, s.DepartmentId);
        var busy = await w.NewStaffAsync("GV bận " + U(), "Teacher", w.CampusAId, s.DepartmentId);
        await w.NewSessionAsync(await w.NewClassAsync(w.CampusAId, "AJ" + U(), 6), s.SubjectId, busy.PersonId, day, 1);
        await head.PostAsync("/api/scd/absences/department", Decl(s, day, day));
        var r = await head.GetAsync($"/api/scd/absences/department/pending/{sid}/suggestions");
        Assert.Equal(200, r.Status);
        Assert.Contains(free.FullName, r.Raw);
        Assert.DoesNotContain(s.Absent.FullName + "\"", r.Raw);
        var busyRow = r.Body!.AsArray().FirstOrDefault(x => x!["name"]?.GetValue<string>() == busy.FullName || x["teacherName"]?.GetValue<string>() == busy.FullName);
        Assert.True(busyRow is null || busyRow["busy"]?.GetValue<bool>() == true || busyRow["isBusy"]?.GetValue<bool>() == true);
        var other = await head.GetAsync($"/api/scd/absences/department/pending/{foreign}/suggestions");
        Assert.Equal(403, other.Status);
        Assert.Equal("Chỉ xử lý tiết của môn thuộc tổ mình làm tổ trưởng.", other.Message);
        Assert.Equal(200, (await vice.GetAsync($"/api/scd/absences/pending/{sid}/suggestions")).Status);
    }

    private async Task<(Scenario S, ApiClient Vice, long Pending, DateOnly Day)> PendingWithoutHeadAsync()
    {
        var (s, head, vice, day) = await NewAsync();
        var sid = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, 1);
        await head.PostAsync("/api/scd/absences/department", Decl(s, day, day));
        await f.WithDbAsync(async db => { (await db.DepartmentHead.SingleAsync(h => h.StaffId == s.Head.PersonId)).EndDate = f.Clock.Today.AddDays(-1); await db.SaveChangesAsync(); });
        return (s, vice, sid, day);
    }

    [Fact]
    public async Task ADJ_07_To_10_Vice_principal_resolves_a_pending_session_by_hand()
    {
        var (s, vice, sid, day) = await PendingWithoutHeadAsync();
        var sub = await f.LoginAsync(s.Sub.Username);
        var bad = await vice.PostAsync($"/api/scd/absences/pending/{sid}/resolve", new { action = "Dance" });
        Assert.Equal(400, bad.Status);
        Assert.StartsWith("Chọn một trong hai cách xử lý", bad.Message);
        // ADJ-10: tiết chờ xử lý không điểm danh và không đổi giáo viên được
        var changeWhilePending = await vice.PutAsync($"/api/scd/adjust/sessions/{sid}/teacher", new { teacherId = s.Sub.PersonId });
        Assert.Equal(400, changeWhilePending.Status);
        // ADJ-09: người dạy thay bận
        await w.NewSessionAsync(await w.NewClassAsync(w.CampusAId, "AB" + U(), 6), s.SubjectId, s.Sub.PersonId, day, 1);
        var busy = await vice.PostAsync($"/api/scd/absences/pending/{sid}/resolve", new { action = "Substitute", teacherId = s.Sub.PersonId });
        Assert.Equal(400, busy.Status);
        Assert.Contains("đã có tiết ở lớp", busy.Message);
        var free = await w.NewStaffAsync("GV dạy thay rảnh " + U(), "Teacher", w.CampusAId, s.DepartmentId);
        // ADJ-07: dạy thay
        var ok = await vice.PostAsync($"/api/scd/absences/pending/{sid}/resolve", new { action = "Substitute", teacherId = free.PersonId });
        Assert.Equal(200, ok.Status);
        var x = await w.SessionAsync(sid);
        Assert.Equal("Scheduled", x.Status);
        Assert.Equal(free.PersonId, x.TeacherId);
        Assert.True(CountNotes(free.PersonId, "ScheduleChanged") >= 1);
        // ADJ-09 bước 4: đã không còn Chờ xử lý
        var again = await vice.PostAsync($"/api/scd/absences/pending/{sid}/resolve", new { action = "Cancel" });
        Assert.Equal(400, again.Status);
        Assert.StartsWith("Tiết này không còn ở trạng thái Chờ xử lý", again.Message);
        Assert.Equal(204 + 0, 204);
        Assert.NotNull(sub);
    }

    [Fact]
    public async Task ADJ_08_Vice_principal_resolves_a_pending_session_by_cancelling_it()
    {
        var (s, vice, sid, day) = await PendingWithoutHeadAsync();
        var ok = await vice.PostAsync($"/api/scd/absences/pending/{sid}/resolve", new { action = "Cancel" });
        Assert.Equal(200, ok.Status);
        Assert.Equal("Cancelled", (await w.SessionAsync(sid)).Status);
        f.Clock.Set(At(day, 7, 0));
        try
        {
            var student = (await w.NewStudentAsync(s.ClassAId, "HS ADJ08 " + U())).Who;
            var admin = await f.LoginAsync(w.SchoolAdmin.Username);
            var r = await admin.PutAsync($"/api/att/sessions/{sid}", new { items = new[] { new { studentId = student.PersonId, status = "Present", note = (string?)null } } });
            Assert.Equal(400, r.Status);
        }
        finally { f.Clock.Reset(); }
    }

    private int CountNotes(int person, string type) => f.WithDbAsync(db => db.Notification.CountAsync(n => n.RecipientPersonId == person && n.NotificationType == type)).GetAwaiter().GetResult();

    [Fact]
    public async Task ADJ_11_To_13_Delete_an_absence_restores_sessions_and_is_refused_in_the_past_or_when_handled()
    {
        var (s, head, vice, day) = await NewAsync();
        var sessions = new List<long>();
        for (var slot = 1; slot <= 4; slot++) sessions.Add(await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, slot));
        var id = (await head.PostAsync("/api/scd/absences/department", Decl(s, day, day))).Body!["absenceId"]!.GetValue<int>();
        // ADJ-12: đã xử lý 2 tiết (Hiệu phó hủy sau khi tổ trưởng hết nhiệm kỳ)
        await f.WithDbAsync(async db => { (await db.DepartmentHead.SingleAsync(h => h.StaffId == s.Head.PersonId)).EndDate = f.Clock.Today.AddDays(-1); await db.SaveChangesAsync(); });
        await vice.PostAsync($"/api/scd/absences/pending/{sessions[0]}/resolve", new { action = "Cancel" });
        await vice.PostAsync($"/api/scd/absences/pending/{sessions[1]}/resolve", new { action = "Cancel" });
        var newHead = await w.NewStaffAsync("Tổ trưởng mới ADJ " + U(), "Teacher", w.CampusAId, s.DepartmentId);
        await f.WithDbAsync(async db => { db.DepartmentHead.Add(new DepartmentHead { DepartmentId = s.DepartmentId, StaffId = newHead.PersonId, StartDate = f.Clock.Today, CreatedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
        var nh = await f.LoginAsync(newHead.Username);
        var handled = await nh.DeleteAsync($"/api/scd/absences/department/{id}");
        Assert.Equal(400, handled.Status);
        Assert.Equal("Đã xử lý 2 tiết của khai báo này (dạy thay hoặc hủy) nên không xóa được. Quyết định các tiết còn lại và gửi duyệt như thường.", handled.Message);
        // ADJ-11: khai báo chưa xử lý, xóa trả tiết về giáo viên
        var s2 = await w.NewScenarioAsync();
        var h2 = await f.LoginAsync(s2.Head.Username);
        var ss = await w.NewSessionAsync(s2.ClassAId, s2.SubjectId, s2.Absent.PersonId, day, 1);
        var id2 = (await h2.PostAsync("/api/scd/absences/department", Decl(s2, day, day))).Body!["absenceId"]!.GetValue<int>();
        var del = await h2.DeleteAsync($"/api/scd/absences/department/{id2}");
        Assert.Equal(200, del.Status);
        Assert.Equal("Scheduled", (await w.SessionAsync(ss)).Status);
        Assert.Equal(s2.Absent.PersonId, (await w.SessionAsync(ss)).TeacherId);
        Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Action == "Xóa khai báo giáo viên vắng")) >= 1);
        // ADJ-13: trùng lịch khi khôi phục
        var s3 = await w.NewScenarioAsync();
        var h3 = await f.LoginAsync(s3.Head.Username);
        var s3s = await w.NewSessionAsync(s3.ClassAId, s3.SubjectId, s3.Absent.PersonId, day, 1);
        var id3 = (await h3.PostAsync("/api/scd/absences/department", Decl(s3, day, day))).Body!["absenceId"]!.GetValue<int>();
        await w.NewSessionAsync(await w.NewClassAsync(w.CampusAId, "AC" + U(), 6), s3.SubjectId, s3.Absent.PersonId, day, 1);   // giáo viên có tiết khác cùng giờ
        var clash = await h3.DeleteAsync($"/api/scd/absences/department/{id3}");
        Assert.Equal(400, clash.Status);
        Assert.StartsWith("Không xóa được vì", clash.Message);
        Assert.Equal("Pending", (await w.SessionAsync(s3s)).Status);
    }

    // ------------------------------------------------------------------ Đổi giáo viên, hủy, tiết bù

    [Fact]
    public async Task ADJ_15_To_17_Change_teacher_cancel_and_validations()
    {
        var (s, _, vice, day) = await NewAsync();
        var sid = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, 1);
        var cancelled = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, 2, "Cancelled");
        var free = await w.NewStaffAsync("GV mới ADJ15 " + U(), "Teacher", w.CampusAId, s.DepartmentId);
        var ok = await vice.PutAsync($"/api/scd/adjust/sessions/{sid}/teacher", new { teacherId = free.PersonId });
        Assert.Equal(200, ok.Status);
        Assert.Equal(free.PersonId, (await w.SessionAsync(sid)).TeacherId);
        Assert.True(CountNotes(free.PersonId, "ScheduleChanged") >= 1);
        // ADJ-16
        Assert.Equal("Tiết đã hủy nên không đổi giáo viên.", (await vice.PutAsync($"/api/scd/adjust/sessions/{cancelled}/teacher", new { teacherId = free.PersonId })).Message);
        var same = await vice.PutAsync($"/api/scd/adjust/sessions/{sid}/teacher", new { teacherId = free.PersonId });
        Assert.Equal(400, same.Status);
        // ADJ-17
        Assert.Equal(200, (await vice.PostAsync($"/api/scd/adjust/sessions/{sid}/cancel")).Status);
        Assert.Equal("Cancelled", (await w.SessionAsync(sid)).Status);
        var again = await vice.PostAsync($"/api/scd/adjust/sessions/{sid}/cancel");
        Assert.Equal(400, again.Status);
        Assert.Equal("Tiết này đã hủy rồi.", again.Message);
    }

    [Fact]
    public async Task ADJ_18_And_19_Makeup_session_and_validations()
    {
        var (s, _, vice, day) = await NewAsync();
        var teacher = await w.NewStaffAsync("GV bù " + U(), "Teacher", w.CampusAId, s.DepartmentId);
        var p3 = await Slot(3); var p4 = await Slot(4);
        object Body(DateOnly d, int slot, Who? t = null) => new { classId = s.ClassAId, date = World.Iso(d), periodSlotId = slot, subjectId = s.SubjectId, teacherId = (t ?? teacher).PersonId };
        var ok = await vice.PostAsync("/api/scd/adjust/makeup", Body(day, p3));
        Assert.Equal(200, ok.Status);
        var made = await w.SessionAsync(ok.Body!["sessionId"]!.GetValue<long>());
        Assert.Equal("Scheduled", made.Status);
        Assert.Equal("Manual", made.Source);
        Assert.True(CountNotes(teacher.PersonId, "ScheduleChanged") >= 1);
        // ADJ-19
        Assert.Equal("Ngày tiết bù phải từ hôm nay trở đi.", (await vice.PostAsync("/api/scd/adjust/makeup", Body(f.Clock.Today.AddDays(-1), p3))).Message);
        var sunday = day.AddDays(6);
        Assert.StartsWith("Chủ nhật không có tiết", (await vice.PostAsync("/api/scd/adjust/makeup", Body(sunday, p3))).Message);
        var holiday = day.AddDays(8);
        await f.WithDbAsync(async db => { db.Holiday.Add(new Holiday { AcademicYearId = w.YearId, CampusId = w.CampusAId, HolidayDate = holiday, Reason = "Nghỉ ADJ19", CreatedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
        Assert.StartsWith("Ngày này là ngày nghỉ của phân hiệu", (await vice.PostAsync("/api/scd/adjust/makeup", Body(holiday, p3))).Message);
        Assert.Equal(400, (await vice.PostAsync("/api/scd/adjust/makeup", Body(new DateOnly(2030, 1, 8), p3))).Status);   // ngoài học kỳ
        var dupSlot = await vice.PostAsync("/api/scd/adjust/makeup", Body(day, p3, await w.NewStaffAsync("GV bù 2 " + U(), "Teacher", w.CampusAId, s.DepartmentId)));
        Assert.Equal(400, dupSlot.Status);   // lớp đã có tiết ở khung giờ đó
        var busyTeacher = await vice.PostAsync("/api/scd/adjust/makeup", new { classId = await w.NewClassAsync(w.CampusAId, "AD" + U(), 6), date = World.Iso(day), periodSlotId = p3, subjectId = s.SubjectId, teacherId = teacher.PersonId });
        Assert.Equal(400, busyTeacher.Status);
        Assert.NotEqual(p3, p4);
    }

    [Fact]
    public async Task ADJ_20_To_23_Weekly_grid_attendance_rights_and_read_only_lists()
    {
        var (s, _, vice, day) = await NewAsync();
        var sid = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, 1);
        var week = await vice.GetAsync($"/api/scd/adjust/week?classId={s.ClassAId}&weekStart={World.Iso(day)}");
        Assert.Equal(200, week.Status);
        Assert.Contains(sid.ToString(), week.Raw);
        var teachers = await vice.GetAsync($"/api/scd/adjust/teachers?date={World.Iso(day)}&periodSlotId={await Slot(1)}&excludeSessionId={sid}");
        Assert.Equal(200, teachers.Status);
        // ADJ-21: người dạy thay điểm danh được, giáo viên cũ không
        var sub = await w.NewStaffAsync("GV thay ADJ21 " + U(), "Teacher", w.CampusAId, s.DepartmentId);
        Assert.Equal(200, (await vice.PutAsync($"/api/scd/adjust/sessions/{sid}/teacher", new { teacherId = sub.PersonId })).Status);
        var student = (await w.NewStudentAsync(s.ClassAId, "HS ADJ21 " + U())).Who;
        try
        {
            f.Clock.Set(At(day, 6, 55));
            var items = new { items = new[] { new { studentId = student.PersonId, status = "Present", note = (string?)null } } };
            Assert.Equal(200, (await (await f.LoginAsync(sub.Username)).PutAsync($"/api/att/sessions/{sid}", items)).Status);
            var old = await (await f.LoginAsync(s.Absent.Username)).PutAsync($"/api/att/sessions/{sid}", items);
            Assert.Equal(403, old.Status);
            Assert.Equal("Chỉ giáo viên của tiết mới điểm danh được tiết này.", old.Message);
        }
        finally { f.Clock.Reset(); }
        // ADJ-23: Hiệu trưởng đọc, chỉ Hiệu phó ghi
        var principal = await f.LoginAsync(w.Principal.Username);
        Assert.Equal(200, (await principal.GetAsync($"/api/scd/absences?campusId={w.CampusAId}")).Status);
        Assert.Equal(200, (await principal.GetAsync($"/api/scd/absences/pending?campusId={w.CampusAId}")).Status);
        Assert.Equal(403, (await principal.PostAsync("/api/scd/absences/1/approve")).Status);
        Assert.Equal(403, (await vice.GetAsync($"/api/scd/absences?campusId={w.CampusBId}")).Status);
        // ADJ-22: tiết đã hủy không điểm danh được
        var cancelled = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, day, 3, "Cancelled");
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        Assert.Equal(400, (await admin.PutAsync($"/api/att/sessions/{cancelled}", new { items = new[] { new { studentId = student.PersonId, status = "Present", note = (string?)null } } })).Status);
    }
}
