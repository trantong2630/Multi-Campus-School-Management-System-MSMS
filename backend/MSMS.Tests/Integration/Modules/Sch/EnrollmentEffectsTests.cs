using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Sch;

/// <summary>SCH-04 và các module khác: chuyển lớp đổi đối tượng điểm danh (ENR-20), người nhận bài (ENR-21) và hộp thư chủ nhiệm (ENR-23).</summary>
[Collection("api")]
public class EnrollmentEffectsTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();

    [Fact]
    public async Task ENR_20_A_transfer_changes_the_attendance_audience_from_the_transfer_date()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var s = await w.NewScenarioAsync();
        var d = w.NextMonday();
        var (s1, enr) = await w.NewStudentAsync(s.ClassAId, "HS chuyển điểm danh " + U());
        var classB = await w.NewClassAsync(w.CampusAId, "CB" + U(), 6);
        Assert.Equal(204, (await admin.PostAsync($"/api/sch/enrollments/{enr}/transfer", new { toClassId = classB, effectiveDate = World.Iso(d) })).Status);
        var before = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, d.AddDays(-7), 1);
        var aOnD = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, d, 1);
        var bOnD = await w.NewSessionAsync(classB, s.SubjectId, s.Absent.PersonId, d, 2);
        var teacher = await f.LoginAsync(s.Absent.Username);
        async Task<string> SheetAsync(long id, DateOnly day)
        {
            f.Clock.Set(day.ToDateTime(new TimeOnly(7, 5)));
            var r = await teacher.GetAsync($"/api/att/sessions/{id}");
            Assert.Equal(200, r.Status);
            return r.Raw;
        }
        try
        {
            Assert.Contains(s1.FullName, await SheetAsync(before, d.AddDays(-7)));
            Assert.DoesNotContain(s1.FullName, await SheetAsync(aOnD, d));
            f.Clock.Set(d.ToDateTime(new TimeOnly(8, 5)));
            var sheetB = await teacher.GetAsync($"/api/att/sessions/{bOnD}");
            Assert.Equal(200, sheetB.Status);
            Assert.Contains(s1.FullName, sheetB.Raw);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task ENR_23_A_leave_request_stays_with_the_class_it_was_written_for_and_only_that_homeroom_teacher_decides()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var clsA = await w.NewClassAsync(w.CampusAId, "H1" + U(), 6);
        var clsB = await w.NewClassAsync(w.CampusAId, "H2" + U(), 6);
        var hrA = await w.NewStaffAsync("GVCN A " + U(), "Teacher", w.CampusAId, null);
        var hrB = await w.NewStaffAsync("GVCN B " + U(), "Teacher", w.CampusAId, null);
        var (s1, enr) = await w.NewStudentAsync(clsA, "HS đổi lớp " + U());
        var parent = await w.NewAccountAsync("PH đổi lớp " + U(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db =>
        {
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = clsA, StaffId = hrA.PersonId, StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = clsB, StaffId = hrB.PersonId, StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = s1.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var day = f.Clock.Today.AddDays(2);
        Assert.Equal(200, (await (await f.LoginAsync(parent.Username)).PostAsync("/api/par/leave-requests/bulk", new { studentId = s1.PersonId, reason = "Ốm", days = new[] { new { date = World.Iso(day), sessionOfDay = "AllDay", slotIds = (int[]?)null } } })).Status);
        var id = await f.WithDbAsync(db => db.LeaveRequest.Where(l => l.StudentId == s1.PersonId).Select(l => l.LeaveRequestId).SingleAsync());
        // S_1 chuyển sang lớp B từ hôm nay
        await f.WithDbAsync(async db =>
        {
            (await db.Enrollment.SingleAsync(e => e.EnrollmentId == enr)).EndDate = f.Clock.Today.AddDays(-1);
            db.Enrollment.Add(new Enrollment { StudentId = s1.PersonId, ClassId = clsB, StartDate = f.Clock.Today, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var a = await f.LoginAsync(hrA.Username); var b = await f.LoginAsync(hrB.Username);
        var owner = await f.WithDbAsync(db => db.LeaveRequest.Where(l => l.LeaveRequestId == id).Select(l => l.ClassId).SingleAsync());
        Assert.Equal(clsA, owner);   // đơn gắn với lớp lúc gửi, không đổi theo học sinh
        Assert.Contains(id.ToString(), (await a.GetAsync($"/api/par/leave-requests/homeroom?classId={clsA}")).Raw);
        Assert.Equal(403, (await b.GetAsync($"/api/par/leave-requests/homeroom?classId={clsA}")).Status);
        var wrong = await b.PutAsync($"/api/par/leave-requests/{id}/approve", new { decisionNote = "x" });
        Assert.Equal(403, wrong.Status);
        Assert.Equal(204, (await a.PutAsync($"/api/par/leave-requests/{id}/approve", new { decisionNote = "ok" })).Status);
        Assert.NotNull(admin);
    }
}
