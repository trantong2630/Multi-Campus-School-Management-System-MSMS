using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.E2e;

/// <summary>E2E-02: cán bộ và học sinh mới đi từ hồ sơ tới lần đăng nhập đầu tiên, qua PPL, IAM, SCH, PAR và SCD.</summary>
[Collection("api")]
public class NewPeopleFlowTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();
    private static string NatId() => "08" + (1_000_000_000L + long.Parse(U()) * 7907 + Random.Shared.NextInt64(1, 999_999)).ToString()[..10];
    private static string Phone() => "09" + Random.Shared.NextInt64(10_000_000, 99_999_999);

    [Fact]
    public async Task E2E_02_New_staff_and_student_from_profile_to_first_login()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var today = f.Clock.Today;
        // 1. Hồ sơ cán bộ, học sinh, phụ huynh đại diện
        var staff = await admin.PostAsync("/api/ppl/staff", new { fullName = "GV mới E2E " + U(), dateOfBirth = "1990-04-04", nationalId = NatId(), gender = "Nam", phoneNumber = Phone(), email = $"gv{U()}@example.com", primaryCampusId = w.CampusAId, teacherAbbr = "N" + U(), position = "Giáo viên" });
        Assert.Equal(200, staff.Status);
        var staffId = staff.Body!["staffId"]!.GetValue<int>();
        var pupil = await admin.PostAsync("/api/ppl/students", new { fullName = "HS mới E2E " + U(), dateOfBirth = World.Iso(today.AddYears(-12)), nationalId = NatId(), gender = "Nam", address = "Hà Nội" });
        Assert.Equal(200, pupil.Status);
        var pupilId = pupil.Body!["studentId"]!.GetValue<int>();
        var guardian = await admin.PostAsync("/api/ppl/guardians", new { fullName = "PH mới E2E " + U(), phoneNumber = Phone(), email = $"ph{U()}@example.com", address = "Hà Nội" });
        Assert.Equal(200, guardian.Status);
        var guardianId = guardian.Body!["parentId"]!.GetValue<int>();
        Assert.True((await admin.PutAsync($"/api/ppl/students/{pupilId}/guardian", new { existingParentId = guardianId, relationship = "Mẹ" })).Ok);
        // 2. Tài khoản
        var tAcc = await admin.PostAsync("/api/iam/accounts", new { personId = staffId, accountType = "Teacher" });
        var pAcc = await admin.PostAsync("/api/iam/accounts", new { personId = guardianId, accountType = "Parent" });
        var sAcc = await admin.PostAsync("/api/iam/accounts", new { personId = pupilId, accountType = "Student" });
        Assert.All(new[] { tAcc, pAcc, sAcc }, r => Assert.Equal(200, r.Status));
        Assert.All(new[] { staffId, guardianId, pupilId }, id => Assert.Equal("PendingPasswordChange", f.WithDbAsync(db => db.Account.Where(a => a.PersonId == id).Select(a => a.Status).SingleAsync()).Result));
        // 3. Xếp lớp
        var cls = await w.NewClassAsync(w.CampusAId, "E2" + U(), 6);
        Assert.Equal(204, (await admin.PostAsync($"/api/sch/classes/{cls}/enrollments", new { studentId = pupilId, startDate = World.Iso(today) })).Status);
        // 4. Đăng nhập lần đầu và đổi mật khẩu
        var logins = new List<(string User, string Temp)>
        {
            (tAcc.Body!["username"]!.GetValue<string>(), tAcc.Body["tempPassword"]!.GetValue<string>()),
            (pAcc.Body!["username"]!.GetValue<string>(), pAcc.Body["tempPassword"]!.GetValue<string>()),
        };
        foreach (var (user, temp) in logins)
        {
            var first = await f.Anonymous().PostAsync("/api/auth/login", new { username = user, password = temp });
            Assert.True(first.Body!["user"]!["mustChangePassword"]!.GetValue<bool>());
            var client = await f.LoginAsync(user, temp);
            Assert.Equal(403, (await client.GetAsync("/api/att/today")).Status);   // chưa đổi mật khẩu thì chưa dùng được
            Assert.Equal(204, (await client.PostAsync("/api/auth/change-password", new { currentPassword = temp, newPassword = "Mat-khau-moi-1" })).Status);
        }
        Assert.All(new[] { staffId, guardianId }, id => Assert.Equal("Active", f.WithDbAsync(db => db.Account.Where(a => a.PersonId == id).Select(a => a.Status).SingleAsync()).Result));
        // 5. Phụ huynh thấy đúng con và lịch lớp; giáo viên chưa có trong thời khóa biểu nào
        var parent = await f.LoginAsync(logins[1].User, "Mat-khau-moi-1");
        var kids = await parent.GetAsync("/api/par/leave-requests/my-children");
        Assert.Equal(200, kids.Status);
        Assert.Single(kids.Body!.AsArray());
        Assert.Contains($"\"studentId\":{pupilId},", kids.Raw);
        var from = World.Iso(today); var to = World.Iso(today.AddDays(30));
        Assert.Equal(200, (await parent.GetAsync($"/api/scd/schedule/child?studentId={pupilId}&from={from}&to={to}")).Status);
        var teacher = await f.LoginAsync(logins[0].User, "Mat-khau-moi-1");
        var mine = await teacher.GetAsync($"/api/scd/schedule/mine?from={from}&to={to}");
        Assert.Equal(200, mine.Status);
        Assert.Empty(mine.Body!.AsArray());
    }
}
