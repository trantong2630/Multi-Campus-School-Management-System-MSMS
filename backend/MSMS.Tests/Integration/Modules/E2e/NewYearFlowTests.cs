using ClosedXML.Excel;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Scd.Services;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.E2e;

/// <summary>E2E-01: thiết lập năm học mới tới buổi học đầu tiên (SCH, PPL, SCD, ATT, ADM). Dùng phân hiệu riêng và năm học tương lai để không dẫm lên dữ liệu khác.</summary>
[Collection("api")]
public class NewYearFlowTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();
    private static int seq;

    [Fact]
    public async Task E2E_01_New_year_setup_to_the_first_lesson()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var principal = await f.LoginAsync(w.Principal.Username);
        // phân hiệu riêng cùng Hiệu phó phụ trách
        var campus = await f.WithDbAsync(async db => { var c = new Campus { Code = "NY" + U(), Name = "Phân hiệu năm mới " + U(), IsActive = true, CreatedAt = f.Clock.Now }; db.Campus.Add(c); await db.SaveChangesAsync(); return c.CampusId; });
        var vice = await w.NewStaffAsync("HP năm mới " + U(), "VicePrincipal", campus, null);
        Assert.Equal(204, (await principal.PostAsync("/api/ppl/responsibilities/assign", new { kind = "campus", targetId = campus, staffId = vice.PersonId, startDate = World.Iso(f.Clock.Today) })).Status);
        var s = await w.NewScenarioAsync();
        var teacher = await w.NewStaffAsync("GV năm mới " + U(), "Teacher", campus, s.DepartmentId);
        var abbr = await f.WithDbAsync(db => db.StaffProfile.Where(p => p.StaffId == teacher.PersonId).Select(p => p.TeacherAbbr!).SingleAsync());
        var subj = await f.WithDbAsync(db => db.Subject.Where(x => x.SubjectId == s.SubjectId).Select(x => x.Abbreviation).SingleAsync());

        // 1. Năm học cũ có lớp và học sinh; năm học mới có hai học kỳ và một ngày nghỉ (SCH)
        var n = Interlocked.Increment(ref seq);
        var oldStart = new DateOnly(2400 + n * 4, 9, 5);
        var (oldYear, newYear) = await f.WithDbAsync(async db =>
        {
            var a = new AcademicYear { Name = $"{oldStart.Year}-{oldStart.Year + 1}", StartDate = oldStart, EndDate = new DateOnly(oldStart.Year + 1, 5, 31), IsCurrent = false };
            db.AcademicYear.Add(a);
            await db.SaveChangesAsync();
            return (a.AcademicYearId, 0);
        });
        var year = await admin.PostAsync("/api/sch/academic-years", new { name = $"{oldStart.Year + 1}-{oldStart.Year + 2}", startDate = $"{oldStart.Year + 1}-09-05", endDate = $"{oldStart.Year + 2}-05-31" });
        Assert.Equal(200, year.Status);
        var yearId = year.Body!["academicYearId"]!.GetValue<int>();
        var sem = await admin.PutAsync($"/api/sch/academic-years/{yearId}/semesters", new { semester1 = new { startDate = $"{oldStart.Year + 1}-09-05", endDate = $"{oldStart.Year + 1}-12-31" }, semester2 = new { startDate = $"{oldStart.Year + 2}-01-01", endDate = $"{oldStart.Year + 2}-05-31" } });
        Assert.True(sem.Ok, sem.Raw);
        var holiday = new DateOnly(oldStart.Year + 1, 9, 9);
        Assert.True((await admin.PostAsync("/api/sch/holidays", new { academicYearId = yearId, campusId = campus, holidayDate = World.Iso(holiday), reason = "Nghỉ lễ" })).Ok);
        // 2. Kết chuyển: tạo lớp mới và chuyển học sinh (SCH)
        var oldClass = await f.WithDbAsync(async db => { var c = new Class { CampusId = campus, AcademicYearId = oldYear, Name = "6A1", GradeLevel = 6, IsActive = true, CreatedAt = f.Clock.Now }; db.Class.Add(c); await db.SaveChangesAsync(); return c.ClassId; });
        var kids = new List<Who>();
        for (var i = 0; i < 3; i++) kids.Add((await w.NewStudentAsync(oldClass, $"HS năm mới {i} {U()}", oldStart)).Who);
        var create = await admin.PostAsync("/api/sch/rollover/classes", new { fromYearId = oldYear, toYearId = yearId, items = new[] { new { sourceClassId = oldClass, name = "7A1", gradeLevel = 7 } } });
        Assert.Equal(200, create.Status);
        var newClass = create.Body!["classes"]![0]!["classId"]!.GetValue<int>();
        Assert.Equal(3, (await admin.PostAsync("/api/sch/rollover/move", new { pairs = new[] { new { sourceClassId = oldClass, targetClassId = newClass } } })).Body!["moved"]!.GetValue<int>());
        // 3. Hiệu trưởng gán chủ nhiệm (PPL): lớp thuộc năm học tương lai nên bản hẹn bắt đầu từ ngày bắt đầu năm không cho phép (ngày hiệu lực không được quá khứ, nhưng được ở tương lai)
        var firstDay = new DateOnly(oldStart.Year + 1, 9, 5);
        Assert.Equal(204, (await principal.PostAsync("/api/ppl/responsibilities/assign", new { kind = "homeroom", targetId = newClass, staffId = teacher.PersonId, startDate = World.Iso(firstDay) })).Status);
        // 4. Hiệu phó nhập thời khóa biểu cho lớp mới, xem trước và phát hành (SCD)
        var vc = await f.LoginAsync(vice.Username);
        var semesterId = await f.WithDbAsync(db => db.Semester.Where(x => x.AcademicYearId == yearId && x.SemesterNo == 1).Select(x => x.SemesterId).SingleAsync());
        var tpl = await vc.GetBytesAsync($"/api/scd/timetable/template?campusId={campus}&semesterId={semesterId}");
        Assert.Equal(200, tpl.Status);
        using var ms = new MemoryStream(tpl.Bytes); using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("01_TKB");
        var rows = 0; while (ws.Cell(TimetableLayout.FirstDataRow + rows, 3).GetString().Length > 0) rows++;
        var perDay = rows / 6;
        string[] names = Enumerable.Range(0, 3).Select(d => $"{subj}-{abbr}").ToArray();
        for (var day = 2; day <= 7; day++) ws.Cell(TimetableLayout.FirstDataRow + (day - 2) * perDay + 0, TimetableLayout.FirstClassColumn).Value = names[0];
        using var o = new MemoryStream(); wb.SaveAs(o);
        var import = await vc.PostFileAsync("/api/scd/timetable/import", "file", o.ToArray(), "tkb.xlsx");
        Assert.Equal(200, import.Status);
        Assert.True(import.Body!["accepted"]!.GetValue<bool>(), import.Raw.Length > 600 ? import.Raw[..600] : import.Raw);
        var versionId = import.Body["versionId"]!.GetValue<int>();
        Assert.Equal(200, (await vc.GetAsync($"/api/scd/timetable/versions/{versionId}/preview")).Status);
        // đồng hồ đặt vào ngày trước buổi học đầu để tiết được sinh từ ngày đầu năm
        f.Clock.Set(firstDay.AddDays(-1).ToDateTime(new TimeOnly(9, 0)));
        try
        {
            var vc2 = await f.LoginAsync(vice.Username);
            var pub = await vc2.PostAsync($"/api/scd/timetable/versions/{versionId}/publish", new { confirmWarnings = false });
            Assert.Equal(200, pub.Status);
            var sessions = await f.WithDbAsync(db => db.TeachingSession.Where(x => x.ClassId == newClass).Select(x => new { x.SessionDate, x.Status, x.TeacherId }).ToListAsync());
            Assert.NotEmpty(sessions);
            Assert.All(sessions, x => Assert.NotEqual(DayOfWeek.Sunday, x.SessionDate.DayOfWeek));
            Assert.DoesNotContain(sessions, x => x.SessionDate == holiday);   // ngày nghỉ không có tiết
            Assert.Contains(sessions, x => x.SessionDate == firstDay);
            // thông báo cho giáo viên, học sinh và nhật ký phát hành
            Assert.True(await f.WithDbAsync(db => db.Notification.CountAsync(x => x.RecipientPersonId == teacher.PersonId && x.NotificationType == "TimetablePublished")) >= 1);
            // hành vi thật: học sinh chỉ được báo khi đã có ghi danh hiệu lực ở ngày phát hành, nên học sinh của năm học chưa bắt đầu chưa nhận thông báo
            Assert.Equal(0, await f.WithDbAsync(db => db.Notification.CountAsync(x => x.RecipientPersonId == kids[0].PersonId && x.NotificationType == "TimetablePublished")));
            Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Action == "Phát hành thời khóa biểu" && a.ActorPersonId == vice.PersonId)) >= 1);
            // 5. Giáo viên và học sinh xem lịch
            f.Clock.Set(firstDay.ToDateTime(new TimeOnly(7, 0)));
            var tc = await f.LoginAsync(teacher.Username);
            var mine = await tc.GetAsync($"/api/scd/schedule/mine?from={World.Iso(firstDay)}&to={World.Iso(firstDay.AddDays(7))}");
            Assert.Equal(200, mine.Status); Assert.NotEmpty(mine.Body!.AsArray());
            var sc = await f.LoginAsync(kids[0].Username);
            Assert.Equal(200, (await sc.GetAsync($"/api/scd/schedule/student?from={World.Iso(firstDay)}&to={World.Iso(firstDay.AddDays(7))}")).Status);
            // 6. Ngày đầu: giáo viên mở bảng điểm danh, danh sách đúng 3 học sinh vừa kết chuyển
            var first = sessions.First(x => x.SessionDate == firstDay);
            var sid = await f.WithDbAsync(db => db.TeachingSession.Where(x => x.ClassId == newClass && x.SessionDate == firstDay).Select(x => x.SessionId).FirstAsync());
            var sheet = await tc.GetAsync($"/api/att/sessions/{sid}");
            Assert.Equal(200, sheet.Status);
            Assert.Equal(3, sheet.Body!["students"]!.AsArray().Count);
            Assert.NotNull(first);
            // 7. Bảng tình hình của Hiệu phó thấy 1 lớp và 3 học sinh
            Assert.True((await (await f.LoginAsync(w.SchoolAdmin.Username)).PutAsync($"/api/sch/academic-years/{yearId}/current", new { })).Ok);   // năm học mới thành hiện hành (tạm thời trong test)
            var dash = await (await f.LoginAsync(vice.Username)).GetAsync($"/api/adm/dashboard?campusId={campus}");
            Assert.Equal(200, dash.Status);
            Assert.Equal(1, dash.Body!["classCount"]!.GetValue<int>());
            Assert.Equal(3, dash.Body["studentCount"]!.GetValue<int>());
        }
        finally
        {
            f.Clock.Reset();
            await admin.PutAsync($"/api/sch/academic-years/{w.YearId}/current", new { });   // trả lại năm học hiện hành cho các test khác
        }
        Assert.NotEqual(0, newYear + yearId);
    }
}
