using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Sch;

/// <summary>SCH-06 kết chuyển năm học (ENR-13..19) và các case ghi danh còn lại (ENR-02, 05, 11, 21, 22). Mỗi test dựng cặp năm học riêng.</summary>
[Collection("api")]
public class RolloverTests(ApiFactory f)
{
    private World w => f.World;
    private async Task<ApiClient> AdminAsync() { await w.EnsureAsync(); f.Clock.Reset(); return await f.LoginAsync(w.SchoolAdmin.Username); }

    private static int yearSeq = 0;

    /// <summary>Cặp năm học nối nhau ở tương lai xa (chưa bắt đầu) hoặc quá khứ xa (đã bắt đầu) để không dẫm lên dữ liệu khác.</summary>
    private async Task<(int From, int To)> YearsAsync(bool toStarted = false)
    {
        var n = Interlocked.Increment(ref yearSeq);
        var baseYear = toStarted ? 1900 + n * 2 : 2200 + n * 2;
        return await f.WithDbAsync(async db =>
        {
            var a = new AcademicYear { Name = $"{baseYear}-{baseYear + 1}", StartDate = new DateOnly(baseYear, 9, 5), EndDate = new DateOnly(baseYear + 1, 5, 31), IsCurrent = false };
            var b = new AcademicYear { Name = $"{baseYear + 1}-{baseYear + 2}", StartDate = new DateOnly(baseYear + 1, 9, 5), EndDate = new DateOnly(baseYear + 2, 5, 31), IsCurrent = false };
            db.AcademicYear.AddRange(a, b);
            await db.SaveChangesAsync();
            return (a.AcademicYearId, b.AcademicYearId);
        });
    }

    private Task<int> ClassInYear(int yearId, string name, byte grade, int? campus = null, bool active = true) => f.WithDbAsync(async db =>
    {
        var c = new Class { CampusId = campus ?? w.CampusAId, AcademicYearId = yearId, Name = name, GradeLevel = grade, IsActive = active, CreatedAt = f.Clock.Now };
        db.Class.Add(c);
        await db.SaveChangesAsync();
        return c.ClassId;
    });

    private Task<DateOnly> YearStart(int yearId) => f.WithDbAsync(db => db.AcademicYear.Where(y => y.AcademicYearId == yearId).Select(y => y.StartDate).SingleAsync());

    [Fact]
    public async Task ENR_02_Bulk_enroll_and_candidate_search()
    {
        var admin = await AdminAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "EN2" + World.Uniq(), 6);
        var tag = "Ứng viên " + World.Uniq();
        var s1 = await w.NewAccountAsync(tag + " một", "Student", "Student", "Active");
        var s2 = await w.NewAccountAsync(tag + " hai", "Student", "Student", "Active");
        var found = await admin.GetAsync($"/api/sch/enrollments/candidates?yearId={w.YearId}&search={Uri.EscapeDataString(tag)}&limit=30");
        Assert.Equal(200, found.Status);
        var ids = found.Body!.AsArray().Select(x => x!["studentId"]!.GetValue<int>()).ToList();
        Assert.Contains(s1.PersonId, ids); Assert.Contains(s2.PersonId, ids);
        var r = await admin.PostAsync($"/api/sch/classes/{cls}/enrollments/bulk", new { studentIds = new[] { s1.PersonId, s2.PersonId }, startDate = World.Iso(f.Clock.Today) });
        Assert.Equal(204, r.Status);
        var after = await admin.GetAsync($"/api/sch/enrollments/candidates?yearId={w.YearId}&search={Uri.EscapeDataString(tag)}");
        Assert.Empty(after.Body!.AsArray());   // đã có lớp thì không còn là ứng viên
        var roster = await admin.GetAsync($"/api/sch/classes/{cls}/roster");
        Assert.Equal(2, roster.Body!["students"]!.AsArray().Count);
    }

    [Fact]
    public async Task ENR_05_Roster_of_a_next_year_class_defaults_to_the_year_start()
    {
        var admin = await AdminAsync();
        var (fromY, toY) = await YearsAsync();
        var cls = await ClassInYear(toY, "EN5" + World.Uniq(), 7);
        var start = await YearStart(toY);
        var (s, _) = await w.NewStudentAsync(cls, "HS năm sau " + World.Uniq(), start);
        var noDate = await admin.GetAsync($"/api/sch/classes/{cls}/roster");
        Assert.Equal(200, noDate.Status);
        Assert.Equal(1, noDate.Body!["students"]!.AsArray().Count);
        Assert.Equal(World.Iso(start), noDate.Body["date"]!.GetValue<string>());
        var withDate = await admin.GetAsync($"/api/sch/classes/{cls}/roster?date={World.Iso(start)}");
        Assert.Equal(1, withDate.Body!["students"]!.AsArray().Count);
        var today = await admin.GetAsync($"/api/sch/classes/{cls}/roster?date={World.Iso(f.Clock.Today)}");
        Assert.Empty(today.Body!["students"]!.AsArray());   // hôm nay chưa có ai vì năm sau chưa bắt đầu
        Assert.NotEqual(0, fromY); Assert.NotEqual(0, s.PersonId);
    }

    [Fact]
    public async Task ENR_11_Bulk_close_is_all_or_nothing()
    {
        var admin = await AdminAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "EN11" + World.Uniq(), 6);
        var (a, ea) = await w.NewStudentAsync(cls, "HS đóng A " + World.Uniq());
        var (b, eb) = await w.NewStudentAsync(cls, "HS đóng B " + World.Uniq());
        var (c, ec) = await w.NewStudentAsync(cls, "HS đóng C " + World.Uniq());
        Assert.Equal(204, (await admin.PostAsync($"/api/sch/enrollments/{ec}/close", new { reason = "SchoolTransfer" })).Status);
        var r = await admin.PostAsync("/api/sch/enrollments/close-bulk", new { enrollmentIds = new[] { ea, eb, ec }, reason = "SchoolTransfer" });
        Assert.Equal(400, r.Status);
        Assert.Equal($"{c.FullName}: đã rời lớp từ trước.", r.Message);
        Assert.Null(await f.WithDbAsync(db => db.Enrollment.Where(e => e.EnrollmentId == ea).Select(e => e.EndDate).SingleAsync()));
        Assert.Equal(204, (await admin.PostAsync("/api/sch/enrollments/close-bulk", new { enrollmentIds = new[] { ea, eb }, reason = "SchoolTransfer" })).Status);
        Assert.NotNull(await f.WithDbAsync(db => db.Enrollment.Where(e => e.EnrollmentId == eb).Select(e => e.EndDate).SingleAsync()));
        Assert.NotEqual(a.PersonId, b.PersonId);
    }

    [Fact]
    public async Task ENR_13_And_14_Rollover_overview_class_creation_and_validations()
    {
        var admin = await AdminAsync();
        var (fromY, toY) = await YearsAsync();
        var c6 = await ClassInYear(fromY, "6A1", 6);
        var c9 = await ClassInYear(fromY, "9A1", 9);
        var ov = await admin.GetAsync($"/api/sch/rollover?fromYearId={fromY}&toYearId={toY}");
        Assert.Equal(200, ov.Status);
        var rows = ov.Body!.AsArray();
        var r6 = rows.Single(x => x!["sourceClassId"]!.GetValue<int>() == c6)!;
        var r9 = rows.Single(x => x!["sourceClassId"]!.GetValue<int>() == c9)!;
        Assert.Equal("7A1", r6["suggestedName"]!.GetValue<string>());
        Assert.Equal(7, r6["suggestedGrade"]!.GetValue<int>());
        Assert.Null(r9["suggestedGrade"]);
        // ENR-13: tạo lớp
        var create = await admin.PostAsync("/api/sch/rollover/classes", new { fromYearId = fromY, toYearId = toY, items = new[] { new { sourceClassId = c6, name = "7A1", gradeLevel = 7 } } });
        Assert.Equal(200, create.Status);
        Assert.Equal(1, create.Body!["created"]!.GetValue<int>());
        var nc = await f.WithDbAsync(db => db.Class.Where(c => c.CopiedFromClassId == c6).SingleAsync());
        Assert.True(nc.IsActive); Assert.Equal(toY, nc.AcademicYearId); Assert.Equal(w.CampusAId, nc.CampusId);
        // ENR-14
        Assert.StartsWith("Một lớp năm cũ xuất hiện hai lần", (await admin.PostAsync("/api/sch/rollover/classes", new { fromYearId = fromY, toYearId = toY, items = new[] { new { sourceClassId = c9, name = "X1", gradeLevel = 9 }, new { sourceClassId = c9, name = "X2", gradeLevel = 9 } } })).Message);
        Assert.StartsWith("Lớp 6A1 đã có lớp tương ứng ở năm học", (await admin.PostAsync("/api/sch/rollover/classes", new { fromYearId = fromY, toYearId = toY, items = new[] { new { sourceClassId = c6, name = "7A9", gradeLevel = 7 } } })).Message);
        var g10 = await admin.PostAsync("/api/sch/rollover/classes", new { fromYearId = fromY, toYearId = toY, items = new[] { new { sourceClassId = c9, name = "10A1", gradeLevel = 10 } } });
        Assert.Equal(400, g10.Status);
        Assert.Contains("khối chỉ từ 6 đến 9.", g10.Message);
        var foreign = await w.NewClassAsync(w.CampusAId, "EN14f" + World.Uniq(), 6);   // thuộc năm hiện hành
        Assert.StartsWith("Có lớp không thuộc năm học", (await admin.PostAsync("/api/sch/rollover/classes", new { fromYearId = fromY, toYearId = toY, items = new[] { new { sourceClassId = foreign, name = "Z1", gradeLevel = 7 } } })).Message);
        var deadCampus = await f.WithDbAsync(async db => { var c = new Campus { Code = "PHX" + World.Uniq(), Name = "Phân hiệu ngừng " + World.Uniq(), IsActive = false, CreatedAt = f.Clock.Now }; db.Campus.Add(c); await db.SaveChangesAsync(); return c.CampusId; });
        var onDead = await ClassInYear(fromY, "8D1", 8, deadCampus);
        var dead = await admin.PostAsync("/api/sch/rollover/classes", new { fromYearId = fromY, toYearId = toY, items = new[] { new { sourceClassId = onDead, name = "9D1", gradeLevel = 9 } } });
        Assert.Equal(400, dead.Status);
        Assert.EndsWith("đã ngừng hoạt động nên không tạo lớp mới được.", dead.Message);
    }

    [Fact]
    public async Task ENR_15_And_16_Move_whole_classes_and_pair_validations()
    {
        var admin = await AdminAsync();
        var (fromY, toY) = await YearsAsync();
        var src = await ClassInYear(fromY, "6B1", 6);
        var dst = await ClassInYear(toY, "7B1", 7);
        var fromStart = await YearStart(fromY);
        var (s1, e1) = await w.NewStudentAsync(src, "HS lên lớp 1 " + World.Uniq(), fromStart);
        var (s2, _) = await w.NewStudentAsync(src, "HS lên lớp 2 " + World.Uniq(), fromStart);
        // ENR-16: kiểm tra cặp
        Assert.EndsWith("trùng lớp nguồn.", (await admin.PostAsync("/api/sch/rollover/move", new { pairs = new[] { new { sourceClassId = src, targetClassId = src } } })).Message);
        Assert.Contains("không thuộc năm học sau năm học", (await admin.PostAsync("/api/sch/rollover/move", new { pairs = new[] { new { sourceClassId = dst, targetClassId = src } } })).Message);
        Assert.Equal("Chọn ít nhất một lớp để chuyển học sinh.", (await admin.PostAsync("/api/sch/rollover/move", new { pairs = Array.Empty<object>() })).Message);
        var inactive = await ClassInYear(toY, "7I1", 7, null, false);
        Assert.EndsWith("đã ngừng hoạt động nên không chuyển học sinh vào được.", (await admin.PostAsync("/api/sch/rollover/move", new { pairs = new[] { new { sourceClassId = src, targetClassId = inactive } } })).Message);
        var years = await admin.GetAsync($"/api/sch/rollover?fromYearId={toY}&toYearId={fromY}");
        Assert.StartsWith("Năm học đích (", years.Message);
        // ENR-15: chuyển
        var ok = await admin.PostAsync("/api/sch/rollover/move", new { pairs = new[] { new { sourceClassId = src, targetClassId = dst } } });
        Assert.Equal(200, ok.Status);
        Assert.Equal(2, ok.Body!["moved"]!.GetValue<int>());
        var start = await YearStart(toY);
        var roster = await admin.GetAsync($"/api/sch/classes/{dst}/roster?date={World.Iso(start)}");
        Assert.Equal(2, roster.Body!["students"]!.AsArray().Count);
        Assert.Null(await f.WithDbAsync(db => db.Enrollment.Where(e => e.EnrollmentId == e1).Select(e => e.EndDate).SingleAsync()));   // ghi danh cũ nguyên vẹn
        // chạy lại không tạo trùng
        var again = await admin.PostAsync("/api/sch/rollover/move", new { pairs = new[] { new { sourceClassId = src, targetClassId = dst } } });
        Assert.Equal(0, again.Body!["moved"]!.GetValue<int>());
        Assert.Equal(2, again.Body["skipped"]!.GetValue<int>());
        Assert.NotEqual(s1.PersonId, s2.PersonId);
    }

    [Fact]
    public async Task ENR_17_Move_selected_pupils_only_and_repeat_safely()
    {
        var admin = await AdminAsync();
        var (fromY, toY) = await YearsAsync();
        var src = await ClassInYear(fromY, "6C1", 6);
        var dst = await ClassInYear(toY, "7C1", 7);
        var st = await YearStart(fromY);
        var kids = new List<Who>();
        for (var i = 0; i < 4; i++) kids.Add((await w.NewStudentAsync(src, $"HS chọn {i} " + World.Uniq(), st)).Who);
        var list = await admin.GetAsync($"/api/sch/rollover/students?fromClassId={src}&toYearId={toY}");
        Assert.Equal(200, list.Status);
        Assert.Equal(4, list.Body!.AsArray().Count);
        Assert.All(list.Body.AsArray(), x => Assert.Null(x!["newClassId"]));
        var picked = kids.Take(3).Select(k => k.PersonId).ToArray();
        var mv = await admin.PostAsync("/api/sch/rollover/move-students", new { fromClassId = src, toClassId = dst, studentIds = picked });
        Assert.Equal(200, mv.Status);
        Assert.Equal(3, mv.Body!["moved"]!.GetValue<int>());
        var list2 = await admin.GetAsync($"/api/sch/rollover/students?fromClassId={src}&toYearId={toY}");
        Assert.Equal(3, list2.Body!.AsArray().Count(x => x!["newClassId"] != null));
        var again = await admin.PostAsync("/api/sch/rollover/move-students", new { fromClassId = src, toClassId = dst, studentIds = picked });
        Assert.Equal(0, again.Body!["moved"]!.GetValue<int>());
        Assert.Equal(3, again.Body["skipped"]!.GetValue<int>());
        Assert.Equal("Chọn ít nhất một học sinh để chuyển.", (await admin.PostAsync("/api/sch/rollover/move-students", new { fromClassId = src, toClassId = dst, studentIds = Array.Empty<int>() })).Message);
    }

    [Fact]
    public async Task ENR_18_And_19_Undo_before_the_new_year_starts_and_its_refusals()
    {
        var admin = await AdminAsync();
        var (fromY, toY) = await YearsAsync();
        var src = await ClassInYear(fromY, "6D1", 6);
        var dst = await ClassInYear(toY, "7D1", 7);
        var st = await YearStart(fromY);
        var (a, _) = await w.NewStudentAsync(src, "HS gỡ A " + World.Uniq(), st);
        var (b, _) = await w.NewStudentAsync(src, "HS gỡ B " + World.Uniq(), st);
        Assert.Equal(200, (await admin.PostAsync("/api/sch/rollover/move-students", new { fromClassId = src, toClassId = dst, studentIds = new[] { a.PersonId, b.PersonId } })).Status);
        var ids = await f.WithDbAsync(db => db.Enrollment.Where(e => e.ClassId == dst).Select(e => e.EnrollmentId).ToListAsync());
        // ENR-19: ghi danh thủ công không bắt đầu ngày đầu năm
        var (m, em) = await w.NewStudentAsync(dst, "HS thủ công " + World.Uniq(), (await YearStart(toY)).AddDays(20));
        var manual = await admin.PostAsync("/api/sch/rollover/undo", new { enrollmentIds = new[] { em } });
        Assert.Equal(400, manual.Status);
        Assert.Equal("Chỉ gỡ được việc xếp lớp do kết chuyển tạo ra (từ ngày đầu năm học).", manual.Message);
        // ENR-18: gỡ thành công
        var undo = await admin.PostAsync("/api/sch/rollover/undo", new { enrollmentIds = ids });
        Assert.Equal(200, undo.Status);
        Assert.Equal(2, int.Parse(undo.Raw.Trim()));
        var list = await admin.GetAsync($"/api/sch/rollover/students?fromClassId={src}&toYearId={toY}");
        Assert.All(list.Body!.AsArray(), x => Assert.Null(x!["newClassId"]));
        // ENR-19: năm đã bắt đầu
        var (fromS, toS) = await YearsAsync(true);
        var srcS = await ClassInYear(fromS, "6E1", 6);
        var dstS = await ClassInYear(toS, "7E1", 7);
        var (x, ex) = await w.NewStudentAsync(dstS, "HS năm đã bắt đầu " + World.Uniq(), await YearStart(toS));
        var started = await admin.PostAsync("/api/sch/rollover/undo", new { enrollmentIds = new[] { ex } });
        Assert.Equal(400, started.Status);
        Assert.StartsWith("Năm học ", started.Message);
        Assert.EndsWith("đã bắt đầu nên không gỡ được. Dùng Chuyển lớp hoặc Rời lớp ở màn Xếp lớp.", started.Message);
        Assert.NotEqual(0, srcS); Assert.NotEqual(0, x.PersonId);
    }

    [Fact]
    public async Task ENR_22_Leave_request_needs_a_current_class()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var admin = await AdminAsync();
        // ENR-22: học sinh không thuộc lớp nào -> phụ huynh không gửi được đơn nghỉ
        var parent = await w.NewAccountAsync("PH không lớp " + World.Uniq(), "Parent", "Parent", "Active");
        var kid = await w.NewAccountAsync("HS không lớp " + World.Uniq(), "Student", "Student", "Active");
        await f.WithDbAsync(async db =>
        {
            db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = kid.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var p = await f.LoginAsync(parent.Username);
        var day = World.Iso(f.Clock.Today.AddDays(1));
        var r = await p.PostAsync("/api/par/leave-requests/bulk", new { studentId = kid.PersonId, reason = "Ốm", days = new[] { new { date = day, sessionOfDay = "AllDay", slotIds = (int[]?)null } } });
        Assert.Equal(400, r.Status);
        Assert.Equal("Học sinh hiện không thuộc lớp nào. Không thể tạo đơn xin nghỉ.", r.Message);
        Assert.NotNull(admin);
    }
}
