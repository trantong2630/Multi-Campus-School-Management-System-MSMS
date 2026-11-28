using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Scd;

/// <summary>ADJ-14: quyền xóa khai báo vắng thuộc tổ trưởng đương nhiệm, không thuộc người đã khai.</summary>
[Collection("api")]
public class AbsenceDeleteTests(ApiFactory f)
{
    private World w => f.World;

    [Fact]
    public async Task ADJ_14_Only_a_current_head_of_the_absent_teachers_department_may_delete_the_absence()
    {
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var other = await w.NewScenarioAsync();   // tổ trưởng của tổ khác
        var date = w.NextMonday();
        await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 1);
        var head = await f.LoginAsync(s.Head.Username);
        var declared = await head.PostAsync("/api/scd/absences/department", new { teacherId = s.Absent.PersonId, fromDate = World.Iso(date), toDate = World.Iso(date), reason = "Ốm" });
        Assert.Equal(200, declared.Status);
        var id = declared.Body!["absenceId"]!.GetValue<int>();
        // T_DH2 trở thành tổ trưởng sau; T_DH hết nhiệm kỳ
        await f.WithDbAsync(async db =>
        {
            (await db.DepartmentHead.SingleAsync(h => h.StaffId == s.Head.PersonId)).EndDate = f.Clock.Today.AddDays(-1);
            db.DepartmentHead.Add(new DepartmentHead { DepartmentId = s.DepartmentId, StaffId = s.Sub.PersonId, StartDate = f.Clock.Today, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        const string msg = "Chỉ tổ trưởng đương nhiệm của tổ giáo viên vắng mới xóa được khai báo.";
        var ended = await head.DeleteAsync($"/api/scd/absences/department/{id}");
        Assert.Equal(403, ended.Status); Assert.Equal(msg, ended.Message);
        var otherHead = await (await f.LoginAsync(other.Head.Username)).DeleteAsync($"/api/scd/absences/department/{id}");
        Assert.Equal(403, otherHead.Status); Assert.Equal(msg, otherHead.Message);
        var vp = await (await f.LoginAsync(w.ViceA.Username)).DeleteAsync($"/api/scd/absences/department/{id}");
        Assert.Equal(403, vp.Status);
        var current = await f.LoginAsync(s.Sub.Username);
        var ok = await current.DeleteAsync($"/api/scd/absences/department/{id}");
        Assert.Equal(200, ok.Status);
        Assert.False(await f.WithDbAsync(db => db.TeacherAbsence.AnyAsync(a => a.AbsenceId == id)));
        var missing = await current.DeleteAsync("/api/scd/absences/department/987654321");
        Assert.Equal(404, missing.Status);
        Assert.Equal("Không tìm thấy khai báo vắng.", missing.Message);
    }
}
