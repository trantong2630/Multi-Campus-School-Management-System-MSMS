using ClosedXML.Excel;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Scd.Services;
using MSMS.Tests.Integration.Infrastructure;
using static MSMS.Tests.Integration.Infrastructure.Roles;

namespace MSMS.Tests.Integration.Modules.Scd;

/// <summary>
/// SCD-01: mẫu, import, sửa bản nháp, xem trước, phát hành thời khóa biểu (BR-12, BR-13, BR-14) và các màn xem lịch.
/// Mỗi test dựng một phân hiệu riêng (2 lớp, 1 Hiệu phó phụ trách) để các phiên bản không dẫm lên nhau. Tên test bắt đầu bằng mã case (TTB_xx).
/// </summary>
[Collection("api")]
public partial class TimetableTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();

    private sealed record Setup(int CampusId, int SemesterId, Who Vice, int Class1, int Class2, string Class1Name, string Class2Name, Scenario S, ApiClient VpClient);

    private async Task<Setup> NewSetupAsync(int semesterNo = 1)
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var campus = await f.WithDbAsync(async db =>
        {
            var c = new Campus { Code = "TB" + U(), Name = "Phân hiệu TKB " + U(), IsActive = true, CreatedAt = f.Clock.Now };
            db.Campus.Add(c);
            await db.SaveChangesAsync();
            return c.CampusId;
        });
        var vice = await w.NewStaffAsync("HP TKB " + U(), "VicePrincipal", campus, null);
        await f.WithDbAsync(async db =>
        {
            db.CampusLeadership.Add(new CampusLeadership { CampusId = campus, StaffId = vice.PersonId, StartDate = new DateOnly(2026, 9, 5), CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var n1 = "T1" + U(); var n2 = "T2" + U();
        var c1 = await w.NewClassAsync(campus, n1, 6);
        var c2 = await w.NewClassAsync(campus, n2, 6);
        var sem = await f.WithDbAsync(db => db.Semester.Where(s => s.AcademicYear.IsCurrent && s.SemesterNo == semesterNo).Select(s => s.SemesterId).SingleAsync());
        var scenario = await w.NewScenarioAsync();
        return new Setup(campus, sem, vice, c1, c2, n1, n2, scenario, await f.LoginAsync(vice.Username));
    }

    private async Task<byte[]> TemplateAsync(Setup s)
    {
        var (status, bytes, _) = await s.VpClient.GetBytesAsync($"/api/scd/timetable/template?campusId={s.CampusId}&semesterId={s.SemesterId}");
        Assert.Equal(200, status);
        return bytes;
    }

    /// <summary>Điền các ô (cột lớp 0 hoặc 1, thứ 2..7, chỉ số khung giờ trong ngày) bằng chuỗi "VIẾTTẮTMÔN-VIẾTTẮTGV".</summary>
    private static byte[] Fill(byte[] template, params (int ClassIndex, int Day, int SlotIndex, string Text)[] cells)
    {
        using var inStream = new MemoryStream(template);
        using var wb = new XLWorkbook(inStream);
        var ws = wb.Worksheet("01_TKB");
        var rows = 0;
        while (ws.Cell(TimetableLayout.FirstDataRow + rows, 3).GetString().Length > 0) rows++;
        var perDay = rows / 6;
        foreach (var (ci, day, slot, text) in cells)
            ws.Cell(TimetableLayout.FirstDataRow + (day - 2) * perDay + slot, TimetableLayout.FirstClassColumn + ci).Value = text;
        using var outStream = new MemoryStream();
        wb.SaveAs(outStream);
        return outStream.ToArray();
    }


    private async Task<string> AbbrOf(Who t) => await f.WithDbAsync(db => db.StaffProfile.Where(p => p.StaffId == t.PersonId).Select(p => p.TeacherAbbr!).SingleAsync());
    private async Task<string> SubjAbbr(Scenario s) => await f.WithDbAsync(db => db.Subject.Where(x => x.SubjectId == s.SubjectId).Select(x => x.Abbreviation).SingleAsync());

    private async Task<(int VersionId, ApiResult Res)> ImportAsync(Setup s, byte[] file)
    {
        var res = await s.VpClient.PostFileAsync("/api/scd/timetable/import", "file", file, "tkb.xlsx");
        Assert.Equal(200, res.Status);
        return (res.Body!["versionId"]?.GetValue<int>() ?? 0, res);
    }

    /// <summary>Phiên bản nháp: Toán(môn của tổ) do giáo viên A dạy lớp 1 thứ 2 tiết 1 và tiết 2, giáo viên B dạy lớp 2 thứ 2 tiết 1.</summary>
    private async Task<(Setup S, int VersionId, string Subj, string TA, string TB)> DraftAsync()
    {
        var s = await NewSetupAsync();
        var subj = await SubjAbbr(s.S); var ta = await AbbrOf(s.S.Absent); var tb = await AbbrOf(s.S.Sub);
        var file = Fill(await TemplateAsync(s), (0, 2, 0, $"{subj}-{ta}"), (0, 2, 1, $"{subj}-{ta}"), (1, 2, 0, $"{subj}-{tb}"));
        var (v, res) = await ImportAsync(s, file);
        Assert.True(res.Body!["accepted"]!.GetValue<bool>(), res.Raw);
        return (s, v, subj, ta, tb);
    }

    private async Task<int> SlotId(int morningNo) => await f.WithDbAsync(db => db.PeriodSlot.Where(p => p.SessionOfDay == "Morning" && p.PeriodNo == morningNo).Select(p => p.PeriodSlotId).SingleAsync());

    // ------------------------------------------------------------------ Mẫu và import

    [Fact]
    public async Task TTB_01_And_02_Template_is_an_xlsx_and_has_preconditions()
    {
        var s = await NewSetupAsync();
        var (status, bytes, type) = await s.VpClient.GetBytesAsync($"/api/scd/timetable/template?campusId={s.CampusId}&semesterId={s.SemesterId}");
        Assert.Equal(200, status);
        Assert.Contains("spreadsheet", type);
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheet("01_TKB");
        Assert.Equal(s.Class1Name, ws.Cell(TimetableLayout.HeaderRow, TimetableLayout.FirstClassColumn).GetString());
        Assert.Equal(s.Class2Name, ws.Cell(TimetableLayout.HeaderRow, TimetableLayout.FirstClassColumn + 1).GetString());
        // TTB-02: phân hiệu chưa có lớp
        var empty = await f.WithDbAsync(async db =>
        {
            var c = new Campus { Code = "TE" + U(), Name = "Phân hiệu trống TKB " + U(), IsActive = true, CreatedAt = f.Clock.Now };
            db.Campus.Add(c);
            await db.SaveChangesAsync();
            db.CampusLeadership.Add(new CampusLeadership { CampusId = c.CampusId, StaffId = s.Vice.PersonId, StartDate = new DateOnly(2026, 9, 5), CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
            return c.CampusId;
        });
        var r = await s.VpClient.GetAsync($"/api/scd/timetable/template?campusId={empty}&semesterId={s.SemesterId}");
        Assert.Equal(400, r.Status);
        Assert.Contains("chưa có lớp nào trong năm học", r.Message);
    }

    [Fact]
    public async Task TTB_03_A_valid_file_creates_a_draft_version_without_sessions()
    {
        var (s, v, _, _, _) = await DraftAsync();
        var list = await s.VpClient.GetAsync($"/api/scd/timetable/versions?campusId={s.CampusId}&semesterId={s.SemesterId}");
        Assert.Equal(200, list.Status);
        var row = list.Body!.AsArray().Single(x => x!["versionId"]!.GetValue<int>() == v)!;
        Assert.Equal("Draft", row["status"]!.GetValue<string>());
        Assert.Equal(3, row["entryCount"]!.GetValue<int>());
        var grid = await s.VpClient.GetAsync($"/api/scd/timetable/versions/{v}");
        Assert.Equal(200, grid.Status);
        Assert.Equal(2, grid.Body!["classes"]!.AsArray().Count);
        Assert.Equal(0, await f.WithDbAsync(db => db.TeachingSession.CountAsync(x => x.ClassId == s.Class1 || x.ClassId == s.Class2)));
    }

    [Fact]
    public async Task TTB_04_An_import_with_errors_creates_no_version()
    {
        var s = await NewSetupAsync();
        var subj = await SubjAbbr(s.S);
        var file = Fill(await TemplateAsync(s), (0, 2, 0, $"{subj}-KHONGCO"), (1, 2, 0, "SAIMON-XYZ"));
        var res = await s.VpClient.PostFileAsync("/api/scd/timetable/import", "file", file, "tkb.xlsx");
        Assert.Equal(200, res.Status);
        Assert.False(res.Body!["accepted"]!.GetValue<bool>());
        Assert.True(res.Body["totalErrors"]!.GetValue<int>() >= 2);
        Assert.Null(res.Body["versionId"]);
        var list = await s.VpClient.GetAsync($"/api/scd/timetable/versions?campusId={s.CampusId}&semesterId={s.SemesterId}");
        Assert.Empty(list.Body!.AsArray());
    }

    [Fact]
    public async Task TTB_05_File_format_errors()
    {
        var s = await NewSetupAsync();
        var junk = await s.VpClient.PostFileAsync("/api/scd/timetable/import", "file", [1, 2, 3, 4], "x.xlsx");
        Assert.Equal(400, junk.Status);
        Assert.StartsWith("Không đọc được file", junk.Message);
        using var wb = new XLWorkbook();
        wb.AddWorksheet("Sheet1").Cell(1, 1).Value = "x";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var noSheet = await s.VpClient.PostFileAsync("/api/scd/timetable/import", "file", ms.ToArray(), "x.xlsx");
        Assert.Equal(200, noSheet.Status);   // sheet đầu tiên được đọc, các dòng sai bị báo lỗi
        Assert.False(noSheet.Body!["accepted"]!.GetValue<bool>());
    }

    [Fact]
    public async Task TTB_06_A_teacher_clash_inside_the_file_is_rejected_at_import()
    {
        var s = await NewSetupAsync();
        var subj = await SubjAbbr(s.S); var ta = await AbbrOf(s.S.Absent);
        var res = await s.VpClient.PostFileAsync("/api/scd/timetable/import", "file", Fill(await TemplateAsync(s), (0, 2, 0, $"{subj}-{ta}"), (1, 2, 0, $"{subj}-{ta}")), "tkb.xlsx");
        Assert.Equal(200, res.Status);
        Assert.False(res.Body!["accepted"]!.GetValue<bool>());
        Assert.Contains("Một giáo viên chỉ dạy một lớp mỗi tiết.", res.Raw);
        Assert.Empty((await s.VpClient.GetAsync($"/api/scd/timetable/versions?campusId={s.CampusId}&semesterId={s.SemesterId}")).Body!.AsArray());
    }

    [Fact]
    public async Task TTB_13_And_14_A_clash_with_a_published_lesson_is_shown_by_preview_and_blocks_publishing()
    {
        // phân hiệu 1: giáo viên A dạy thứ 2 tiết 1 đã phát hành
        var (s1, v1, _, ta, _) = await DraftAsync();
        Assert.Equal(200, (await s1.VpClient.PostAsync($"/api/scd/timetable/versions/{v1}/publish", new { confirmWarnings = false })).Status);
        // phân hiệu 2: cùng giáo viên A, cùng thứ 2 tiết 1
        var s2 = await NewSetupAsync();
        var subj = await SubjAbbr(s1.S);
        var (v2, res) = await ImportAsync(s2, Fill(await TemplateAsync(s2), (0, 2, 0, $"{subj}-{ta}")));
        Assert.True(res.Body!["accepted"]!.GetValue<bool>(), res.Raw);
        var preview = await s2.VpClient.GetAsync($"/api/scd/timetable/versions/{v2}/preview");
        Assert.Equal(200, preview.Status);
        Assert.NotEmpty(preview.Body!["conflicts"]!.AsArray());
        Assert.False(preview.Body["canPublish"]!.GetValue<bool>());
        var publish = await s2.VpClient.PostAsync($"/api/scd/timetable/versions/{v2}/publish", new { confirmWarnings = false });
        Assert.Equal(400, publish.Status);
        Assert.Contains("xung đột giáo viên. Xung đột chặn Phát hành", publish.Message);
        Assert.Equal("Draft", await f.WithDbAsync(db => db.TimetableVersion.Where(x => x.VersionId == v2).Select(x => x.Status).SingleAsync()));
        Assert.Equal(0, await f.WithDbAsync(db => db.TeachingSession.CountAsync(x => x.ClassId == s2.Class1 || x.ClassId == s2.Class2)));
    }

    // ------------------------------------------------------------------ Sửa bản nháp

    [Fact]
    public async Task TTB_07_And_08_Move_an_entry_and_its_validations()
    {
        var (s, v, _, _, _) = await DraftAsync();
        var p1 = await SlotId(1); var p2 = await SlotId(2); var p3 = await SlotId(3);
        var url = $"/api/scd/timetable/versions/{v}/entries/move";
        Assert.Equal(200, (await s.VpClient.PostAsync(url, new { classId = s.Class1, fromDayOfWeek = 2, fromPeriodSlotId = p2, toDayOfWeek = 3, toPeriodSlotId = p3 })).Status);
        Assert.Equal("Ô nguồn không có tiết.", (await s.VpClient.PostAsync(url, new { classId = s.Class1, fromDayOfWeek = 4, fromPeriodSlotId = p1, toDayOfWeek = 5, toPeriodSlotId = p1 })).Message);
        // thả vào ô đã có tiết thì hai tiết đổi chỗ cho nhau (không báo lỗi)
        var swap = await s.VpClient.PostAsync(url, new { classId = s.Class1, fromDayOfWeek = 2, fromPeriodSlotId = p1, toDayOfWeek = 3, toPeriodSlotId = p3 });
        Assert.Equal(200, swap.Status);
        foreach (var day in new[] { 1, 8 })
            Assert.Equal("Thứ không hợp lệ.", (await s.VpClient.PostAsync(url, new { classId = s.Class1, fromDayOfWeek = 2, fromPeriodSlotId = p1, toDayOfWeek = day, toPeriodSlotId = p1 })).Message);
        Assert.StartsWith("Lớp này không có trong phiên bản", (await s.VpClient.PostAsync(url, new { classId = 987654, fromDayOfWeek = 2, fromPeriodSlotId = p1, toDayOfWeek = 3, toPeriodSlotId = p1 })).Message);
    }

    [Fact]
    public async Task TTB_09_And_10_Add_an_entry_and_change_its_teacher_with_eligibility()
    {
        var (s, v, subj, ta, tb) = await DraftAsync();
        var p1 = await SlotId(1); var p4 = await SlotId(4);
        var options = await s.VpClient.GetAsync($"/api/scd/timetable/draft-options?campusId={s.CampusId}");
        Assert.Equal(200, options.Status);
        var add = await s.VpClient.PostAsync($"/api/scd/timetable/versions/{v}/entries", new { classId = s.Class2, dayOfWeek = 3, periodSlotId = p4, subjectId = s.S.SubjectId, teacherId = s.S.Absent.PersonId });
        Assert.Equal(200, add.Status);
        // TTB-10: giáo viên chưa có viết tắt
        var noAbbr = await w.NewAccountAsync("GV không viết tắt " + U(), "Staff", "Teacher", "Active");
        var r1 = await s.VpClient.PutAsync($"/api/scd/timetable/versions/{v}/entries/teacher", new { classId = s.Class1, dayOfWeek = 2, periodSlotId = p1, teacherId = noAbbr.PersonId });
        Assert.True(r1.Status == 400, "r1: " + r1.Raw);
        Assert.StartsWith("Giáo viên chưa có viết tắt", r1.Message);
        // giáo viên có tài khoản bị khóa không đủ điều kiện
        var locked = await w.NewStaffAsync("GV khóa TKB " + U(), "Teacher", s.CampusId, s.S.DepartmentId);
        await f.WithDbAsync(async db => { (await db.Account.SingleAsync(a => a.PersonId == locked.PersonId)).Status = "Locked"; await db.SaveChangesAsync(); });
        var r2 = await s.VpClient.PutAsync($"/api/scd/timetable/versions/{v}/entries/teacher", new { classId = s.Class1, dayOfWeek = 2, periodSlotId = p1, teacherId = locked.PersonId });
        Assert.True(r2.Status == 400, "r2: " + r2.Raw);
        Assert.Contains("không đủ điều kiện dạy:", r2.Message);
        // đổi sang giáo viên hợp lệ: bản nháp cho phép trùng, chỉ báo ở xem trước
        var ok = await s.VpClient.PutAsync($"/api/scd/timetable/versions/{v}/entries/teacher", new { classId = s.Class1, dayOfWeek = 2, periodSlotId = p1, teacherId = s.S.Sub.PersonId });
        Assert.Equal(200, ok.Status);
        Assert.Equal(tb.Length > 0 && ta.Length > 0 && subj.Length > 0, true);
    }

    [Fact]
    public async Task TTB_11_Remove_reset_and_delete_a_draft()
    {
        var (s, v, _, _, _) = await DraftAsync();
        var p1 = await SlotId(1);
        Assert.Equal(200, (await s.VpClient.DeleteAsync($"/api/scd/timetable/versions/{v}/entries?classId={s.Class1}&dayOfWeek=2&periodSlotId={p1}")).Status);
        var reset = await s.VpClient.PostAsync($"/api/scd/timetable/versions/{v}/reset");
        Assert.Equal(200, reset.Status);
        var cells = reset.Body!["rows"]!.AsArray().SelectMany(r => r!["cells"]!.AsArray()).Count(c => c != null);
        Assert.Equal(3, cells);   // nội dung gốc của file import được khôi phục
        Assert.Equal(204, (await s.VpClient.DeleteAsync($"/api/scd/timetable/versions/{v}")).Status);
        Assert.False(await f.WithDbAsync(db => db.TimetableVersion.AnyAsync(x => x.VersionId == v)));
    }

    // ------------------------------------------------------------------ Phát hành

    private async Task<(Setup S, int VersionId)> PublishedAsync()
    {
        var (s, v, _, _, _) = await DraftAsync();
        var r = await s.VpClient.PostAsync($"/api/scd/timetable/versions/{v}/publish", new { confirmWarnings = false });
        Assert.Equal(200, r.Status);
        return (s, v);
    }

    [Fact]
    public async Task TTB_16_And_18_Publish_generates_sessions_on_school_days_notifies_and_audits()
    {
        var (s, v, _, _, _) = await DraftAsync();
        var student = (await w.NewStudentAsync(s.Class1, "HS TKB " + U())).Who;
        var res = await s.VpClient.PostAsync($"/api/scd/timetable/versions/{v}/publish", new { confirmWarnings = false });
        Assert.Equal(200, res.Status);
        Assert.True(res.Body!["createdCount"]!.GetValue<int>() > 0);
        var sessions = await f.WithDbAsync(db => db.TeachingSession.Where(x => x.ClassId == s.Class1 || x.ClassId == s.Class2).ToListAsync());
        Assert.NotEmpty(sessions);
        Assert.All(sessions, x => Assert.Equal("Scheduled", x.Status));
        Assert.All(sessions, x => Assert.NotEqual(DayOfWeek.Sunday, x.SessionDate.DayOfWeek));
        Assert.All(sessions, x => Assert.Equal(DayOfWeek.Monday, x.SessionDate.DayOfWeek));   // chỉ nhập thứ 2
        Assert.All(sessions, x => Assert.True(x.SessionDate >= f.Clock.Today));
        Assert.Equal("Published", await f.WithDbAsync(db => db.TimetableVersion.Where(x => x.VersionId == v).Select(x => x.Status).SingleAsync()));
        // TTB-18: thông báo và nhật ký
        Assert.True(await f.WithDbAsync(db => db.Notification.CountAsync(n => n.RecipientPersonId == s.S.Absent.PersonId && n.NotificationType == "TimetablePublished")) >= 1);
        Assert.True(await f.WithDbAsync(db => db.Notification.CountAsync(n => n.RecipientPersonId == student.PersonId && n.NotificationType == "TimetablePublished")) >= 1);
        Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Category == "Phát hành thời khóa biểu" && a.Action == "Phát hành thời khóa biểu" && a.ActorPersonId == s.Vice.PersonId)) >= 1);
        // TTB-12: bản đã phát hành không sửa được và không phát hành lại
        var p1 = await SlotId(1); var p3 = await SlotId(3);
        var edit = await s.VpClient.PostAsync($"/api/scd/timetable/versions/{v}/entries/move", new { classId = s.Class1, fromDayOfWeek = 2, fromPeriodSlotId = p1, toDayOfWeek = 3, toPeriodSlotId = p3 });
        Assert.Equal(400, edit.Status);
        Assert.StartsWith("Phiên bản này đã phát hành nên không sửa được", edit.Message);
        var again = await s.VpClient.PostAsync($"/api/scd/timetable/versions/{v}/publish", new { confirmWarnings = false });
        Assert.Equal(400, again.Status);
        Assert.StartsWith("Phiên bản này đã phát hành.", again.Message);
        var del = await s.VpClient.DeleteAsync($"/api/scd/timetable/versions/{v}");
        Assert.Equal(400, del.Status);
        Assert.StartsWith("Phiên bản đã phát hành là hồ sơ của lịch đang chạy nên không xóa được.", del.Message);
    }

    [Fact]
    public async Task TTB_15_Publishing_needs_confirmation_when_it_deletes_manual_work()
    {
        var (s, v, _, _, _) = await DraftAsync();
        var manual = await w.NewSessionAsync(s.Class1, s.S.SubjectId, s.S.Absent.PersonId, w.NextMonday().AddDays(1), 1, "Pending");
        await f.WithDbAsync(async db => { (await db.TeachingSession.SingleAsync(x => x.SessionId == manual)).Source = "Manual"; await db.SaveChangesAsync(); });
        var noConfirm = await s.VpClient.PostAsync($"/api/scd/timetable/versions/{v}/publish", new { confirmWarnings = false });
        Assert.Equal(400, noConfirm.Status);
        Assert.StartsWith("Phát hành sẽ xóa tiết Chỉnh tay, tiết bù hoặc tiết Chờ xử lý.", noConfirm.Message);
        Assert.Equal(200, (await s.VpClient.PostAsync($"/api/scd/timetable/versions/{v}/publish", new { confirmWarnings = true })).Status);
        Assert.False(await f.WithDbAsync(db => db.TeachingSession.AnyAsync(x => x.SessionId == manual)));
    }

    // ------------------------------------------------------------------ Màn xem lịch

    [Fact]
    public async Task TTB_22_To_23_Teacher_student_and_parent_views()
    {
        var (s, _) = await PublishedAsync();
        var (student, _) = await w.NewStudentAsync(s.Class1, "HS lịch " + U());
        var parent = await w.NewAccountAsync("PH lịch " + U(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db => { db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = student.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
        var from = World.Iso(f.Clock.Today); var to = World.Iso(f.Clock.Today.AddDays(30));
        var teacher = await f.LoginAsync(s.S.Absent.Username);
        var mine = await teacher.GetAsync($"/api/scd/schedule/mine?from={from}&to={to}");
        Assert.Equal(200, mine.Status);
        Assert.NotEmpty(mine.Body!.AsArray());
        var st = await f.LoginAsync(student.Username);
        Assert.Equal(200, (await st.GetAsync($"/api/scd/schedule/student?from={from}&to={to}")).Status);
        Assert.Equal(200, (await st.GetAsync("/api/scd/schedule/student/class")).Status);
        var pc = await f.LoginAsync(parent.Username);
        Assert.Equal(200, (await pc.GetAsync($"/api/scd/schedule/child?studentId={student.PersonId}&from={from}&to={to}")).Status);
        var stranger = await w.NewAccountAsync("PH lạ lịch " + U(), "Parent", "Parent", "Active");
        var denied = await (await f.LoginAsync(stranger.Username)).GetAsync($"/api/scd/schedule/child?studentId={student.PersonId}&from={from}&to={to}");
        Assert.Equal(403, denied.Status);
    }

    [Fact]
    public async Task TTB_24_And_27_Management_views_follow_the_led_campus_and_the_principal_reads_everything()
    {
        var (s, v) = await PublishedAsync();
        var day = World.Iso(w.NextMonday());
        // Hiệu phó A (phân hiệu khác) bị từ chối với dữ liệu của phân hiệu của s
        var viceOther = await f.LoginAsync(w.ViceA.Username);
        var denied = await viceOther.GetAsync($"/api/scd/schedule/day?campusId={s.CampusId}&date={day}");
        Assert.True(denied.Status is 403 or 404, denied.Raw);
        var principal = await f.LoginAsync(w.Principal.Username);
        Assert.Equal(200, (await principal.GetAsync($"/api/scd/schedule/day?campusId={s.CampusId}&date={day}")).Status);
        // TTB-27
        Assert.Equal(200, (await principal.GetAsync("/api/scd/timetable/campuses")).Status);
        Assert.Equal(200, (await principal.GetAsync($"/api/scd/timetable/versions?campusId={s.CampusId}&semesterId={s.SemesterId}")).Status);
        Assert.Equal(200, (await principal.GetAsync($"/api/scd/timetable/versions/{v}")).Status);
        Assert.Equal(200, (await principal.GetAsync("/api/scd/responsibilities")).Status);
        Assert.Equal(403, (await principal.GetAsync($"/api/scd/timetable/template?campusId={s.CampusId}&semesterId={s.SemesterId}")).Status);
        Assert.Equal(403, (await principal.GetAsync($"/api/scd/timetable/versions/{v}/preview")).Status);
    }

    [Fact]
    public async Task TTB_25_Date_range_rules()
    {
        var (s, _) = await PublishedAsync();
        var vice = s.VpClient;
        var d = f.Clock.Today;
        var back = await vice.GetAsync($"/api/scd/schedule/teacher?teacherId={s.S.Absent.PersonId}&from={World.Iso(d.AddDays(5))}&to={World.Iso(d)}");
        Assert.Equal(400, back.Status);
        Assert.Equal("Ngày kết thúc phải sau ngày bắt đầu.", back.Message);
        Assert.Equal(200, (await vice.GetAsync($"/api/scd/schedule/teacher?teacherId={s.S.Absent.PersonId}&from={World.Iso(d)}&to={World.Iso(d.AddDays(62))}")).Status);
        var tooLong = await vice.GetAsync($"/api/scd/schedule/teacher?teacherId={s.S.Absent.PersonId}&from={World.Iso(d)}&to={World.Iso(d.AddDays(63))}");
        Assert.Equal(400, tooLong.Status);
        Assert.StartsWith("Chỉ xem tối đa 62 ngày mỗi lần", tooLong.Message);
    }

    [Fact]
    public async Task TTB_26_Role_matrix_for_the_timetable_write_endpoints()
    {
        var s = await NewSetupAsync();
        var roles = await f.LoginAllAsync();
        foreach (var role in new[] { Admin, Principal, Teacher, Student, Parent })
        {
            Assert.Equal(403, (await roles[role].GetAsync($"/api/scd/timetable/template?campusId={s.CampusId}&semesterId={s.SemesterId}")).Status);
            Assert.Equal(403, (await roles[role].PostFileAsync("/api/scd/timetable/import", "file", [1], "x.xlsx")).Status);
            Assert.Equal(403, (await roles[role].PostAsync("/api/scd/timetable/versions/1/publish", new { confirmWarnings = false })).Status);
        }
        // Hiệu phó của phân hiệu khác không tải được mẫu của phân hiệu này
        var other = await roles[Vice].GetAsync($"/api/scd/timetable/template?campusId={s.CampusId}&semesterId={s.SemesterId}");
        Assert.Equal(403, other.Status);
        Assert.StartsWith("Chỉ hiệu phó đang phụ trách phân hiệu này mới thao tác được", other.Message);
    }

    [Fact]
    public async Task TTB_12_Published_versions_are_immutable()
    {
        var (s, v) = await PublishedAsync();
        var p1 = await SlotId(1); var p3 = await SlotId(3);
        Assert.StartsWith("Phiên bản này đã phát hành nên không sửa được", (await s.VpClient.PostAsync($"/api/scd/timetable/versions/{v}/entries/move", new { classId = s.Class1, fromDayOfWeek = 2, fromPeriodSlotId = p1, toDayOfWeek = 3, toPeriodSlotId = p3 })).Message);
        Assert.StartsWith("Phiên bản này đã phát hành nên không sửa được", (await s.VpClient.PostAsync($"/api/scd/timetable/versions/{v}/entries", new { classId = s.Class1, dayOfWeek = 3, periodSlotId = p3, subjectId = s.S.SubjectId, teacherId = s.S.Absent.PersonId })).Message);
        Assert.StartsWith("Phiên bản này đã phát hành.", (await s.VpClient.PostAsync($"/api/scd/timetable/versions/{v}/publish", new { confirmWarnings = false })).Message);
        Assert.StartsWith("Phiên bản này đã phát hành.", (await s.VpClient.GetAsync($"/api/scd/timetable/versions/{v}/preview")).Message);
    }

    [Fact]
    public async Task TTB_20_A_draft_gives_no_subject_responsibility_and_publishing_does()
    {
        var (s, v, _, _, _) = await DraftAsync();
        var teacher = await f.LoginAsync(s.S.Absent.Username);
        var before = await teacher.GetAsync("/api/lrn/assignments/class-subjects");
        Assert.Equal(200, before.Status);
        Assert.DoesNotContain($"\"classId\":{s.Class1},", before.Raw);
        Assert.Equal(200, (await s.VpClient.PostAsync($"/api/scd/timetable/versions/{v}/publish", new { confirmWarnings = false })).Status);
        var after = await teacher.GetAsync("/api/lrn/assignments/class-subjects");
        Assert.Contains($"\"classId\":{s.Class1},", after.Raw);
    }
}
