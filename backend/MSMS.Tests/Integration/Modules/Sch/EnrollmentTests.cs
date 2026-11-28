using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Sch;

/// <summary>SCH-04: xếp lớp, chuyển lớp, cho rời lớp, tốt nghiệp (BR-09). Tên test bắt đầu bằng mã case (ENR_xx).</summary>
[Collection("api")]
public class EnrollmentTests(ApiFactory f)
{
    private World w => f.World;
    private async Task<ApiClient> AdminAsync() { await w.EnsureAsync(); f.Clock.Reset(); return await f.LoginAsync(w.SchoolAdmin.Username); }
    private DateOnly Today => f.Clock.Today;

    private async Task<int> StudentIdsInRoster(ApiClient c, int classId, DateOnly date, int studentId)
    {
        var r = await c.GetAsync($"/api/sch/classes/{classId}/roster?date={World.Iso(date)}");
        Assert.Equal(200, r.Status);
        return r.Body!["students"]!.AsArray().Count(x => x!["studentId"]!.GetValue<int>() == studentId);
    }

    [Fact]
    public async Task ENR_01_Place_a_pupil_into_a_class()
    {
        var admin = await AdminAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "EN1" + World.Uniq(), 6);
        var s = await w.NewAccountAsync("HS xếp lớp " + World.Uniq(), "Student", "Student", "Active");
        var r = await admin.PostAsync($"/api/sch/classes/{cls}/enrollments", new { studentId = s.PersonId, startDate = World.Iso(Today) });
        Assert.Equal(204, r.Status);
        Assert.Equal(1, await StudentIdsInRoster(admin, cls, Today, s.PersonId));
    }

    [Fact]
    public async Task ENR_03_Class_placement_validations()
    {
        var admin = await AdminAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "EN3" + World.Uniq(), 6);
        var left = await w.NewAccountAsync("HS đã rời " + World.Uniq(), "Student", "Student", "Active");
        await f.WithDbAsync(async db => { (await db.StudentProfile.SingleAsync(p => p.StudentId == left.PersonId)).Status = "Transferred"; await db.SaveChangesAsync(); });
        var notStudying = await admin.PostAsync($"/api/sch/classes/{cls}/enrollments", new { studentId = left.PersonId, startDate = World.Iso(Today) });
        Assert.Equal(400, notStudying.Status);
        Assert.EndsWith("không còn ở trạng thái Đang học nên không xếp lớp được.", notStudying.Message);
        Assert.Equal("Chọn ít nhất một học sinh để xếp lớp.", (await admin.PostAsync($"/api/sch/classes/{cls}/enrollments/bulk", new { studentIds = Array.Empty<int>(), startDate = World.Iso(Today) })).Message);
        Assert.StartsWith("Có học sinh không tồn tại", (await admin.PostAsync($"/api/sch/classes/{cls}/enrollments/bulk", new { studentIds = new[] { 987654321 }, startDate = World.Iso(Today) })).Message);
        var inactive = await w.NewClassAsync(w.CampusAId, "EN3x" + World.Uniq(), 6);
        await f.WithDbAsync(async db => { (await db.Class.SingleAsync(c => c.ClassId == inactive)).IsActive = false; await db.SaveChangesAsync(); });
        var ok = await w.NewAccountAsync("HS ok " + World.Uniq(), "Student", "Student", "Active");
        var r = await admin.PostAsync($"/api/sch/classes/{inactive}/enrollments", new { studentId = ok.PersonId, startDate = World.Iso(Today) });
        Assert.Equal("Lớp đã ngừng hoạt động. Kích hoạt lại lớp trước khi xếp lớp.", r.Message);
    }

    [Fact]
    public async Task ENR_04_A_pupil_cannot_be_in_two_classes_in_overlapping_periods()
    {
        var admin = await AdminAsync();
        var a = await w.NewClassAsync(w.CampusAId, "EN4a" + World.Uniq(), 6);
        var b = await w.NewClassAsync(w.CampusAId, "EN4b" + World.Uniq(), 6);
        var (s, _) = await w.NewStudentAsync(a, "HS hai lớp " + World.Uniq());
        var r = await admin.PostAsync($"/api/sch/classes/{b}/enrollments", new { studentId = s.PersonId, startDate = World.Iso(Today) });
        Assert.Equal(400, r.Status);
        Assert.Equal(0, await StudentIdsInRoster(admin, b, Today, s.PersonId));
    }

    [Fact]
    public async Task ENR_06_Roster_permission()
    {
        await w.EnsureAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "EN6" + World.Uniq(), 6);
        var teacher = await f.LoginAsPersonAsync("Phạm Minh Tuấn");
        var r = await teacher.GetAsync($"/api/sch/classes/{cls}/roster");
        Assert.Equal(403, r.Status);
        Assert.Equal("Không có quyền xem danh sách học sinh của lớp này.", r.Message);
    }

    [Fact]
    public async Task ENR_07_And_08_Transfer_within_the_same_year_and_grade_with_validations()
    {
        var admin = await AdminAsync();
        var from = await w.NewClassAsync(w.CampusAId, "EN7a" + World.Uniq(), 6);
        var to = await w.NewClassAsync(w.CampusAId, "EN7b" + World.Uniq(), 6);
        var g7 = await w.NewClassAsync(w.CampusAId, "EN7c" + World.Uniq(), 7);
        var (s, enr) = await w.NewStudentAsync(from, "HS chuyển lớp " + World.Uniq());
        var date = Today.AddDays(5);
        // ENR-08 trước: các lỗi
        Assert.StartsWith("Lớp chuyển đến trùng lớp hiện tại", (await admin.PostAsync($"/api/sch/enrollments/{enr}/transfer", new { toClassId = from, effectiveDate = World.Iso(date) })).Message);
        var other7 = await admin.PostAsync($"/api/sch/enrollments/{enr}/transfer", new { toClassId = g7, effectiveDate = World.Iso(date) });
        Assert.Equal(400, other7.Status);
        Assert.StartsWith("Chỉ chuyển lớp trong cùng khối.", other7.Message);
        var otherYear = await f.WithDbAsync(async db =>
        {
            var y = new Api.Data.Entities.AcademicYear { Name = "2099-2100 " + World.Uniq(), StartDate = new DateOnly(2099, 9, 5), EndDate = new DateOnly(2100, 5, 31), IsCurrent = false };
            db.AcademicYear.Add(y);
            await db.SaveChangesAsync();
            var c = new Api.Data.Entities.Class { CampusId = w.CampusAId, AcademicYearId = y.AcademicYearId, Name = "EN7y" + World.Uniq(), GradeLevel = 6, IsActive = true, CreatedAt = f.Clock.Now };
            db.Class.Add(c);
            await db.SaveChangesAsync();
            return c.ClassId;
        });
        Assert.Equal("Chỉ chuyển lớp trong cùng năm học. Sang năm mới dùng Kết chuyển năm học.", (await admin.PostAsync($"/api/sch/enrollments/{enr}/transfer", new { toClassId = otherYear, effectiveDate = World.Iso(date) })).Message);
        var inactive = await w.NewClassAsync(w.CampusAId, "EN7i" + World.Uniq(), 6);
        await f.WithDbAsync(async db => { (await db.Class.SingleAsync(c => c.ClassId == inactive)).IsActive = false; await db.SaveChangesAsync(); });
        Assert.StartsWith("Lớp chuyển đến đã ngừng hoạt động", (await admin.PostAsync($"/api/sch/enrollments/{enr}/transfer", new { toClassId = inactive, effectiveDate = World.Iso(date) })).Message);
        // ENR-07: chuyển thành công
        Assert.Equal(204, (await admin.PostAsync($"/api/sch/enrollments/{enr}/transfer", new { toClassId = to, effectiveDate = World.Iso(date) })).Status);
        Assert.Equal(1, await StudentIdsInRoster(admin, from, date.AddDays(-1), s.PersonId));
        Assert.Equal(0, await StudentIdsInRoster(admin, to, date.AddDays(-1), s.PersonId));
        Assert.Equal(0, await StudentIdsInRoster(admin, from, date, s.PersonId));
        Assert.Equal(1, await StudentIdsInRoster(admin, to, date, s.PersonId));
        // chuyển lần nữa khi đã có lịch chuyển
        var again = await admin.PostAsync($"/api/sch/enrollments/{enr}/transfer", new { toClassId = to, effectiveDate = World.Iso(date) });
        Assert.Equal(400, again.Status);
        Assert.StartsWith("Học sinh này đã rời lớp hoặc đã có lịch chuyển lớp.", again.Message);
    }

    [Fact]
    public async Task ENR_09_Bulk_transfer_is_all_or_nothing_and_names_the_failing_pupil()
    {
        var admin = await AdminAsync();
        var from = await w.NewClassAsync(w.CampusAId, "EN9a" + World.Uniq(), 6);
        var to = await w.NewClassAsync(w.CampusAId, "EN9b" + World.Uniq(), 6);
        var (a, ea) = await w.NewStudentAsync(from, "HS bulk A " + World.Uniq());
        var (b, eb) = await w.NewStudentAsync(from, "HS bulk B " + World.Uniq());
        var (c, ec) = await w.NewStudentAsync(from, "HS bulk C " + World.Uniq());
        Assert.Equal(204, (await admin.PostAsync($"/api/sch/enrollments/{ec}/transfer", new { toClassId = to, effectiveDate = World.Iso(Today.AddDays(2)) })).Status);   // C đã có lịch chuyển
        var r = await admin.PostAsync("/api/sch/enrollments/transfer-bulk", new { enrollmentIds = new[] { ea, eb, ec }, toClassId = to, effectiveDate = World.Iso(Today.AddDays(3)) });
        Assert.Equal(400, r.Status);
        Assert.StartsWith(c.FullName + ":", r.Message);
        Assert.Equal(1, await StudentIdsInRoster(admin, from, Today.AddDays(4), a.PersonId));
        Assert.Equal(1, await StudentIdsInRoster(admin, from, Today.AddDays(4), b.PersonId));
        var unknown = await admin.PostAsync("/api/sch/enrollments/transfer-bulk", new { enrollmentIds = new[] { 987654321 }, toClassId = to, effectiveDate = World.Iso(Today.AddDays(3)) });
        Assert.StartsWith("Không tìm thấy học sinh trong lớp.", unknown.Message);
    }

    [Fact]
    public async Task ENR_10_And_12_Close_with_a_reason_and_graduate_only_the_final_grade()
    {
        var admin = await AdminAsync();
        var c6 = await w.NewClassAsync(w.CampusAId, "EN10a" + World.Uniq(), 6);
        var c9 = await w.NewClassAsync(w.CampusAId, "EN10b" + World.Uniq(), 9);
        var (s6, e6) = await w.NewStudentAsync(c6, "HS lớp 6 " + World.Uniq());
        var (s6b, e6b) = await w.NewStudentAsync(c6, "HS lớp 6b " + World.Uniq());
        var (s9, e9) = await w.NewStudentAsync(c9, "HS lớp 9 " + World.Uniq());
        var (s9b, _) = await w.NewStudentAsync(c9, "HS lớp 9b " + World.Uniq());
        // ENR-10 bước 3: lý do rỗng hoặc lạ
        Assert.Equal("Chọn lý do rời lớp: chuyển lớp, chuyển trường hoặc tốt nghiệp.", (await admin.PostAsync($"/api/sch/enrollments/{e6}/close", new { reason = "" })).Message);
        Assert.Equal("Chọn lý do rời lớp: chuyển lớp, chuyển trường hoặc tốt nghiệp.", (await admin.PostAsync($"/api/sch/enrollments/{e6}/close", new { reason = "Other" })).Message);
        // ENR-25: tốt nghiệp ở khối dưới 9
        var grad6 = await admin.PostAsync($"/api/sch/enrollments/{e6}/close", new { reason = "Graduated" });
        Assert.Equal(400, grad6.Status);
        Assert.Contains("chưa phải khối cuối cấp nên không cho tốt nghiệp được. Em rời trường thì chọn lý do Chuyển trường.", grad6.Message);
        Assert.Null(await f.WithDbAsync(db => db.Enrollment.Where(e => e.EnrollmentId == e6).Select(e => e.EndDate).SingleAsync()));
        // ENR-10: chuyển trường được; đóng lần nữa bị từ chối
        Assert.Equal(204, (await admin.PostAsync($"/api/sch/enrollments/{e6}/close", new { reason = "SchoolTransfer" })).Status);
        Assert.Equal("Học sinh này đã rời lớp từ trước.", (await admin.PostAsync($"/api/sch/enrollments/{e6}/close", new { reason = "SchoolTransfer" })).Message);
        // ENR-25: tốt nghiệp ở lớp 9 được
        Assert.Equal(204, (await admin.PostAsync($"/api/sch/enrollments/{e9}/close", new { reason = "Graduated" })).Status);
        // ENR-12: tốt nghiệp cả lớp
        var r6 = await admin.PostAsync($"/api/sch/classes/{c6}/graduate", new { studentIds = new[] { s6b.PersonId } });
        Assert.Equal(400, r6.Status);
        Assert.StartsWith($"Lớp ", r6.Message);
        Assert.Contains("chưa phải khối cuối cấp nên không cho tốt nghiệp được.", r6.Message);
        var left = await admin.PostAsync($"/api/sch/classes/{c9}/graduate", new { studentIds = new[] { s9.PersonId } });   // đã rời lớp
        Assert.Equal(400, left.Status);
        Assert.StartsWith("Có học sinh không còn học ở lớp này.", left.Message);
        var ok = await admin.PostAsync($"/api/sch/classes/{c9}/graduate", new { studentIds = new[] { s9b.PersonId } });
        Assert.Equal(200, ok.Status);
        Assert.Equal("Graduated", await f.WithDbAsync(db => db.StudentProfile.Where(p => p.StudentId == s9b.PersonId).Select(p => p.Status).SingleAsync()));
        Assert.Equal(e6b, e6b);
    }

    [Fact]
    public async Task ENR_24_Transfer_is_refused_for_a_pupil_who_has_left_the_school()
    {
        var admin = await AdminAsync();
        var from = await w.NewClassAsync(w.CampusAId, "EN24a" + World.Uniq(), 6);
        var to = await w.NewClassAsync(w.CampusAId, "EN24b" + World.Uniq(), 6);
        var (t, et) = await w.NewStudentAsync(from, "HS đã chuyển trường " + World.Uniq());
        var (g, eg) = await w.NewStudentAsync(from, "HS đã tốt nghiệp " + World.Uniq());
        var (ok, eo) = await w.NewStudentAsync(from, "HS đang học " + World.Uniq());
        await f.WithDbAsync(async db =>
        {
            (await db.StudentProfile.SingleAsync(p => p.StudentId == t.PersonId)).Status = "Transferred";
            (await db.StudentProfile.SingleAsync(p => p.StudentId == g.PersonId)).Status = "Graduated";
            await db.SaveChangesAsync();
        });
        var body = new { toClassId = to, effectiveDate = World.Iso(Today.AddDays(2)) };
        Assert.Equal("Học sinh đã chuyển trường nên không chuyển lớp được.", (await admin.PostAsync($"/api/sch/enrollments/{et}/transfer", body)).Message);
        Assert.Equal("Học sinh đã tốt nghiệp nên không chuyển lớp được.", (await admin.PostAsync($"/api/sch/enrollments/{eg}/transfer", body)).Message);
        var bulk = await admin.PostAsync("/api/sch/enrollments/transfer-bulk", new { enrollmentIds = new[] { eo, et }, toClassId = to, effectiveDate = World.Iso(Today.AddDays(2)) });
        Assert.Equal(400, bulk.Status);
        Assert.StartsWith(t.FullName + ":", bulk.Message);
        Assert.Equal(1, await StudentIdsInRoster(admin, from, Today.AddDays(3), ok.PersonId));   // học sinh đang học không bị chuyển
    }

    [Fact]
    public async Task ENR_25_Reason_Graduated_is_accepted_only_for_the_final_grade()
    {
        var admin = await AdminAsync();
        var c6 = await w.NewClassAsync(w.CampusAId, "EN25a" + World.Uniq(), 6);
        var c9 = await w.NewClassAsync(w.CampusAId, "EN25b" + World.Uniq(), 9);
        var (s6, e6) = await w.NewStudentAsync(c6, "HS 6 " + World.Uniq());
        var (s9, e9) = await w.NewStudentAsync(c9, "HS 9 " + World.Uniq());
        var r = await admin.PostAsync($"/api/sch/enrollments/{e6}/close", new { reason = "Graduated" });
        Assert.Equal(400, r.Status);
        Assert.Contains("chưa phải khối cuối cấp nên không cho tốt nghiệp được. Em rời trường thì chọn lý do Chuyển trường.", r.Message);
        Assert.Null(await f.WithDbAsync(db => db.Enrollment.Where(e => e.EnrollmentId == e6).Select(e => e.EndDate).SingleAsync()));
        Assert.Equal(204, (await admin.PostAsync($"/api/sch/enrollments/{e9}/close", new { reason = "Graduated" })).Status);
        Assert.Equal(204, (await admin.PostAsync($"/api/sch/enrollments/{e6}/close", new { reason = "SchoolTransfer" })).Status);
        Assert.NotEqual(s6.PersonId, s9.PersonId);
    }
}
