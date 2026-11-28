using MSMS.Tests.Integration.Infrastructure;
using static MSMS.Tests.Integration.Infrastructure.Roles;

namespace MSMS.Tests.Integration.Modules.Sch;

/// <summary>SCH-01..03, 05: phân hiệu, tổ, môn, lớp, khung giờ, năm học, ngày nghỉ, hồ sơ trường. Tên test bắt đầu bằng mã case (STR_xx).</summary>
[Collection("api")]
public class StructureTests(ApiFactory f)
{
    private World w => f.World;
    private async Task<ApiClient> AdminAsync() { await w.EnsureAsync(); f.Clock.Reset(); return await f.LoginAsync(w.SchoolAdmin.Username); }
    private static string U() => World.Uniq();

    // ------------------------------------------------------------------ Phân hiệu

    [Fact]
    public async Task STR_01_To_02_Create_update_campus_and_unique_code_and_name()
    {
        var a = await AdminAsync();
        var code = "C" + U(); var name = "Phân hiệu STR " + U();
        var created = await a.PostAsync("/api/sch/campuses", new { code, name, address = "Hà Nội", phoneNumber = "0912345678" });
        Assert.Equal(200, created.Status);
        var id = created.Body!["campusId"]!.GetValue<int>();
        Assert.True(created.Body["isActive"]!.GetValue<bool>());
        var upd = await a.PutAsync($"/api/sch/campuses/{id}", new { code, name = name + " mới", address = "Hà Nội", phoneNumber = "0912345678" });
        Assert.True(upd.Ok);
        Assert.Contains(name + " mới", (await a.GetAsync("/api/sch/campuses")).Raw);
        var dupCode = await a.PostAsync("/api/sch/campuses", new { code, name = "Tên khác " + U() });
        Assert.Equal(400, dupCode.Status);
        Assert.StartsWith($"Mã phân hiệu \"{code}\" đã có", dupCode.Message);
        var dupName = await a.PostAsync("/api/sch/campuses", new { code = "C" + U(), name = name + " mới" });
        Assert.Equal(400, dupName.Status);
        Assert.Contains("đã có. File import tra cứu theo tên nên không được trùng.", dupName.Message);
    }

    [Fact]
    public async Task STR_03_And_04_Deactivating_a_campus_with_active_classes_is_blocked_and_inactive_campus_is_not_selectable()
    {
        var a = await AdminAsync();
        var campus = (await a.PostAsync("/api/sch/campuses", new { code = "C" + U(), name = "Phân hiệu STR3 " + U() })).Body!["campusId"]!.GetValue<int>();
        var cls = await w.NewClassAsync(campus, "S3" + U(), 6);
        var blocked = await a.PutAsync($"/api/sch/campuses/{campus}/active", new { isActive = false });
        Assert.Equal(400, blocked.Status);
        Assert.StartsWith("Phân hiệu còn 1 lớp đang hoạt động", blocked.Message);
        Assert.True((await a.PutAsync($"/api/sch/classes/{cls}/active", new { isActive = false })).Ok);
        Assert.True((await a.PutAsync($"/api/sch/campuses/{campus}/active", new { isActive = false })).Ok);
        // STR-04
        var year = w.YearId;
        var newClass = await a.PostAsync("/api/sch/classes", new { campusId = campus, academicYearId = year, name = "S4" + U(), gradeLevel = 6 });
        Assert.Equal(400, newClass.Status);
        Assert.Equal("Phân hiệu đã ngừng hoạt động. Chọn phân hiệu khác.", newClass.Message);
    }

    [Fact]
    public async Task STR_05_Campus_list_visibility()
    {
        await f.AssertGetByRoleAsync("/api/sch/campuses", Admin, Principal, Vice);
    }

    // ------------------------------------------------------------------ Tổ và môn

    [Fact]
    public async Task STR_06_To_09_Department_and_subject_rules()
    {
        var a = await AdminAsync();
        var dName = "Tổ STR " + U();
        var d = await a.PostAsync("/api/sch/departments", new { name = dName, description = "x" });
        Assert.Equal(200, d.Status);
        var deptId = d.Body!["departmentId"]!.GetValue<int>();
        var sName = "Môn STR " + U(); var abbr = "S" + U();
        var s = await a.PostAsync("/api/sch/subjects", new { departmentId = deptId, name = sName, abbreviation = abbr });
        Assert.Equal(200, s.Status);
        Assert.Equal(deptId, s.Body!["departmentId"]!.GetValue<int>());
        // STR-07
        Assert.Contains("đã có", (await a.PostAsync("/api/sch/departments", new { name = dName })).Message);
        Assert.Contains("đã có", (await a.PostAsync("/api/sch/subjects", new { departmentId = deptId, name = sName, abbreviation = "X" + U() })).Message);
        var abbrDup = await a.PostAsync("/api/sch/subjects", new { departmentId = deptId, name = "Môn khác " + U(), abbreviation = abbr });
        Assert.Equal(400, abbrDup.Status);
        Assert.StartsWith("Viết tắt", abbrDup.Message);
        // STR-09: tổ còn môn đang hoạt động, rồi tổ có tổ trưởng
        var sId = s.Body["subjectId"]!.GetValue<int>();
        var g1 = await a.PutAsync($"/api/sch/departments/{deptId}/active", new { isActive = false });
        Assert.Equal(400, g1.Status);
        Assert.StartsWith("Tổ còn 1 môn đang hoạt động", g1.Message);
        await a.PutAsync($"/api/sch/subjects/{sId}/active", new { isActive = false });
        var head = await w.NewStaffAsync("Tổ trưởng STR " + U(), "Teacher", w.CampusAId, deptId);
        await f.WithDbAsync(async db => { db.DepartmentHead.Add(new Api.Data.Entities.DepartmentHead { DepartmentId = deptId, StaffId = head.PersonId, StartDate = new DateOnly(2026, 9, 5), CreatedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
        Assert.Equal("Tổ đang có tổ trưởng. Kết thúc trách nhiệm tổ trưởng trước.", (await a.PutAsync($"/api/sch/departments/{deptId}/active", new { isActive = false })).Message);
    }

    [Fact]
    public async Task STR_08_A_subject_needs_an_active_department()
    {
        var a = await AdminAsync();
        var deptId = (await a.PostAsync("/api/sch/departments", new { name = "Tổ tắt " + U() })).Body!["departmentId"]!.GetValue<int>();
        Assert.True((await a.PutAsync($"/api/sch/departments/{deptId}/active", new { isActive = false })).Ok);
        var r = await a.PostAsync("/api/sch/subjects", new { departmentId = deptId, name = "Môn tắt " + U(), abbreviation = "T" + U() });
        Assert.Equal(400, r.Status);
        Assert.Equal("Tổ chuyên môn đã ngừng hoạt động. Chọn tổ khác.", r.Message);
        // bật lại môn khi tổ ngừng
        await a.PutAsync($"/api/sch/departments/{deptId}/active", new { isActive = true });
        var sId = (await a.PostAsync("/api/sch/subjects", new { departmentId = deptId, name = "Môn bật " + U(), abbreviation = "B" + U() })).Body!["subjectId"]!.GetValue<int>();
        await a.PutAsync($"/api/sch/subjects/{sId}/active", new { isActive = false });
        await a.PutAsync($"/api/sch/departments/{deptId}/active", new { isActive = false });
        var back = await a.PutAsync($"/api/sch/subjects/{sId}/active", new { isActive = true });
        Assert.Equal(400, back.Status);
        Assert.StartsWith("Tổ chuyên môn của môn này đã ngừng hoạt động. Bật lại tổ trước", back.Message);
    }

    [Fact]
    public async Task STR_10_Staff_read_the_catalog_and_only_the_school_admin_writes()
    {
        await f.AssertGetByRoleAsync("/api/sch/departments", Admin, Principal, Vice, Teacher);
        await f.AssertGetByRoleAsync("/api/sch/subjects", Admin, Principal, Vice, Teacher);
        var roles = await f.LoginAllAsync();
        foreach (var role in new[] { Principal, Vice, Teacher })
        {
            Assert.Equal(403, (await roles[role].PostAsync("/api/sch/departments", new { name = "x" })).Status);
            Assert.Equal(403, (await roles[role].PostAsync("/api/sch/subjects", new { departmentId = 1, name = "x", abbreviation = "x" })).Status);
        }
    }

    // ------------------------------------------------------------------ Lớp

    [Fact]
    public async Task STR_11_And_12_Create_a_class_with_unique_name_per_campus_year_and_grade_6_to_9()
    {
        var a = await AdminAsync();
        var name = "S11" + U();
        var r = await a.PostAsync("/api/sch/classes", new { campusId = w.CampusAId, academicYearId = w.YearId, name, gradeLevel = 6 });
        Assert.Equal(200, r.Status);
        Assert.True(r.Body!["isActive"]!.GetValue<bool>());
        Assert.Contains(name, (await a.GetAsync($"/api/sch/classes?campusId={w.CampusAId}&yearId={w.YearId}")).Raw);
        var dup = await a.PostAsync("/api/sch/classes", new { campusId = w.CampusAId, academicYearId = w.YearId, name, gradeLevel = 6 });
        Assert.Equal(400, dup.Status);
        Assert.StartsWith($"Lớp \"{name}\" đã có ở", dup.Message);
        Assert.Equal(200, (await a.PostAsync("/api/sch/classes", new { campusId = w.CampusBId, academicYearId = w.YearId, name, gradeLevel = 6 })).Status);
        foreach (var grade in new[] { 5, 10 })
            Assert.Equal("Khối chỉ từ 6 đến 9.", (await a.PostAsync("/api/sch/classes", new { campusId = w.CampusAId, academicYearId = w.YearId, name = "S12" + U(), gradeLevel = grade })).Message);
    }

    [Fact]
    public async Task STR_14_An_inactive_class_cannot_receive_pupils_or_a_homeroom_teacher()
    {
        var a = await AdminAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "S14" + U(), 6);
        Assert.True((await a.PutAsync($"/api/sch/classes/{cls}/active", new { isActive = false })).Ok);
        var s = await w.NewAccountAsync("HS STR14 " + U(), "Student", "Student", "Active");
        Assert.Equal("Lớp đã ngừng hoạt động. Kích hoạt lại lớp trước khi xếp lớp.", (await a.PostAsync($"/api/sch/classes/{cls}/enrollments", new { studentId = s.PersonId, startDate = World.Iso(f.Clock.Today) })).Message);
        var vice = await f.LoginAsync(w.ViceA.Username);
        var makeup = await vice.PostAsync("/api/scd/adjust/makeup", new { classId = cls, date = World.Iso(f.Clock.Today.AddDays(3)), periodSlotId = 1, subjectId = 1, teacherId = w.ViceA.PersonId });
        Assert.Equal(400, makeup.Status);
        var t = await w.NewStaffAsync("GV STR14 " + U(), "Teacher", w.CampusAId, null);
        var p = await f.LoginAsync(w.Principal.Username);
        var hr = await p.PostAsync("/api/ppl/responsibilities/assign", new { kind = "homeroom", targetId = cls, staffId = t.PersonId, startDate = World.Iso(f.Clock.Today) });
        Assert.Equal(400, hr.Status);
        Assert.Equal("Lớp không tồn tại hoặc đã ngừng hoạt động.", hr.Message);
    }

    [Fact]
    public async Task STR_15_Class_list_by_role_and_inactive_filter()
    {
        var a = await AdminAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "S15" + U(), 6);
        await a.PutAsync($"/api/sch/classes/{cls}/active", new { isActive = false });
        await f.AssertGetByRoleAsync("/api/sch/classes", Admin, Principal, Vice, Teacher);
        var plain = await a.GetAsync($"/api/sch/classes?campusId={w.CampusAId}&yearId={w.YearId}");
        var withInactive = await a.GetAsync($"/api/sch/classes?campusId={w.CampusAId}&yearId={w.YearId}&includeInactive=true");
        Assert.DoesNotContain($"\"classId\":{cls},", plain.Raw);
        Assert.Contains($"\"classId\":{cls},", withInactive.Raw);
    }

    // ------------------------------------------------------------------ Khung giờ

    [Fact]
    public async Task STR_16_Period_slot_creation_validation()
    {
        var a = await AdminAsync();
        // buổi chiều tiết 20 là số lớn ít dùng: tránh trùng dữ liệu mẫu (tiết 1-5)
        var ok = await a.PostAsync("/api/sch/period-slots", new { sessionOfDay = "Afternoon", periodNo = 12, startTime = "20:00", endTime = "20:45" });
        Assert.Equal(200, ok.Status);
        var dup = await a.PostAsync("/api/sch/period-slots", new { sessionOfDay = "Afternoon", periodNo = 12, startTime = "21:00", endTime = "21:45" });
        Assert.Equal(400, dup.Status);
        Assert.Contains("tiết 12 đã có", dup.Message);
        foreach (var n in new[] { 0, 21 })
            Assert.Equal("Số tiết phải từ 1 đến 20.", (await a.PostAsync("/api/sch/period-slots", new { sessionOfDay = "Morning", periodNo = n, startTime = "06:00", endTime = "06:45" })).Message);
        Assert.Equal("Giờ kết thúc phải sau giờ bắt đầu.", (await a.PostAsync("/api/sch/period-slots", new { sessionOfDay = "Afternoon", periodNo = 13, startTime = "20:00", endTime = "20:00" })).Message);
        Assert.Equal("Buổi chỉ có Sáng hoặc Chiều.", (await a.PostAsync("/api/sch/period-slots", new { sessionOfDay = "Evening", periodNo = 13, startTime = "20:00", endTime = "20:45" })).Message);
    }

    [Fact]
    public async Task STR_19_Period_slots_are_readable_by_everybody_and_writable_by_the_school_admin_only()
    {
        await f.AssertGetByRoleAsync("/api/sch/period-slots", All);
        var roles = await f.LoginAllAsync();
        foreach (var role in new[] { Principal, Vice, Teacher })
            Assert.Equal(403, (await roles[role].PutAsync("/api/sch/period-slots/1", new { startTime = "07:00", endTime = "07:45" })).Status);
    }

    // ------------------------------------------------------------------ Năm học, học kỳ, ngày nghỉ

    [Fact]
    public async Task STR_20_Academic_year_date_rules()
    {
        var a = await AdminAsync();
        var y = 3000 + int.Parse(U()) % 900;
        Assert.Equal("Ngày kết thúc phải sau ngày bắt đầu năm học.", (await a.PostAsync("/api/sch/academic-years", new { name = $"{y}-{y + 1}", startDate = $"{y}-09-05", endDate = $"{y}-09-05" })).Message);
        var tooLong = await a.PostAsync("/api/sch/academic-years", new { name = $"{y}-{y + 2}", startDate = $"{y}-09-05", endDate = $"{y + 1}-10-30" });
        Assert.Equal(400, tooLong.Status);
        Assert.StartsWith("Năm học dài tối đa 13 tháng", tooLong.Message);
        var name = $"{y}-{y + 1}";
        Assert.Equal(200, (await a.PostAsync("/api/sch/academic-years", new { name, startDate = $"{y}-09-05", endDate = $"{y + 1}-05-31" })).Status);
        var dup = await a.PostAsync("/api/sch/academic-years", new { name, startDate = $"{y}-09-05", endDate = $"{y + 1}-05-31" });
        Assert.Equal(400, dup.Status);
        Assert.StartsWith($"Năm học \"{name}\" đã có", dup.Message);
    }

    private async Task<(int Id, int Y)> NewYearAsync(ApiClient a)
    {
        var y = 3000 + int.Parse(U()) % 900;
        var r = await a.PostAsync("/api/sch/academic-years", new { name = $"{y}-{y + 1}", startDate = $"{y}-09-05", endDate = $"{y + 1}-06-30" });
        Assert.Equal(200, r.Status);
        return (r.Body!["academicYearId"]!.GetValue<int>(), y);
    }

    [Fact]
    public async Task STR_23_And_24_Semester_limits_and_shrinking_the_year()
    {
        var a = await AdminAsync();
        var (id, y) = await NewYearAsync(a);
        object Sems(string e1, string s2, string e2) => new { semester1 = new { startDate = $"{y}-09-05", endDate = e1 }, semester2 = new { startDate = s2, endDate = e2 } };
        var url = $"/api/sch/academic-years/{id}/semesters";
        Assert.True((await a.PutAsync(url, Sems($"{y + 1}-01-18", $"{y + 1}-01-19", $"{y + 1}-05-31"))).Ok);
        var late1 = await a.PutAsync(url, Sems($"{y + 1}-01-19", $"{y + 1}-01-20", $"{y + 1}-05-31"));
        Assert.Equal(400, late1.Status);
        Assert.StartsWith("Học kỳ 1 phải kết thúc muộn nhất ngày 18/01/", late1.Message);
        var late2 = await a.PutAsync(url, Sems($"{y + 1}-01-18", $"{y + 1}-01-19", $"{y + 1}-06-01"));
        Assert.Equal(400, late2.Status);
        Assert.StartsWith("Học kỳ 2 phải kết thúc muộn nhất ngày 31/05/", late2.Message);
        var overlap = await a.PutAsync(url, Sems($"{y + 1}-01-18", $"{y + 1}-01-10", $"{y + 1}-05-31"));
        Assert.Equal(400, overlap.Status);
        Assert.StartsWith("Học kỳ 1 phải kết thúc trước ngày bắt đầu học kỳ 2", overlap.Message);
        var bad = await a.PutAsync(url, Sems($"{y}-09-05", $"{y + 1}-01-19", $"{y + 1}-05-31"));
        Assert.Equal(400, bad.Status);
        Assert.Contains("ngày kết thúc phải sau ngày bắt đầu", bad.Message);
        // STR-24: thu hẹp năm học xuống trước khi học kỳ 2 kết thúc
        Assert.True((await a.PutAsync(url, Sems($"{y + 1}-01-18", $"{y + 1}-01-19", $"{y + 1}-05-31"))).Ok);
        var shrink = await a.PutAsync($"/api/sch/academic-years/{id}", new { name = $"{y}-{y + 1}", startDate = $"{y}-09-05", endDate = $"{y + 1}-04-30" });
        Assert.Equal(400, shrink.Status);
        Assert.Contains("đang nằm ngoài khoảng năm học mới", shrink.Message);
    }

    [Fact]
    public async Task STR_25_Holidays_scope_duplicates_and_impact_preview()
    {
        var a = await AdminAsync();
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var date = w.NextMonday().AddDays(49);
        var sess = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 1);
        var impact = await a.GetAsync($"/api/sch/holidays/impact?date={World.Iso(date)}");
        Assert.Equal(200, impact.Status);
        Assert.True(impact.Body!["sessions"]!.GetValue<int>() >= 1);
        var h = await a.PostAsync("/api/sch/holidays", new { academicYearId = w.YearId, campusId = (int?)null, holidayDate = World.Iso(date), reason = "Nghỉ lễ STR25" });
        Assert.Equal(200, h.Status);
        Assert.True(h.Body!["cancelledSessions"]!.GetValue<int>() >= 1);
        Assert.Equal("Cancelled", (await w.SessionAsync(sess)).Status);
        var dup = await a.PostAsync("/api/sch/holidays", new { academicYearId = w.YearId, campusId = (int?)null, holidayDate = World.Iso(date), reason = "x" });
        Assert.Equal(400, dup.Status);
        Assert.Equal("Ngày này đã khai là ngày nghỉ cho phạm vi đã chọn.", dup.Message);
        var outside = await a.PostAsync("/api/sch/holidays", new { academicYearId = w.YearId, campusId = (int?)null, holidayDate = "2035-01-01", reason = "x" });
        Assert.Equal(400, outside.Status);
        Assert.StartsWith("Ngày nghỉ phải nằm trong năm học", outside.Message);
        // phạm vi một phân hiệu
        var d2 = date.AddDays(1);
        var sessB = await w.NewSessionAsync(s.ClassBId, s.SubjectId, s.Absent.PersonId, d2, 2);
        var sessA = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, d2, 3);
        Assert.Equal(200, (await a.PostAsync("/api/sch/holidays", new { academicYearId = w.YearId, campusId = w.CampusAId, holidayDate = World.Iso(d2), reason = "Chỉ phân hiệu A" })).Status);
        Assert.Equal("Cancelled", (await w.SessionAsync(sessA)).Status);
        Assert.Equal("Scheduled", (await w.SessionAsync(sessB)).Status);
    }

    [Fact]
    public async Task STR_31_Holiday_impact_preview_is_for_the_school_admin_only()
    {
        await f.AssertGetByRoleAsync("/api/sch/holidays/impact?date=2026-12-10", Admin);
        var a = await AdminAsync();
        Assert.Equal(400, (await a.GetAsync("/api/sch/holidays/impact")).Status);
    }

    [Fact]
    public async Task STR_27_Calendar_read_access_and_admin_only_writes()
    {
        await f.AssertGetByRoleAsync("/api/sch/academic-years", All);
        await f.AssertGetByRoleAsync("/api/sch/holidays", All);
        var roles = await f.LoginAllAsync();
        foreach (var role in new[] { Principal, Vice, Teacher, Student, Parent })
        {
            Assert.Equal(403, (await roles[role].PostAsync("/api/sch/academic-years", new { name = "x", startDate = "2040-09-05", endDate = "2041-05-31" })).Status);
            Assert.Equal(403, (await roles[role].PostAsync("/api/sch/holidays", new { academicYearId = 1, holidayDate = "2026-12-24" })).Status);
        }
    }

    // ------------------------------------------------------------------ Hồ sơ trường và thống kê

    [Fact]
    public async Task STR_28_School_profile_is_public()
    {
        var anon = f.Anonymous();
        Assert.Equal(200, (await anon.GetAsync("/api/school")).Status);
        Assert.NotEqual(401, (await anon.GetAsync("/api/school/logo")).Status);
    }

    [Fact]
    public async Task STR_29_School_profile_validation()
    {
        var a = await AdminAsync();
        var web = await a.PutAsync("/api/school", new { name = "Trường kiểm thử", website = "ftp://x" });
        Assert.Equal(400, web.Status);
        Assert.Equal("Website phải bắt đầu bằng http:// hoặc https://.", web.Message);
        var mail = await a.PutAsync("/api/school", new { name = "Trường kiểm thử", email = "abc" });
        Assert.Equal(400, mail.Status);
        Assert.Contains("mail", mail.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True((await a.PutAsync("/api/school", new { name = "Trường kiểm thử", website = "https://truong.example.com", email = "t@example.com" })).Ok);
    }

    [Fact]
    public async Task STR_30_Structure_statistics_by_role()
    {
        await f.AssertGetByRoleAsync("/api/sch/stats/campuses", Admin, Principal);
        await f.AssertGetByRoleAsync("/api/sch/stats/classes", Admin, Principal);
        await f.AssertGetByRoleAsync("/api/sch/stats/catalog", Admin, Principal, Vice);
    }
}
