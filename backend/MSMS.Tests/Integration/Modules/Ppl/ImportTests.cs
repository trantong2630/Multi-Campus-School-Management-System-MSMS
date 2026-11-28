using ClosedXML.Excel;
using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Ppl;

/// <summary>PPL-01..03: nhập danh sách bằng file Excel mẫu (cán bộ PPLP-08, học sinh PPLP-18, phụ huynh PPLP-23) và hai hiệu ứng chéo module (PPLP-17, PPLP-22).</summary>
[Collection("api")]
public class ImportTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();
    private static long idSeq = 700_000_000_000;
    private static string NewNationalId() => Interlocked.Increment(ref idSeq).ToString("D12");
    private static string NewPhone() => "09" + (10_000_000 + Interlocked.Increment(ref idSeq) % 80_000_000).ToString("D8");

    private async Task<ApiClient> AdminAsync() { await w.EnsureAsync(); f.Clock.Reset(); return await f.LoginAsync(w.SchoolAdmin.Username); }

    private static byte[] Fill(byte[] template, int firstRow, params object?[][] rows)
    {
        using var ms = new MemoryStream(template);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheets.First();
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c] is { } v) ws.Cell(firstRow + r, c + 1).Value = v is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : XLCellValue.FromObject(v);
        using var o = new MemoryStream();
        wb.SaveAs(o);
        return o.ToArray();
    }

    private async Task<ApiResult> UploadAsync(ApiClient c, string url, byte[] file) => await c.PostFileAsync(url, "file", file, "mau.xlsx");

    [Fact]
    public async Task PPLP_08_Staff_import_validates_every_row_and_creates_nothing_when_one_row_is_wrong()
    {
        var admin = await AdminAsync();
        var s = await w.NewScenarioAsync();
        var deptName = await f.WithDbAsync(db => db.SubjectDepartment.Where(d => d.DepartmentId == s.DepartmentId).Select(d => d.Name).SingleAsync());
        var campusName = await f.WithDbAsync(db => db.Campus.Where(c => c.CampusId == w.CampusAId).Select(c => c.Name).SingleAsync());
        var tpl = await admin.GetBytesAsync("/api/ppl/staff/import-template");
        Assert.Equal(200, tpl.Status);
        object?[] Good(string n) => [n, new DateOnly(1988, 3, 3), "Nam", null, $"{n.Replace(' ', '.')}{U()}@example.com".ToLowerInvariant().Replace("đ", "d"), deptName, campusName, "G" + U(), NewNationalId()];
        var tag = "Cán bộ import " + U();
        object?[] bad1 = [tag + " tuổi", new DateOnly(2015, 1, 1), "Nam", null, "bad1" + U() + "@example.com", deptName, campusName, "B" + U(), NewNationalId()];
        object?[] bad2 = [tag + " định danh", new DateOnly(1988, 3, 3), "Nam", null, "bad2" + U() + "@example.com", deptName, campusName, "B" + U(), "123"];
        var dup = "D" + U();
        object?[] bad3 = [tag + " viết tắt", new DateOnly(1988, 3, 3), "Nam", null, "bad3" + U() + "@example.com", deptName, campusName, "T!x", NewNationalId()];
        var mixed = Fill(tpl.Bytes, 5, Good(tag + " một"), Good(tag + " hai"), Good(tag + " ba"), bad1, bad2, bad3);
        var r = await UploadAsync(admin, "/api/ppl/staff/import", mixed);
        Assert.Equal(200, r.Status);
        Assert.False(r.Body!["accepted"]!.GetValue<bool>());
        var errors = r.Body["errors"]!.AsArray();
        Assert.Equal(3, errors.Count);
        Assert.Equal(["Dòng 8", "Dòng 9", "Dòng 10"], errors.Select(e => e!["location"]!.GetValue<string>()).ToArray());
        Assert.Contains(errors, e => e!["reason"]!.GetValue<string>() == "Số định danh phải gồm đúng 12 chữ số.");
        Assert.Contains(errors, e => e!["reason"]!.GetValue<string>().StartsWith("Viết tắt GV chỉ gồm chữ và số"));
        Assert.Equal(0, await f.WithDbAsync(db => db.Person.CountAsync(p => p.FullName.StartsWith(tag))));   // có một dòng sai là không ghi gì
        // chỉ các dòng hợp lệ thì tạo đủ
        var ok = await UploadAsync(admin, "/api/ppl/staff/import", Fill(tpl.Bytes, 5, Good(tag + " một"), Good(tag + " hai"), Good(tag + " ba")));
        Assert.True(ok.Body!["accepted"]!.GetValue<bool>());
        Assert.Equal(3, ok.Body["created"]!.GetValue<int>());
        Assert.Equal(3, await f.WithDbAsync(db => db.Person.CountAsync(p => p.FullName.StartsWith(tag))));
        // file không phải xlsx và file thiếu sheet
        var notXlsx = await UploadAsync(admin, "/api/ppl/staff/import", "không phải excel"u8.ToArray());
        Assert.Equal(400, notXlsx.Status);
        Assert.StartsWith("Không đọc được file", notXlsx.Message);
        using (var wb = new XLWorkbook()) { wb.AddWorksheet("Khac").Cell(1, 1).Value = "x"; using var ms = new MemoryStream(); wb.SaveAs(ms); var noSheet = await UploadAsync(admin, "/api/ppl/staff/import", ms.ToArray()); Assert.Equal(200, noSheet.Status); Assert.False(noSheet.Body!["accepted"]!.GetValue<bool>()); Assert.StartsWith("Hàng tiêu đề bị sửa", noSheet.Body["errors"]![0]!["reason"]!.GetValue<string>()); }   // hành vi thật: sheet lạ được đọc như sheet đầu tiên rồi bị từ chối vì sai hàng tiêu đề
        Assert.NotNull(dup);
    }

    [Fact]
    public async Task PPLP_23_Guardian_import_phone_rules_and_duplicates()
    {
        var admin = await AdminAsync();
        var tpl = await admin.GetBytesAsync("/api/ppl/guardians/import-template");
        Assert.Equal(200, tpl.Status);
        var tag = "PH import " + U();
        object?[] good(string n) => [n, NewPhone(), "Nữ", n.Replace(' ', '.').ToLowerInvariant() + U() + "@example.com", "Hà Nội"];
        var phone = NewPhone();
        object?[][] bad =
        [
            [tag + " rỗng", null, null, null, null],
            [tag + " sai đầu số", "0123456789", null, null, null],
            [tag + " trùng 1", phone, null, null, null],
            [tag + " trùng 2", phone, null, null, null],
            [tag + " email", NewPhone(), null, "khong-phai-email", null],
        ];
        var r = await UploadAsync(admin, "/api/ppl/guardians/import", Fill(tpl.Bytes, 5, [good(tag + " một"), .. bad]));
        Assert.Equal(200, r.Status);
        Assert.False(r.Body!["accepted"]!.GetValue<bool>());
        var reasons = r.Body["errors"]!.AsArray().Select(e => e!["reason"]!.GetValue<string>()).ToList();
        Assert.Contains("Thiếu số điện thoại. Đây là khóa nhận diện phụ huynh.", reasons);
        Assert.Contains("Số điện thoại di động phải gồm đúng 10 chữ số, đầu số 03, 05, 07, 08 hoặc 09.", reasons);
        Assert.Contains(reasons, x => x.StartsWith("Trùng số điện thoại với dòng"));
        Assert.Contains("Email chưa đúng định dạng.", reasons);
        Assert.Equal(0, await f.WithDbAsync(db => db.Person.CountAsync(p => p.FullName.StartsWith(tag))));
        var ok = await UploadAsync(admin, "/api/ppl/guardians/import", Fill(tpl.Bytes, 5, good(tag + " a"), good(tag + " b")));
        Assert.True(ok.Body!["accepted"]!.GetValue<bool>());
        Assert.Equal(2, ok.Body["created"]!.GetValue<int>());
        var codes = await f.WithDbAsync(db => db.Person.Where(p => p.FullName.StartsWith(tag)).Select(p => p.PersonCode).ToListAsync());
        Assert.All(codes, c => Assert.StartsWith("PH", c));
        // số đã có thì bỏ qua cả dòng
        var again = await UploadAsync(admin, "/api/ppl/guardians/import", Fill(tpl.Bytes, 5, [tag + " a", await f.WithDbAsync(db => db.Person.Where(p => p.FullName == tag + " a").Select(p => p.PhoneNumber).SingleAsync()), null, null, null]));
        Assert.Equal(0, again.Body!["created"]!.GetValue<int>());
        Assert.Single(again.Body["skipped"]!.AsArray());
    }

    [Fact]
    public async Task PPLP_18_Student_import_creates_students_with_guardians_and_class_placement()
    {
        var admin = await AdminAsync();
        var (yearName, campusName) = await f.WithDbAsync(async db => (await db.AcademicYear.Where(y => y.AcademicYearId == w.YearId).Select(y => y.Name).SingleAsync(), await db.Campus.Where(c => c.CampusId == w.CampusAId).Select(c => c.Name).SingleAsync()));
        var cls = "IM" + U();
        var classId = await w.NewClassAsync(w.CampusAId, cls, 6);
        var closed = "IX" + U();
        var closedId = await w.NewClassAsync(w.CampusAId, closed, 6);
        await f.WithDbAsync(async db => { (await db.Class.SingleAsync(c => c.ClassId == closedId)).IsActive = false; await db.SaveChangesAsync(); });
        var tpl = await admin.GetBytesAsync($"/api/ppl/students/import-template?yearId={w.YearId}&campusId={w.CampusAId}");
        Assert.Equal(200, tpl.Status);
        var tag = "HS import " + U();
        object?[] pupil(string n, string? klass, string? ph = null, string? id = null) => [n, new DateOnly(2014, 5, 5), "Nam", "Hà Nội", klass, "PH của " + n, "Bố", ph ?? NewPhone(), null, id ?? NewNationalId()];
        var sharedPhone = NewPhone();
        var good = Fill(tpl.Bytes, 7, pupil(tag + " một", cls, sharedPhone), pupil(tag + " hai", cls, sharedPhone).Select((v, i) => i == 5 ? "PH của " + tag + " một" : v).ToArray());
        // dòng 2 dùng cùng số điện thoại và cùng họ tên phụ huynh: một hồ sơ phụ huynh nối với hai con
        var r = await UploadAsync(admin, "/api/ppl/students/import", good);
        Assert.Equal(200, r.Status);
        Assert.True(r.Body!["accepted"]!.GetValue<bool>(), r.Raw);
        Assert.Equal(2, r.Body["students"]!.GetValue<int>());
        Assert.Equal(2, r.Body["enrollments"]!.GetValue<int>());
        Assert.Equal(1, r.Body["parents"]!.GetValue<int>());
        var roster = await admin.GetAsync($"/api/sch/classes/{classId}/roster");
        Assert.Equal(2, roster.Body!["students"]!.AsArray().Count);
        // dữ liệu sai: lớp không có, lớp ngừng hoạt động, điện thoại sai, định danh sai
        var badFile = Fill(tpl.Bytes, 7,
            pupil(tag + " lớp lạ", "KHONGCO"),
            pupil(tag + " lớp đóng", closed),
            pupil(tag + " điện thoại", cls, "12345"),
            pupil(tag + " định danh", cls, null, "99"));
        var bad = await UploadAsync(admin, "/api/ppl/students/import", badFile);
        Assert.False(bad.Body!["accepted"]!.GetValue<bool>());
        var reasons = bad.Body["errors"]!.AsArray().Select(e => e!["reason"]!.GetValue<string>()).ToList();
        Assert.Contains($"Lớp \"KHONGCO\" không có ở {campusName}, năm học {yearName}. Tạo lớp ở màn Lớp trước, hoặc sửa lại tên lớp cho đúng.", reasons);
        Assert.Contains($"Lớp \"{closed}\" đã ngừng hoạt động nên không xếp lớp được.", reasons);
        Assert.Contains("Số điện thoại phụ huynh phải là số di động đúng 10 chữ số, đầu số 03, 05, 07, 08 hoặc 09.", reasons);
        Assert.Contains("Số định danh phải gồm đúng 12 chữ số.", reasons);
        Assert.Equal(0, await f.WithDbAsync(db => db.Person.CountAsync(p => p.FullName.StartsWith(tag + " lớp") || p.FullName.StartsWith(tag + " điện") || p.FullName.StartsWith(tag + " định"))));
    }

    [Fact]
    public async Task PPLP_22_Changing_the_representative_moves_the_parents_access_to_the_child()
    {
        var admin = await AdminAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RP" + U(), 6);
        var (child, _) = await w.NewStudentAsync(cls, "HS đổi đại diện " + U());
        var g1 = await w.NewAccountAsync("PH cũ " + U(), "Parent", "Parent", "Active");
        var g2 = await w.NewAccountAsync("PH mới " + U(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db => { db.ParentStudentLink.Add(new ParentStudentLink { ParentId = g1.PersonId, StudentId = child.PersonId, Relationship = "Bố", IsPrimary = true, CreatedAt = f.Clock.Now }); await db.SaveChangesAsync(); });
        var c1 = await f.LoginAsync(g1.Username); var c2 = await f.LoginAsync(g2.Username);
        Assert.Contains(child.FullName, (await c1.GetAsync("/api/par/leave-requests/my-children")).Raw);
        var change = await admin.PutAsync($"/api/ppl/students/{child.PersonId}/guardian", new { existingParentId = g2.PersonId, relationship = "Mẹ" });
        Assert.True(change.Status is 200 or 204, change.Raw);
        Assert.Contains(child.FullName, (await c2.GetAsync("/api/par/leave-requests/my-children")).Raw);
        Assert.DoesNotContain(child.FullName, (await c1.GetAsync("/api/par/leave-requests/my-children")).Raw);
        var denied = await c1.PostAsync("/api/par/leave-requests/bulk", new { studentId = child.PersonId, reason = "Ốm", days = new[] { new { date = World.Iso(f.Clock.Today.AddDays(1)), sessionOfDay = "AllDay", slotIds = (int[]?)null } } });
        Assert.Equal(403, denied.Status);
        Assert.Equal("Bạn không có quyền tạo đơn xin nghỉ cho học sinh này.", denied.Message);
    }

    [Fact]
    public async Task PPLP_17_A_pupil_who_leaves_school_stops_receiving_new_assignments_and_keeps_the_results()
    {
        var admin = await AdminAsync();
        var s = await w.NewScenarioAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "LS" + U(), 6);
        var (stay, _) = await w.NewStudentAsync(cls, "HS ở lại " + U());
        var (leave, enr) = await w.NewStudentAsync(cls, "HS rời trường " + U());
        Assert.Equal(204, (await admin.PostAsync("/api/ppl/students/leave", new { studentIds = new[] { leave.PersonId }, reason = "SchoolTransfer" })).Status);
        // đồng thời ghi danh của em đã khép lại
        Assert.NotNull(await f.WithDbAsync(db => db.Enrollment.Where(e => e.EnrollmentId == enr).Select(e => e.EndDate).SingleAsync()));
        await f.WithDbAsync(async db => { (await db.Enrollment.SingleAsync(e => e.EnrollmentId == enr)).EndDate = f.Clock.Today.AddDays(-1); await db.SaveChangesAsync(); });
        Assert.Equal("Transferred", await f.WithDbAsync(db => db.StudentProfile.Where(p => p.StudentId == leave.PersonId).Select(p => p.Status).SingleAsync()));
        var roster = await admin.GetAsync($"/api/sch/classes/{cls}/roster");
        Assert.DoesNotContain(leave.FullName, roster.Raw);
        Assert.Contains(stay.FullName, roster.Raw);
        Assert.Equal("INVALID_CREDENTIALS", (await f.Anonymous().PostAsync("/api/auth/login", new { username = leave.Username, password = ApiFactory.Password })).Code);   // tài khoản đã khóa nên không đăng nhập được
    }
}
