using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Iam;

/// <summary>ACC-07, ACC-10, ACC-14 (hiệu ứng chéo module của tài khoản) và AUTH-10 (refresh token hết hạn).</summary>
[Collection("api")]
public class AccountMoreTests(ApiFactory f)
{
    private World w => f.World;
    private async Task<ApiClient> AdminAsync() { await w.EnsureAsync(); f.Clock.Reset(); return await f.LoginAsync(w.SchoolAdmin.Username); }
    private Task<int> AccountIdOf(int personId) => f.WithDbAsync(db => db.Account.Where(a => a.PersonId == personId).Select(a => a.AccountId).SingleAsync());

    [Fact]
    public async Task ACC_07_A_new_parent_account_sees_only_the_linked_child()
    {
        var admin = await AdminAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "A7" + World.Uniq(), 6);
        var (child, _) = await w.NewStudentAsync(cls, "HS con ACC07 " + World.Uniq());
        var (stranger, _) = await w.NewStudentAsync(cls, "HS lạ ACC07 " + World.Uniq());
        var personId = await f.WithDbAsync(async db =>
        {
            var p = new Person { PersonType = "Parent", PersonCode = "PH7" + World.Uniq(), FullName = "PH mới ACC07 " + World.Uniq(), DateOfBirth = new DateOnly(1985, 1, 1), Gender = "Female", CreatedAt = f.Clock.Now };
            db.Person.Add(p);
            await db.SaveChangesAsync();
            db.ParentStudentLink.Add(new ParentStudentLink { ParentId = p.PersonId, StudentId = child.PersonId, Relationship = "Mẹ", IsPrimary = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
            return p.PersonId;
        });
        var created = await admin.PostAsync("/api/iam/accounts", new { personId, accountType = "Parent" });
        Assert.Equal(200, created.Status);
        var user = created.Body!["username"]!.GetValue<string>();
        var temp = created.Body["tempPassword"]!.GetValue<string>();
        var parent = await f.LoginAsync(user, temp);
        Assert.Equal(204, (await parent.PostAsync("/api/auth/change-password", new { currentPassword = temp, newPassword = "Mat-khau-moi-1" })).Status);
        var again = await f.LoginAsync(user, "Mat-khau-moi-1");
        var kids = await again.GetAsync("/api/par/leave-requests/my-children");
        Assert.Equal(200, kids.Status);
        Assert.Contains(child.FullName, kids.Raw);
        Assert.DoesNotContain(stranger.FullName, kids.Raw);
    }

    [Fact]
    public async Task ACC_10_A_locked_homeroom_teacher_loses_the_duty_and_unlocking_does_not_restore_it()
    {
        var admin = await AdminAsync();
        var cls = await w.NewClassAsync(w.CampusAId, "A10" + World.Uniq(), 6);
        var (pupil, _) = await w.NewStudentAsync(cls, "HS ACC10 " + World.Uniq());
        var parent = await w.NewAccountAsync("PH ACC10 " + World.Uniq(), "Parent", "Parent", "Active");
        var t = await w.NewStaffAsync("GVCN ACC10 " + World.Uniq(), "Teacher", w.CampusAId, null);
        await f.WithDbAsync(async db =>
        {
            db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = cls, StaffId = t.PersonId, StartDate = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2027, 5, 31), CreatedAt = f.Clock.Now });
            db.ParentStudentLink.Add(new ParentStudentLink { ParentId = parent.PersonId, StudentId = pupil.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        Assert.Equal(200, (await (await f.LoginAsync(parent.Username)).PostAsync("/api/par/leave-requests/bulk", new { studentId = pupil.PersonId, reason = "Ốm", days = new[] { new { date = World.Iso(f.Clock.Today.AddDays(1)), sessionOfDay = "AllDay", slotIds = (int[]?)null } } })).Status);
        var id = await f.WithDbAsync(db => db.LeaveRequest.Where(l => l.StudentId == pupil.PersonId).Select(l => l.LeaveRequestId).SingleAsync());
        Assert.Equal(204, (await (await f.LoginAsync(t.Username)).PostAsync("/api/auth/change-password", new { currentPassword = ApiFactory.Password, newPassword = "Mat-khau-moi-1" })).Status);
        var acc = await AccountIdOf(t.PersonId);
        Assert.Equal(204, (await admin.PutAsync($"/api/iam/accounts/{acc}/lock")).Status);
        Assert.Equal(204, (await admin.PutAsync($"/api/iam/accounts/{acc}/unlock")).Status);
        var tc = await f.LoginAsync(t.Username, "Mat-khau-moi-1");
        var approve = await tc.PutAsync($"/api/par/leave-requests/{id}/approve", new { decisionNote = "x" });
        Assert.Equal(403, approve.Status);
        Assert.Equal("Bạn không phải là giáo viên chủ nhiệm hiện tại của lớp này.", approve.Message);
        Assert.Equal(403, (await tc.GetAsync($"/api/par/leave-requests/homeroom?classId={cls}")).Status);
    }

    [Fact]
    public async Task ACC_14_Locking_a_student_account_does_not_touch_the_roster_or_the_attendance_sheet()
    {
        var admin = await AdminAsync();
        var s = await w.NewScenarioAsync();
        var date = w.NextMonday();
        var sid = await w.NewSessionAsync(s.ClassAId, s.SubjectId, s.Absent.PersonId, date, 1);
        var (pupil, _) = await w.NewStudentAsync(s.ClassAId, "HS khóa ACC14 " + World.Uniq());
        var teacher = await f.LoginAsync(s.Absent.Username);
        var stuClient = await f.LoginAsync(pupil.Username);
        Assert.Equal(204, (await admin.PutAsync($"/api/iam/accounts/{await AccountIdOf(pupil.PersonId)}/lock")).Status);
        Assert.Equal(401, (await stuClient.GetAsync("/api/auth/me")).Status);   // phiên bị thu hồi
        var roster = await admin.GetAsync($"/api/sch/classes/{s.ClassAId}/roster");
        Assert.Contains(pupil.FullName, roster.Raw);
        f.Clock.Set(date.ToDateTime(new TimeOnly(7, 0)));
        try
        {
            var sheet = await teacher.GetAsync($"/api/att/sessions/{sid}");
            Assert.Equal(200, sheet.Status);
            Assert.Contains(pupil.FullName, sheet.Raw);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task AUTH_10_A_refresh_token_past_its_expiry_is_rejected()
    {
        await w.EnsureAsync();
        var u = await w.NewAccountAsync("Người dùng AUTH10 " + World.Uniq(), "Staff", "Teacher", "Active");
        var login = await f.Anonymous().PostAsync("/api/auth/login", new { username = u.Username, password = ApiFactory.Password, clientType = "mobile" });
        Assert.Equal(200, login.Status);
        var rt = login.Body!["refreshToken"]!.GetValue<string>();
        await f.WithDbAsync(async db =>
        {
            var acc = await db.Account.Where(a => a.PersonId == u.PersonId).Select(a => a.AccountId).SingleAsync();
            foreach (var s in await db.AccountSession.Where(x => x.AccountId == acc).ToListAsync()) s.ExpiresAt = f.Clock.Now.AddDays(-1);
            await db.SaveChangesAsync();
        });
        var r = await f.Anonymous().PostAsync("/api/auth/refresh", new { refreshToken = rt });
        Assert.Equal(401, r.Status);
    }
}
