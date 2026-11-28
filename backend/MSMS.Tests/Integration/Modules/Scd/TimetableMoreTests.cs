using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Scd.Services;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Scd;

/// <summary>SCD-01 tiếp: phát hành lại giữa kỳ (TTB-19, TTB-21, E2E-11), học kỳ đã kết thúc (TTB-17), lịch học theo ngày nghỉ của từng phân hiệu (STR-26).</summary>
public partial class TimetableTests
{
    private Task<ApiResult> PublishAsync(Setup s, int version, bool confirm = false) =>
        s.VpClient.PostAsync($"/api/scd/timetable/versions/{version}/publish", new { confirmWarnings = confirm });

    [Fact]
    public async Task TTB_17_Publishing_after_the_semester_ended_is_refused()
    {
        var s = await NewSetupAsync(semesterNo: 2);
        var subj = await SubjAbbr(s.S); var ta = await AbbrOf(s.S.Absent);
        var (v, _) = await ImportAsync(s, Fill(await TemplateAsync(s), (0, 2, 0, $"{subj}-{ta}")));
        var original = await f.WithDbAsync(db => db.Semester.Where(x => x.SemesterId == s.SemesterId).Select(x => x.EndDate).SingleAsync());
        try
        {
            await f.WithDbAsync(async db => { (await db.Semester.SingleAsync(x => x.SemesterId == s.SemesterId)).EndDate = f.Clock.Today.AddDays(-1); await db.SaveChangesAsync(); });
            var r = await PublishAsync(s, v);
            Assert.Equal(400, r.Status);
            Assert.EndsWith("đã kết thúc, không còn ngày nào để sinh tiết.", r.Message);
        }
        finally { await f.WithDbAsync(async db => { (await db.Semester.SingleAsync(x => x.SemesterId == s.SemesterId)).EndDate = original; await db.SaveChangesAsync(); }); }
        Assert.Equal("Draft", await f.WithDbAsync(db => db.TimetableVersion.Where(x => x.VersionId == v).Select(x => x.Status).SingleAsync()));
    }

    [Fact]
    public async Task TTB_21_And_E2E_11_Republishing_mid_semester_keeps_past_sessions_and_attendance()
    {
        var s = await NewSetupAsync();
        var subj = await SubjAbbr(s.S); var ta = await AbbrOf(s.S.Absent); var tb = await AbbrOf(s.S.Sub);
        var template = await TemplateAsync(s);
        var (pupil, _) = await w.NewStudentAsync(s.Class1, "HS phát hành lại " + U());
        var past = w.NextMonday().AddDays(-14);
        // V1 được phát hành từ hai tuần trước: các tiết thứ Hai tuần đó đã kết thúc
        var (v1, _) = await ImportAsync(s, Fill(template, (0, 2, 0, $"{subj}-{ta}"), (0, 2, 1, $"{subj}-{ta}"), (1, 2, 0, $"{subj}-{tb}")));
        f.Clock.Set(past.AddDays(-1).ToDateTime(new TimeOnly(6, 0)));   // tiết đầu tiên được sinh từ ngày hôm sau
        try { Assert.Equal(200, (await PublishAsync(s, v1)).Status); }
        finally { f.Clock.Reset(); }
        var pastSession = await f.WithDbAsync(db => db.TeachingSession.Where(x => x.ClassId == s.Class1 && x.SessionDate == past).OrderBy(x => x.PeriodSlotId).FirstAsync());
        Assert.Equal(s.S.Absent.PersonId, pastSession.TeacherId);
        await f.WithDbAsync(async db => { db.AttendanceRecord.Add(new AttendanceRecord { SessionId = pastSession.SessionId, StudentId = pupil.PersonId, Status = "Present", RecordedByPersonId = s.S.Absent.PersonId, RecordedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
        var pastBefore = await f.WithDbAsync(db => db.TeachingSession.CountAsync(x => (x.ClassId == s.Class1 || x.ClassId == s.Class2) && x.SessionDate < f.Clock.Today));
        // tiết bù thủ công ở tương lai làm bản phát hành mới phải xác nhận
        var manual = await w.NewSessionAsync(s.Class1, s.S.SubjectId, s.S.Absent.PersonId, w.NextMonday().AddDays(1), 3, "Pending");
        await f.WithDbAsync(async db => { (await db.TeachingSession.SingleAsync(x => x.SessionId == manual)).Source = "Manual"; await db.SaveChangesAsync(); });
        // V2: đổi giáo viên của lớp 1 (hai lớp cùng khung giờ nên đổi chéo)
        var (v2, _) = await ImportAsync(s, Fill(template, (0, 2, 0, $"{subj}-{tb}"), (0, 2, 1, $"{subj}-{tb}"), (1, 2, 0, $"{subj}-{ta}")));
        var preview = await s.VpClient.GetAsync($"/api/scd/timetable/versions/{v2}/preview");
        Assert.Equal(200, preview.Status);
        var noConfirm = await PublishAsync(s, v2);
        Assert.Equal(400, noConfirm.Status);
        Assert.StartsWith("Phát hành sẽ xóa tiết Chỉnh tay, tiết bù hoặc tiết Chờ xử lý.", noConfirm.Message);
        Assert.Equal(200, (await PublishAsync(s, v2, confirm: true)).Status);
        // tiết đã qua và điểm danh còn nguyên
        var again = await f.WithDbAsync(db => db.TeachingSession.AsNoTracking().SingleAsync(x => x.SessionId == pastSession.SessionId));
        Assert.Equal(s.S.Absent.PersonId, again.TeacherId);
        Assert.Equal(1, await f.WithDbAsync(db => db.AttendanceRecord.CountAsync(a => a.SessionId == pastSession.SessionId)));
        Assert.Equal(pastBefore, await f.WithDbAsync(db => db.TeachingSession.CountAsync(x => (x.ClassId == s.Class1 || x.ClassId == s.Class2) && x.SessionDate < f.Clock.Today)));
        // tiết tương lai theo V2; tiết bù thủ công đã bị xóa
        var future = await f.WithDbAsync(db => db.TeachingSession.Where(x => x.ClassId == s.Class1 && x.SessionDate >= f.Clock.Today).ToListAsync());
        Assert.NotEmpty(future);
        Assert.All(future, x => Assert.Equal(s.S.Sub.PersonId, x.TeacherId));
        Assert.False(await f.WithDbAsync(db => db.TeachingSession.AnyAsync(x => x.SessionId == manual)));
        // trách nhiệm môn chuyển theo V2
        var teacherA = await f.LoginAsync(s.S.Absent.Username);
        var teacherB = await f.LoginAsync(s.S.Sub.Username);
        Assert.DoesNotContain($"\"classId\":{s.Class1},", (await teacherA.GetAsync("/api/lrn/assignments/class-subjects")).Raw);
        Assert.Contains($"\"classId\":{s.Class1},", (await teacherB.GetAsync("/api/lrn/assignments/class-subjects")).Raw);
        Assert.True(await f.WithDbAsync(db => db.AuditLog.CountAsync(a => a.Category == "Phát hành thời khóa biểu" && a.ActorPersonId == s.Vice.PersonId)) >= 2);
    }

    [Fact]
    public async Task TTB_19_A_partial_publish_replaces_only_the_classes_in_the_file()
    {
        var s = await NewSetupAsync();
        var subj = await SubjAbbr(s.S); var ta = await AbbrOf(s.S.Absent); var tb = await AbbrOf(s.S.Sub);
        var template = await TemplateAsync(s);
        var (v1, _) = await ImportAsync(s, Fill(template, (0, 2, 0, $"{subj}-{ta}"), (1, 2, 0, $"{subj}-{tb}")));
        Assert.Equal(200, (await PublishAsync(s, v1)).Status);
        // V2 chỉ gồm lớp 1, chuyển sang tiết thứ 3
        var onlyClass1 = Fill(template, (0, 2, 2, $"{subj}-{ta}"));
        using (var ms = new MemoryStream(onlyClass1)) using (var wb = new ClosedXML.Excel.XLWorkbook(ms))
        {
            wb.Worksheet("01_TKB").Column(TimetableLayout.FirstClassColumn + 1).Delete();   // file chỉ còn cột lớp 1
            using var o = new MemoryStream(); wb.SaveAs(o); onlyClass1 = o.ToArray();
        }
        var (v2, _) = await ImportAsync(s, onlyClass1);
        Assert.Equal(200, (await PublishAsync(s, v2)).Status);
        var slot1 = await SlotId(1); var slot3 = await SlotId(3);
        var c1 = await f.WithDbAsync(db => db.TeachingSession.Where(x => x.ClassId == s.Class1 && x.SessionDate >= f.Clock.Today).Select(x => x.PeriodSlotId).Distinct().ToListAsync());
        var c2 = await f.WithDbAsync(db => db.TeachingSession.Where(x => x.ClassId == s.Class2 && x.SessionDate >= f.Clock.Today).Select(x => new { x.PeriodSlotId, x.TeacherId }).Distinct().ToListAsync());
        Assert.Equal([slot3], c1);                       // lớp 1 theo V2
        Assert.Single(c2);                               // lớp 2 vẫn theo V1
        Assert.Equal(slot1, c2[0].PeriodSlotId);
        Assert.Equal(s.S.Sub.PersonId, c2[0].TeacherId);
    }

    [Fact]
    public async Task STR_26_The_school_day_calendar_decides_the_teaching_days_per_campus()
    {
        var a = await NewSetupAsync();
        var b = await NewSetupAsync();
        var wednesday = w.NextMonday().AddDays(2);
        await f.WithDbAsync(async db =>
        {
            db.Holiday.Add(new Holiday { AcademicYearId = w.YearId, CampusId = a.CampusId, HolidayDate = wednesday, Reason = "Nghỉ riêng phân hiệu", CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        foreach (var x in new[] { a, b })
        {
            var subj = await SubjAbbr(x.S); var ta = await AbbrOf(x.S.Absent);
            var cells = Enumerable.Range(2, 6).Select(d => (0, d, 0, $"{subj}-{ta}")).ToArray();   // thứ Hai đến thứ Bảy
            var (v, _) = await ImportAsync(x, Fill(await TemplateAsync(x), cells));
            Assert.Equal(200, (await PublishAsync(x, v)).Status);
        }
        var sessA = await f.WithDbAsync(db => db.TeachingSession.Where(x => x.ClassId == a.Class1).ToListAsync());
        var sessB = await f.WithDbAsync(db => db.TeachingSession.Where(x => x.ClassId == b.Class1).ToListAsync());
        Assert.DoesNotContain(sessA, x => x.SessionDate == wednesday && x.Status != "Cancelled");   // ngày nghỉ của phân hiệu A không có tiết
        Assert.Contains(sessB, x => x.SessionDate == wednesday && x.Status == "Scheduled");          // phân hiệu B vẫn học
        Assert.DoesNotContain(sessA.Concat(sessB), x => x.SessionDate.DayOfWeek == DayOfWeek.Sunday);
        var sem = await f.WithDbAsync(db => db.Semester.Where(x => x.SemesterId == a.SemesterId).Select(x => new { x.StartDate, x.EndDate }).SingleAsync());
        Assert.All(sessA.Concat(sessB), x => Assert.True(x.SessionDate >= sem.StartDate && x.SessionDate <= sem.EndDate));   // không có tiết ngoài học kỳ
    }
}
