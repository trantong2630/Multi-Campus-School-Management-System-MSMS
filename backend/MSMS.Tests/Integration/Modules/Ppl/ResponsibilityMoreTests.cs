using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Ppl;

/// <summary>PPL-04 tiếp: lịch sử, hiệu lực tức thì của chủ nhiệm mới, phạm vi Hiệu phó theo phụ trách phân hiệu, nhật ký (RESP-10, 15, 17, 21).</summary>
[Collection("api")]
public class ResponsibilityMoreTests(ApiFactory f)
{
    private World w => f.World;
    private async Task<ApiClient> PrincipalAsync() { await w.EnsureAsync(); f.Clock.Reset(); return await f.LoginAsync(w.Principal.Username); }
    private static object Assign(string kind, int target, int staff, DateOnly start) => new { kind, targetId = target, staffId = staff, startDate = World.Iso(start) };
    private Task<Who> TeacherAsync() => w.NewStaffAsync("GV trách nhiệm " + World.Uniq(), "Teacher", w.CampusAId, null);
    private DateOnly Today => f.Clock.Today;

    [Fact]
    public async Task RESP_10_History_shows_every_record_with_the_right_status()
    {
        var p = await PrincipalAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RH" + World.Uniq(), 6);
        var tEnded = await TeacherAsync(); var tCur = await TeacherAsync(); var tFuture = await TeacherAsync();
        await f.WithDbAsync(async db =>
        {
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = cls, StaffId = tEnded.PersonId, StartDate = Today.AddDays(-60), EndDate = Today.AddDays(-30), CreatedAt = f.Clock.Now.AddDays(-60) });
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = cls, StaffId = tCur.PersonId, StartDate = Today.AddDays(-29), EndDate = Today.AddDays(9), CreatedAt = f.Clock.Now.AddDays(-29) });
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = cls, StaffId = tFuture.PersonId, StartDate = Today.AddDays(10), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var r = await p.GetAsync($"/api/ppl/responsibilities/history?kind=homeroom&targetId={cls}");
        Assert.Equal(200, r.Status);
        var rows = r.Body!.AsArray();
        Assert.Equal(3, rows.Count);
        Assert.Equal([tFuture.PersonId, tCur.PersonId, tEnded.PersonId], rows.Select(x => x!["staffId"]!.GetValue<int>()).ToArray());   // mới nhất lên trước
        Assert.Equal([false, true, false], rows.Select(x => x!["isEffective"]!.GetValue<bool>()).ToArray());
        Assert.Equal(World.Iso(Today.AddDays(-30)), rows[2]!["endDate"]!.GetValue<string>());
        Assert.Equal((await p.GetAsync("/api/ppl/responsibilities/history?kind=sai&targetId=1")).Status, 400);
    }

    [Fact]
    public async Task RESP_15_A_new_homeroom_teacher_gains_the_homeroom_features_at_once()
    {
        var p = await PrincipalAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RN" + World.Uniq(), 6);
        var (pupil, _) = await w.NewStudentAsync(cls, "HS chủ nhiệm mới " + World.Uniq());
        var parent = await w.NewAccountAsync("PH chủ nhiệm mới " + World.Uniq(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db =>
        {
            db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = pupil.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var req = await (await f.LoginAsync(parent.Username)).PostAsync("/api/par/leave-requests/bulk", new { studentId = pupil.PersonId, reason = "Ốm", days = new[] { new { date = World.Iso(Today.AddDays(1)), sessionOfDay = "AllDay", slotIds = (int[]?)null } } });
        Assert.Equal(200, req.Status);
        var id = await f.WithDbAsync(db => db.LeaveRequest.Where(l => l.StudentId == pupil.PersonId).Select(l => l.LeaveRequestId).SingleAsync());
        var t = await TeacherAsync();
        var tc = await f.LoginAsync(t.Username);
        // trước khi được gán: không có quyền chủ nhiệm
        Assert.Equal(403, (await tc.PutAsync($"/api/par/leave-requests/{id}/approve", new { decisionNote = "x" })).Status);
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, t.PersonId, Today))).Status);
        // ngay sau khi gán: hộp thư, duyệt đơn, học sinh chủ nhiệm
        var inbox = await tc.GetAsync($"/api/par/leave-requests/homeroom?classId={cls}");
        Assert.Equal(200, inbox.Status);
        Assert.Contains(id.ToString(), inbox.Raw);
        Assert.Equal(204, (await tc.PutAsync($"/api/par/leave-requests/{id}/approve", new { decisionNote = "Đồng ý" })).Status);
        var students = await tc.GetAsync("/api/par/weekly-reports/homeroom-students");
        Assert.Equal(200, students.Status);
        Assert.Contains(pupil.FullName, students.Raw);
        Assert.Equal(200, (await tc.GetAsync("/api/adm/homeroom")).Status);
    }

    [Fact]
    public async Task RESP_17_Campus_leadership_drives_the_vice_principal_scope()
    {
        var p = await PrincipalAsync();
        var campusC = await f.WithDbAsync(async db => { var c = new Campus { Code = "PHC" + World.Uniq(), Name = "Phân hiệu C " + World.Uniq(), IsActive = true, CreatedAt = f.Clock.Now }; db.Campus.Add(c); await db.SaveChangesAsync(); return c.CampusId; });
        var vpX = await w.NewStaffAsync("Hiệu phó X " + World.Uniq(), "VicePrincipal", campusC, null);
        var vpY = await w.NewStaffAsync("Hiệu phó Y " + World.Uniq(), "VicePrincipal", campusC, null);
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("campus", campusC, vpX.PersonId, Today))).Status);
        var x = await f.LoginAsync(vpX.Username);
        var camps = await x.GetAsync("/api/scd/timetable/campuses");
        Assert.Equal(200, camps.Status);
        Assert.Contains("Phân hiệu C", camps.Raw);
        Assert.DoesNotContain("Phân hiệu 2", camps.Raw);
        Assert.Equal(200, (await x.GetAsync($"/api/adm/dashboard?campusId={campusC}")).Status);
        var other = await x.GetAsync($"/api/adm/dashboard?campusId={w.CampusBId}");
        Assert.Equal(403, other.Status);
        Assert.Equal("Không có quyền xem dữ liệu của phân hiệu này.", other.Message);
        // thay Hiệu phó X bằng Y
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("campus", campusC, vpY.PersonId, Today))).Status);
        var after = await x.GetAsync($"/api/adm/dashboard?campusId={campusC}");
        Assert.Equal(403, after.Status);
        Assert.Equal("Không có quyền xem dữ liệu của phân hiệu này.", after.Message);   // hành vi thật: cùng thông báo, không có câu "Chưa được gán..."
        Assert.Equal(200, (await (await f.LoginAsync(vpY.Username)).GetAsync($"/api/adm/dashboard?campusId={campusC}")).Status);
    }

    [Fact]
    public async Task RESP_21_Responsibility_changes_are_written_to_the_audit_log_in_Vietnamese()
    {
        var p = await PrincipalAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "RA" + World.Uniq(), 6);
        var t1 = await TeacherAsync(); var t2 = await TeacherAsync(); var tFuture = await TeacherAsync();
        var since = f.Clock.Now.AddMinutes(-1);
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, t1.PersonId, Today))).Status);
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, t2.PersonId, Today))).Status);
        var cur = await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.ClassId == cls && h.StaffId == t2.PersonId).Select(h => h.Id).SingleAsync());
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/end", new { kind = "homeroom", recordId = cur, endDate = World.Iso(Today.AddDays(5)) })).Status);
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/assign", Assign("homeroom", cls, tFuture.PersonId, Today.AddDays(6)))).Status);
        var fut = await f.WithDbAsync(db => db.HomeroomAssignment.Where(h => h.ClassId == cls && h.StaffId == tFuture.PersonId).Select(h => h.Id).SingleAsync());
        Assert.Equal(204, (await p.PostAsync("/api/ppl/responsibilities/cancel", new { kind = "homeroom", recordId = fut })).Status);
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var log = await admin.GetAsync("/api/adm/audit-log?category=" + Uri.EscapeDataString("Trách nhiệm") + "&pageSize=100");
        Assert.Equal(200, log.Status);
        foreach (var action in new[] { "Gán trách nhiệm", "Thay người đảm nhiệm", "Kết thúc trách nhiệm", "Hủy lịch hẹn trách nhiệm" })
            Assert.Contains(action, log.Raw);
        var details = await f.WithDbAsync(db => db.AuditLog.Where(a => a.Category == "Trách nhiệm" && a.CreatedAt >= since && a.Detail!.Contains(t2.FullName)).Select(a => a.Detail!).ToListAsync());
        Assert.NotEmpty(details);
        Assert.Contains(details, d => d.Contains("Loại trách nhiệm") && d.Contains("Chủ nhiệm") && d.Contains(t1.FullName) && d.Contains("Người đảm nhiệm cũ"));
        Assert.Contains(details, d => d.Contains("Kết thúc ngày") && d.Contains(Today.AddDays(5).ToString("dd/MM/yyyy")));
    }
}
