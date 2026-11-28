using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;
using static MSMS.Tests.Integration.Infrastructure.Roles;

namespace MSMS.Tests.Integration.Modules.Ppl;

/// <summary>PPL-01..03: hồ sơ cán bộ, học sinh, phụ huynh. Tên test bắt đầu bằng mã case (PPLP_xx).</summary>
[Collection("api")]
public class PeopleTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();
    private static string NatId() => "07" + (10000000000L + long.Parse(U()) * 7919 + Random.Shared.NextInt64(1, 1000000)).ToString()[..10];
    private static string Phone() => "09" + Random.Shared.NextInt64(10000000, 99999999);
    private async Task<ApiClient> AdminAsync() { await w.EnsureAsync(); f.Clock.Reset(); return await f.LoginAsync(w.SchoolAdmin.Username); }
    private DateOnly Today => f.Clock.Today;

    private object StaffBody(string? abbr = null, string? nid = null, string? phone = null, string? email = null, DateOnly? dob = null, int? campus = null, int? dept = null) => new
    {
        fullName = "Cán bộ PPLP " + U(), dateOfBirth = World.Iso(dob ?? new DateOnly(1985, 5, 5)), nationalId = nid ?? NatId(), gender = "Male",
        phoneNumber = phone ?? Phone(), email = email ?? $"cb{U()}@example.com", primaryCampusId = campus ?? w.CampusAId, departmentId = dept, teacherAbbr = abbr ?? "A" + U(), position = "Giáo viên",
    };

    // ------------------------------------------------------------------ Cán bộ

    [Fact]
    public async Task PPLP_01_To_03_Create_a_staff_profile_with_boundaries_and_duplicates()
    {
        var a = await AdminAsync();
        var ok = await a.PostAsync("/api/ppl/staff", StaffBody());
        Assert.Equal(200, ok.Status);
        Assert.True(ok.Body!["isActive"]!.GetValue<bool>());
        Assert.False(string.IsNullOrEmpty(ok.Body["personCode"]!.GetValue<string>()));
        // PPLP-02: tuổi 17 / 18 / 75 / 76
        foreach (var (years, expected) in new[] { (17, false), (18, true), (75, true), (76, false) })
        {
            var dob = Today.AddYears(-years);
            var r = await a.PostAsync("/api/ppl/staff", StaffBody(dob: dob));
            Assert.True(expected ? r.Ok : r.Status == 400, $"tuổi {years}: {r.Status} {r.Message}");
        }
        Assert.Equal(400, (await a.PostAsync("/api/ppl/staff", StaffBody(dob: Today.AddDays(1)))).Status);
        Assert.Equal(400, (await a.PostAsync("/api/ppl/staff", StaffBody(nid: "12345678901"))).Status);
        Assert.Equal(400, (await a.PostAsync("/api/ppl/staff", StaffBody(nid: "1234567890123"))).Status);
        Assert.Equal(400, (await a.PostAsync("/api/ppl/staff", StaffBody(nid: "12345678901A"))).Status);
        Assert.Equal("Số điện thoại di động phải gồm đúng 10 chữ số, đầu số 03, 05, 07, 08 hoặc 09.", (await a.PostAsync("/api/ppl/staff", StaffBody(phone: "0123456789"))).Message);
        Assert.Equal(400, (await a.PostAsync("/api/ppl/staff", StaffBody(email: "khong-co-a-cong"))).Status);
        // PPLP-03: trùng số định danh và viết tắt
        var nid = NatId(); var abbr = "ZZ" + U();
        Assert.Equal(200, (await a.PostAsync("/api/ppl/staff", StaffBody(nid: nid, abbr: abbr))).Status);
        var dupId = await a.PostAsync("/api/ppl/staff", StaffBody(nid: nid));
        Assert.Equal(400, dupId.Status);
        Assert.Contains("CB", dupId.Message);
        var dupAbbr = await a.PostAsync("/api/ppl/staff", StaffBody(abbr: abbr));
        Assert.Equal(400, dupAbbr.Status);
        Assert.StartsWith("Viết tắt", dupAbbr.Message);
    }

    [Fact]
    public async Task PPLP_04_Inactive_campus_or_department_cannot_be_assigned()
    {
        var a = await AdminAsync();
        var campus = await f.WithDbAsync(async db =>
        {
            var c = new Campus { Code = "PP" + U(), Name = "Phân hiệu tắt " + U(), IsActive = false, CreatedAt = f.Clock.Now };
            db.Campus.Add(c);
            await db.SaveChangesAsync();
            return c.CampusId;
        });
        var r1 = await a.PostAsync("/api/ppl/staff", StaffBody(campus: campus));
        Assert.Equal(400, r1.Status);
        Assert.StartsWith("Phân hiệu chính đã ngừng hoạt động", r1.Message);
        var dept = await f.WithDbAsync(async db =>
        {
            var d = new SubjectDepartment { Name = "Tổ tắt PPLP " + U(), IsActive = false };
            db.SubjectDepartment.Add(d);
            await db.SaveChangesAsync();
            return d.DepartmentId;
        });
        var r2 = await a.PostAsync("/api/ppl/staff", StaffBody(dept: dept));
        Assert.Equal(400, r2.Status);
        Assert.StartsWith("Tổ chuyên môn đã ngừng hoạt động", r2.Message);
    }

    [Fact]
    public async Task PPLP_05_To_07_Deactivating_staff_is_blocked_by_duties_and_sessions_and_inactive_staff_cannot_be_assigned()
    {
        var a = await AdminAsync();
        var s = await w.NewScenarioAsync();
        // PPLP-05: tổ trưởng đang giữ trách nhiệm
        var blocked = await a.PutAsync($"/api/ppl/staff/{s.Head.PersonId}/active", new { isActive = false });
        Assert.Equal(400, blocked.Status);
        Assert.StartsWith("Cán bộ đang giữ:", blocked.Message);
        Assert.Contains("Chuyển giao hoặc kết thúc các trách nhiệm đó trước.", blocked.Message);
        Assert.True(await f.WithDbAsync(db => db.StaffProfile.Where(p => p.StaffId == s.Head.PersonId).Select(p => p.IsActive).SingleAsync()));
        // PPLP-06: còn tiết chưa kết thúc
        await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, w.NextMonday(), 1);
        var sess = await a.PutAsync($"/api/ppl/staff/{s.Absent.PersonId}/active", new { isActive = false });
        Assert.Equal(400, sess.Status);
        Assert.StartsWith("Cán bộ còn 1 tiết Đã xếp lịch chưa kết thúc.", sess.Message);
        // PPLP-07: cán bộ đã ngừng hoạt động không nhận trách nhiệm
        Assert.True((await a.PutAsync($"/api/ppl/staff/{s.Sub.PersonId}/active", new { isActive = false })).Ok);
        var p = await f.LoginAsync(w.Principal.Username);
        var assign = await p.PostAsync("/api/ppl/responsibilities/assign", new { kind = "homeroom", targetId = s.ClassAId, staffId = s.Sub.PersonId, startDate = World.Iso(Today) });
        Assert.Equal(400, assign.Status);
        Assert.StartsWith("Cán bộ đã ngừng hoạt động", assign.Message);
    }

    [Fact]
    public async Task PPLP_09_Staff_list_scope()
    {
        await w.EnsureAsync();
        foreach (var user in new[] { w.SchoolAdmin.Username, w.Principal.Username, w.ViceA.Username })
            Assert.Equal(200, (await (await f.LoginAsync(user)).GetAsync("/api/ppl/staff?pageSize=100")).Status);
    }

    // ------------------------------------------------------------------ Học sinh

    private object StudentBody(DateOnly? dob = null, string? nid = null, string gender = "Nam", string? note = null) => new
    {
        fullName = "Học sinh PPLP " + U(), dateOfBirth = World.Iso(dob ?? Today.AddYears(-12)), nationalId = nid ?? NatId(), gender, phoneNumber = (string?)null, address = "Hà Nội", note,
    };

    [Fact]
    public async Task PPLP_10_And_11_Create_update_a_student_with_validation_boundaries()
    {
        var a = await AdminAsync();
        var ok = await a.PostAsync("/api/ppl/students", StudentBody());
        Assert.Equal(200, ok.Status);
        Assert.Equal("Studying", ok.Body!["status"]!.GetValue<string>());
        var id = ok.Body["studentId"]!.GetValue<int>();
        var code = ok.Body["personCode"]!.GetValue<string>();
        var upd = await a.PutAsync($"/api/ppl/students/{id}", new { fullName = ok.Body["fullName"]!.GetValue<string>(), dateOfBirth = World.Iso(Today.AddYears(-12)), nationalId = ok.Body["nationalId"]!.GetValue<string>(), gender = "Nam", address = "Hải Phòng" });
        Assert.Equal(200, upd.Status);
        Assert.Equal(code, upd.Body!["personCode"]!.GetValue<string>());
        // PPLP-11: tuổi 4 / 5 / 25 / 26
        foreach (var (years, expected) in new[] { (4, false), (5, true), (25, true), (26, false) })
        {
            var r = await a.PostAsync("/api/ppl/students", StudentBody(dob: Today.AddYears(-years)));
            Assert.True(expected ? r.Ok : r.Status == 400, $"tuổi {years}: {r.Status} {r.Message}");
        }
        var nid = NatId();
        Assert.Equal(200, (await a.PostAsync("/api/ppl/students", StudentBody(nid: nid))).Status);
        var dup = await a.PostAsync("/api/ppl/students", StudentBody(nid: nid));
        Assert.Equal(400, dup.Status);
        Assert.Contains("HS", dup.Message);
        Assert.Equal(400, (await a.PostAsync("/api/ppl/students", StudentBody(gender: "X"))).Status);
        Assert.Equal(400, (await a.PostAsync("/api/ppl/students", StudentBody(note: new string('a', 501)))).Status);
    }

    [Fact]
    public async Task PPLP_12_Student_list_filters()
    {
        var a = await AdminAsync();
        var r = await a.GetAsync($"/api/ppl/students?yearId={w.YearId}&campusId={w.CampusAId}&status=Studying");
        Assert.Equal(200, r.Status);
        var bad = await a.GetAsync("/api/ppl/students?status=Unknown");
        Assert.Equal(400, bad.Status);
        Assert.Equal("Trạng thái học sinh không hợp lệ.", bad.Message);
    }

    [Fact]
    public async Task PPLP_13_To_16_Student_leaves_the_school()
    {
        var a = await AdminAsync();
        var c6 = await w.NewClassAsync(w.CampusAId, "PL6" + U(), 6);
        var c9 = await w.NewClassAsync(w.CampusAId, "PL9" + U(), 9);
        var (s1, e1) = await w.NewStudentAsync(c6, "HS rời trường " + U());
        var (s2, _) = await w.NewStudentAsync(c6, "HS rời trường 2 " + U());
        var (s9, _) = await w.NewStudentAsync(c9, "HS khối 9 " + U());
        // PPLP-15
        Assert.Equal("Lý do chỉ gồm chuyển trường hoặc tốt nghiệp.", (await a.PostAsync("/api/ppl/students/leave", new { studentIds = new[] { s1.PersonId }, reason = "ClassTransfer" })).Message);
        Assert.Equal("Chọn ít nhất một học sinh.", (await a.PostAsync("/api/ppl/students/leave", new { studentIds = Array.Empty<int>(), reason = "SchoolTransfer" })).Message);
        // PPLP-14: tốt nghiệp ở khối 6 bị chặn
        var g6 = await a.PostAsync("/api/ppl/students/leave", new { studentIds = new[] { s1.PersonId }, reason = "Graduated" });
        Assert.Equal(400, g6.Status);
        Assert.Contains("chưa phải khối cuối cấp nên không cho tốt nghiệp được.", g6.Message);
        Assert.Equal(204, (await a.PostAsync("/api/ppl/students/leave", new { studentIds = new[] { s9.PersonId }, reason = "Graduated" })).Status);
        // PPLP-16: một học sinh không hợp lệ làm hỏng cả lượt
        var left = await a.PostAsync("/api/ppl/students/leave", new { studentIds = new[] { s2.PersonId, s9.PersonId }, reason = "SchoolTransfer" });
        Assert.Equal(400, left.Status);
        Assert.Contains("không còn học hoặc chưa thuộc lớp nào. Tải lại danh sách rồi chọn lại.", left.Message);
        Assert.Null(await f.WithDbAsync(db => db.Enrollment.Where(e => e.StudentId == s2.PersonId).Select(e => e.EndDate).SingleAsync()));
        // PPLP-13: chuyển trường
        Assert.Equal(204, (await a.PostAsync("/api/ppl/students/leave", new { studentIds = new[] { s1.PersonId }, reason = "SchoolTransfer" })).Status);
        var enr = await f.WithDbAsync(db => db.Enrollment.Where(e => e.EnrollmentId == e1).Select(e => new { e.EndDate, e.Reason }).SingleAsync());
        Assert.NotNull(enr.EndDate);
        Assert.Equal("SchoolTransfer", enr.Reason);
        Assert.Equal("Transferred", await f.WithDbAsync(db => db.StudentProfile.Where(p => p.StudentId == s1.PersonId).Select(p => p.Status).SingleAsync()));
        var roster = await a.GetAsync($"/api/sch/classes/{c6}/roster?date={World.Iso(Today.AddDays(1))}");
        Assert.DoesNotContain($"\"studentId\":{s1.PersonId},", roster.Raw);
    }

    // ------------------------------------------------------------------ Phụ huynh

    [Fact]
    public async Task PPLP_19_To_21_Guardian_creation_phone_rules_and_representative_change()
    {
        var a = await AdminAsync();
        var phone = Phone();
        var g = await a.PostAsync("/api/ppl/guardians", new { fullName = "Phụ huynh PPLP " + U(), phoneNumber = phone, email = $"ph{U()}@example.com", address = "Hà Nội" });
        Assert.Equal(200, g.Status);
        var gid = g.Body!["parentId"]!.GetValue<int>();
        var cls = await w.NewClassAsync(w.CampusAId, "PG" + U(), 6);
        var (s, _) = await w.NewStudentAsync(cls, "HS phụ huynh " + U());
        // PPLP-19: gán đại diện
        Assert.True((await a.PutAsync($"/api/ppl/students/{s.PersonId}/guardian", new { existingParentId = gid, relationship = "Bố" })).Ok);
        Assert.True(await f.WithDbAsync(db => db.ParentStudentLink.AnyAsync(l => l.ParentId == gid && l.StudentId == s.PersonId && l.IsPrimary)));
        Assert.Contains(s.FullName, (await a.GetAsync($"/api/ppl/guardians/{gid}/students")).Raw);
        // PPLP-20
        var dup = await a.PostAsync("/api/ppl/guardians", new { fullName = "Trùng số " + U(), phoneNumber = phone });
        Assert.Equal(400, dup.Status);
        Assert.Contains("PH", dup.Message);
        Assert.StartsWith("Số điện thoại không được để trống", (await a.PostAsync("/api/ppl/guardians", new { fullName = "Không số " + U(), phoneNumber = "" })).Message);
        foreach (var bad in new[] { "0212345678", "091234567", "09123456789" })
            Assert.Equal("Số điện thoại di động phải gồm đúng 10 chữ số, đầu số 03, 05, 07, 08 hoặc 09.", (await a.PostAsync("/api/ppl/guardians", new { fullName = "Số sai " + U(), phoneNumber = bad })).Message);
        foreach (var prefix in new[] { "03", "05", "07", "08", "09" })
            Assert.Equal(200, (await a.PostAsync("/api/ppl/guardians", new { fullName = "Số đúng " + U(), phoneNumber = prefix + Random.Shared.NextInt64(10000000, 99999999) })).Status);
        // PPLP-21
        var other = await a.PostAsync("/api/ppl/guardians", new { fullName = "Phụ huynh khác " + U(), phoneNumber = Phone() });
        var oid = other.Body!["parentId"]!.GetValue<int>();
        var both = await a.PutAsync($"/api/ppl/students/{s.PersonId}/guardian", new { existingParentId = oid, newGuardian = new { fullName = "Mới", phoneNumber = Phone() } });
        Assert.Equal(400, both.Status);
        Assert.Equal("Chọn một phụ huynh có sẵn hoặc khai phụ huynh mới, không chọn cả hai.", both.Message);
        var same = await a.PutAsync($"/api/ppl/students/{s.PersonId}/guardian", new { existingParentId = gid });
        Assert.Equal(400, same.Status);
        Assert.Contains("đang là phụ huynh đại diện", same.Message);
        Assert.True((await a.PutAsync($"/api/ppl/students/{s.PersonId}/guardian", new { existingParentId = oid, relationship = "Mẹ" })).Ok);
        Assert.False(await f.WithDbAsync(db => db.ParentStudentLink.AnyAsync(l => l.ParentId == gid && l.StudentId == s.PersonId && l.IsPrimary)));
    }

    [Fact]
    public async Task PPLP_24_A_guardian_cannot_change_own_phone()
    {
        await w.EnsureAsync();
        var parent = await w.NewAccountAsync("PH tự sửa số " + U(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db => { (await db.Person.SingleAsync(p => p.PersonId == parent.PersonId)).PhoneNumber = Phone(); await db.SaveChangesAsync(); });
        var c = await f.LoginAsync(parent.Username);
        var r = await c.PutAsync("/api/iam/profile", new { phoneNumber = Phone() });
        Assert.Equal(400, r.Status);
        Assert.StartsWith("Số điện thoại là khóa nhận diện của phụ huynh nên không tự sửa được", r.Message);
    }

    // ------------------------------------------------------------------ Phân quyền

    [Fact]
    public async Task PPLP_25_Write_endpoints_are_for_the_school_admin_only_and_reads_follow_the_policies()
    {
        var roles = await f.LoginAllAsync();
        foreach (var role in new[] { Principal, Vice, Teacher, Student, Parent })
        {
            Assert.Equal(403, (await roles[role].PostAsync("/api/ppl/staff", new { })).Status);
            Assert.Equal(403, (await roles[role].PostAsync("/api/ppl/students", new { })).Status);
            Assert.Equal(403, (await roles[role].PostAsync("/api/ppl/guardians", new { })).Status);
        }
        await f.AssertGetByRoleAsync("/api/ppl/staff", Admin, Principal, Vice);
        await f.AssertGetByRoleAsync("/api/ppl/students", Admin, Principal, Vice);
        await f.AssertGetByRoleAsync("/api/ppl/guardians", Admin, Principal, Vice);
    }

    [Fact]
    public async Task PPLP_26_And_27_Read_only_detail_pages_follow_the_campus_and_homeroom_scope()
    {
        await w.EnsureAsync();
        var a = await f.LoginAsync(w.SchoolAdmin.Username);
        var p = await f.LoginAsync(w.Principal.Username);
        var staffId = (await w.Find("Phạm Minh Tuấn")).PersonId;
        Assert.Equal(200, (await a.GetAsync($"/api/ppl/staff/{staffId}")).Status);
        Assert.Equal(200, (await p.GetAsync($"/api/ppl/staff/{staffId}")).Status);
        var vice = await f.LoginAsync(w.ViceA.Username);
        Assert.Equal(200, (await vice.GetAsync($"/api/ppl/staff/{staffId}")).Status);
        // học sinh thuộc phân hiệu B: Hiệu phó A bị từ chối
        var clsB = await w.NewClassAsync(w.CampusBId, "PD" + U(), 6);
        var (sb, _) = await w.NewStudentAsync(clsB, "HS phân hiệu B " + U());
        var hidden = await vice.GetAsync($"/api/ppl/students/{sb.PersonId}");
        Assert.Equal(404, hidden.Status);
        Assert.Equal("Không tìm thấy hồ sơ học sinh.", hidden.Message);
        Assert.Equal(200, (await a.GetAsync($"/api/ppl/students/{sb.PersonId}")).Status);
        Assert.Equal(200, (await p.GetAsync($"/api/ppl/students/{sb.PersonId}")).Status);
        // giáo viên không chủ nhiệm lớp
        var teacher = await f.LoginAsPersonAsync("Phạm Minh Tuấn");
        Assert.Equal(404, (await teacher.GetAsync($"/api/ppl/students/{sb.PersonId}")).Status);
        Assert.Equal(403, (await (await f.LoginAsPersonAsync("Hoàng Gia Bảo")).GetAsync($"/api/ppl/students/{sb.PersonId}")).Status);
    }
}
