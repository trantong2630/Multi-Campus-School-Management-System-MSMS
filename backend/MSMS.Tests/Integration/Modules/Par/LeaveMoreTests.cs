using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Par;

/// <summary>LEV-06: gửi nhiều ngày một lần; LEV-09: phụ huynh rút đơn đang chờ.</summary>
[Collection("api")]
public class LeaveMoreTests(ApiFactory f)
{
    private World w => f.World;

    private async Task<(int ClassId, Who Student, Who Parent, Who Homeroom)> FamilyAsync()
    {
        f.Clock.Reset();
        await w.EnsureAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "LM" + World.Uniq(), 6);
        var hr = await w.NewStaffAsync("GVCN " + World.Uniq(), "Teacher", w.CampusAId, null);
        var (student, _) = await w.NewStudentAsync(cls, "HS nhiều ngày " + World.Uniq());
        var parent = await w.NewAccountAsync("PH " + World.Uniq(), "Parent", "Parent", "Active");
        await f.WithDbAsync(async db =>
        {
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = cls, StaffId = hr.PersonId, StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = student.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        return (cls, student, parent, hr);
    }

    private static object Day(DateOnly d) => new { date = World.Iso(d), sessionOfDay = "AllDay", slotIds = (int[]?)null };

    [Fact]
    public async Task LEV_06_Bulk_submission_for_selected_days_is_all_or_nothing()
    {
        var fam = await FamilyAsync();
        var p = await f.LoginAsync(fam.Parent.Username);
        var d = w.NextMonday();
        Func<object[], Task<ApiResult>> send = days => p.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, reason = "Ốm", days });
        Assert.Equal(200, (await send([Day(d), Day(d.AddDays(1)), Day(d.AddDays(2))])).Status);
        Assert.Equal(3, await f.WithDbAsync(db => db.LeaveRequest.CountAsync(l => l.StudentId == fam.Student.PersonId)));
        var past = f.Clock.Today.AddDays(-1);
        var rPast = await send([Day(d.AddDays(7)), Day(past)]);
        Assert.Equal(400, rPast.Status);
        Assert.Equal($"Ngày {past:dd/MM/yyyy} đã qua, không thể xin nghỉ.", rPast.Message);
        var rDup = await send([Day(d.AddDays(8)), Day(d.AddDays(8))]);
        Assert.Equal(400, rDup.Status);
        Assert.Contains("xung đột với đơn xin nghỉ đã có hoặc với dòng khác trong đơn.", rDup.Message);
        var rOld = await send([Day(d.AddDays(9)), Day(d)]);
        Assert.Equal(400, rOld.Status);
        Assert.Contains("xung đột với đơn xin nghỉ đã có", rOld.Message);
        var empty = await send([]);
        Assert.Equal(400, empty.Status);
        Assert.Equal("Chọn ít nhất một ngày nghỉ.", empty.Message);
        Assert.Equal(3, await f.WithDbAsync(db => db.LeaveRequest.CountAsync(l => l.StudentId == fam.Student.PersonId)));   // các lần lỗi không tạo đơn nào
    }

    [Fact]
    public async Task LEV_09_Parent_withdraws_a_pending_request()
    {
        var fam = await FamilyAsync();
        var p = await f.LoginAsync(fam.Parent.Username);
        var d = f.Clock.Today.AddDays(1);
        Assert.Equal(200, (await p.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, reason = "Ốm", days = new[] { Day(d) } })).Status);
        var id = await f.WithDbAsync(db => db.LeaveRequest.Where(l => l.StudentId == fam.Student.PersonId).Select(l => l.LeaveRequestId).SingleAsync());
        var hr = await f.LoginAsync(fam.Homeroom.Username);
        Assert.Contains(id.ToString(), (await hr.GetAsync($"/api/par/leave-requests/homeroom?classId={fam.ClassId}")).Raw);
        Assert.Equal(204, (await p.PutAsync($"/api/par/leave-requests/{id}/withdraw")).Status);
        Assert.Equal("Withdrawn", await f.WithDbAsync(db => db.LeaveRequest.Where(l => l.LeaveRequestId == id).Select(l => l.Status).SingleAsync()));
        var inbox = (await hr.GetAsync($"/api/par/leave-requests/homeroom?classId={fam.ClassId}")).Body!["items"]!.AsArray().Single(x => x!["leaveRequestId"]!.GetValue<int>() == id)!;
        Assert.Equal("Withdrawn", inbox["status"]!.GetValue<string>());   // hành vi thật: đơn rút vẫn nằm trong hộp thư với trạng thái Đã rút, không còn chờ duyệt
        // đơn đã rút không còn xung đột: gửi lại đúng ngày đó được
        Assert.Equal(200, (await p.PostAsync("/api/par/leave-requests/bulk", new { studentId = fam.Student.PersonId, reason = "Ốm", days = new[] { Day(d) } })).Status);
    }
}
