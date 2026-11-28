using System.Net.Http.Json;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Iam;

/// <summary>IAM-01/03: đăng nhập, phiên, đổi mật khẩu, quên mật khẩu bằng OTP. Tên test bắt đầu bằng mã case (AUTH_xx).</summary>
[Collection("api")]
public class AuthTests(ApiFactory f)
{
    private World w => f.World;
    private const string Pw = ApiFactory.Password;

    private async Task<(ApiClient Client, ApiResult Res)> RawLoginAsync(string username, string password, bool mobile = true)
    {
        var client = new ApiClient(f.CreateClient());
        var res = await client.PostAsync("/api/auth/login", new { username, password, clientType = mobile ? "mobile" : (string?)null });
        return (client, res);
    }

    private static ApiClient WithToken(ApiFactory f, string token)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return new ApiClient(c);
    }

    [Fact]
    public async Task AUTH_01_Login_succeeds_for_every_account_type_and_returns_the_profile()
    {
        await w.EnsureAsync();
        var people = new[]
        {
            ("Trần Đức Kiên", "SchoolAdmin"), ("Nguyễn Văn Hùng", "Principal"), ("Lê Thị Mai", "VicePrincipal"),
            ("Phạm Minh Tuấn", "Teacher"), ("Hoàng Gia Bảo", "Student"), ("Vũ Thị Lan", "Parent"),
        };
        foreach (var (name, type) in people)
        {
            var who = await w.Find(name);
            var (_, res) = await RawLoginAsync(who.Username, Pw);
            Assert.Equal(200, res.Status);
            Assert.False(string.IsNullOrEmpty(res.Body!["token"]!.GetValue<string>()));
            Assert.False(string.IsNullOrEmpty(res.Body["refreshToken"]!.GetValue<string>()));
            Assert.Equal(type, res.Body["user"]!["accountType"]!.GetValue<string>());
            var me = await WithToken(f, res.Body["token"]!.GetValue<string>()).GetAsync("/api/auth/me");
            Assert.Equal(200, me.Status);
            Assert.Equal(type, me.Body!["accountType"]!.GetValue<string>());
            Assert.Equal(who.PersonId, me.Body["personId"]!.GetValue<int>());
        }
        Assert.True(await f.WithDbAsync(db => db.AccountSession.CountAsync()) >= 6);
    }

    [Fact]
    public async Task AUTH_02_And_03_Wrong_password_and_unknown_user_give_the_same_generic_answer()
    {
        var u = await w.NewAccountAsync("Người dùng AUTH02 " + World.Uniq(), "Staff", "Teacher", "Active");
        var before = await f.WithDbAsync(db => db.AccountSession.CountAsync());
        var (_, wrong) = await RawLoginAsync(u.Username, "Sai-mat-khau-1");
        var (_, unknown) = await RawLoginAsync("no.such.user", "Sai-mat-khau-1");
        Assert.Equal(401, wrong.Status);
        Assert.Equal("INVALID_CREDENTIALS", wrong.Code);
        Assert.StartsWith("Tên đăng nhập hoặc mật khẩu không đúng", wrong.Message);
        Assert.Equal(401, unknown.Status);
        Assert.Equal(wrong.Code, unknown.Code);
        Assert.Equal(wrong.Message, unknown.Message);
        Assert.Equal(before, await f.WithDbAsync(db => db.AccountSession.CountAsync()));
    }

    [Fact]
    public async Task AUTH_04_A_locked_account_cannot_log_in()
    {
        var u = await w.NewAccountAsync("Người dùng AUTH04 " + World.Uniq(), "Staff", "Teacher", "Locked");
        var (_, res) = await RawLoginAsync(u.Username, Pw);
        Assert.Equal(401, res.Status);
        // BR-01/BR-05: tài khoản khóa và sai mật khẩu cùng một lỗi trung tính, không lộ trạng thái khóa
        Assert.Equal("INVALID_CREDENTIALS", res.Code);
        var (_, wrong) = await RawLoginAsync(u.Username, "Sai-mat-khau-1");
        Assert.Equal(wrong.Message, res.Message);
        Assert.Equal(0, await f.WithDbAsync(db => db.AccountSession.CountAsync(s => s.Account.PersonId == u.PersonId)));
    }

    [Fact]
    public async Task AUTH_05_First_login_with_a_temporary_password_forces_a_password_change()
    {
        var u = await w.NewAccountAsync("Người dùng AUTH05 " + World.Uniq(), "Staff", "Teacher", "PendingPasswordChange");
        var (_, login) = await RawLoginAsync(u.Username, Pw);
        Assert.Equal(200, login.Status);
        Assert.True(login.Body!["user"]!["mustChangePassword"]!.GetValue<bool>());
        var api = WithToken(f, login.Body["token"]!.GetValue<string>());
        var blocked = await api.GetAsync("/api/att/today");
        Assert.Equal(403, blocked.Status);
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", blocked.Code);
        Assert.Equal(200, (await api.GetAsync("/api/auth/me")).Status);
        var change = await api.PostAsync("/api/auth/change-password", new { currentPassword = Pw, newPassword = "Mat-khau-moi-1" });
        Assert.Equal(204, change.Status);
        var (_, again) = await RawLoginAsync(u.Username, "Mat-khau-moi-1");
        Assert.Equal(200, again.Status);
        Assert.Equal(200, (await WithToken(f, again.Body!["token"]!.GetValue<string>()).GetAsync("/api/att/today")).Status);
    }

    [Fact]
    public async Task AUTH_06_And_07_Refresh_rotates_and_reuse_of_an_old_token_revokes_the_session()
    {
        var u = await w.NewAccountAsync("Người dùng AUTH06 " + World.Uniq(), "Staff", "Teacher", "Active");
        var (client, login) = await RawLoginAsync(u.Username, Pw);
        var rt1 = login.Body!["refreshToken"]!.GetValue<string>();
        var r1 = await client.PostAsync("/api/auth/refresh", new { refreshToken = rt1 });
        Assert.Equal(200, r1.Status);
        var rt2 = r1.Body!["refreshToken"]!.GetValue<string>();
        Assert.NotEqual(rt1, rt2);
        var r2 = await client.PostAsync("/api/auth/refresh", new { refreshToken = rt2 });
        Assert.Equal(200, r2.Status);
        var rt3 = r2.Body!["refreshToken"]!.GetValue<string>();
        // AUTH-07: dùng lại token cũ -> 401 và cả phiên bị thu hồi
        var reuse = await client.PostAsync("/api/auth/refresh", new { refreshToken = rt1 });
        Assert.Equal(401, reuse.Status);
        Assert.StartsWith("Phiên đăng nhập đã hết hạn hoặc không hợp lệ", reuse.Message);
        Assert.Equal(401, (await client.PostAsync("/api/auth/refresh", new { refreshToken = rt3 })).Status);
    }

    [Fact]
    public async Task AUTH_08_Logout_revokes_only_the_current_session()
    {
        var u = await w.NewAccountAsync("Người dùng AUTH08 " + World.Uniq(), "Staff", "Teacher", "Active");
        var d1 = WithToken(f, (await RawLoginAsync(u.Username, Pw)).Res.Body!["token"]!.GetValue<string>());
        var d2 = WithToken(f, (await RawLoginAsync(u.Username, Pw)).Res.Body!["token"]!.GetValue<string>());
        Assert.Equal(204, (await d1.PostAsync("/api/auth/logout")).Status);
        Assert.Equal(401, (await d1.GetAsync("/api/auth/me")).Status);
        Assert.Equal(200, (await d2.GetAsync("/api/auth/me")).Status);
    }

    [Fact]
    public async Task AUTH_11_A_locked_account_is_rejected_on_the_next_request_and_sessions_are_revoked()
    {
        var u = await w.NewAccountAsync("Người dùng AUTH11 " + World.Uniq(), "Staff", "Teacher", "Active");
        var (client, login) = await RawLoginAsync(u.Username, Pw);
        var api = WithToken(f, login.Body!["token"]!.GetValue<string>());
        var admin = await f.LoginAsync(w.SchoolAdmin.Username);
        var accountId = await f.WithDbAsync(db => db.Account.Where(a => a.PersonId == u.PersonId).Select(a => a.AccountId).SingleAsync());
        Assert.Equal(204, (await admin.PutAsync($"/api/iam/accounts/{accountId}/lock")).Status);
        var me = await api.GetAsync("/api/auth/me");
        Assert.Equal(401, me.Status);
        Assert.Equal("UNAUTHORIZED", me.Code);   // phiên đã bị thu hồi lúc khóa nên token bị từ chối ngay
        Assert.Equal(401, (await client.PostAsync("/api/auth/refresh", new { refreshToken = login.Body["refreshToken"]!.GetValue<string>() })).Status);
        Assert.Equal(0, await f.WithDbAsync(db => db.AccountSession.CountAsync(s => s.AccountId == accountId && s.RevokedAt == null)));
    }

    [Fact]
    public async Task AUTH_12_To_15_Change_password_rules()
    {
        var u = await w.NewAccountAsync("Người dùng AUTH12 " + World.Uniq(), "Staff", "Teacher", "Active");
        var d1 = WithToken(f, (await RawLoginAsync(u.Username, Pw)).Res.Body!["token"]!.GetValue<string>());
        var d2 = WithToken(f, (await RawLoginAsync(u.Username, Pw)).Res.Body!["token"]!.GetValue<string>());
        // AUTH-13: sai mật khẩu hiện tại
        var wrong = await d1.PostAsync("/api/auth/change-password", new { currentPassword = "Sai-mat-khau-1", newPassword = "Mat-khau-moi-1" });
        Assert.Equal(400, wrong.Status);
        Assert.StartsWith("Mật khẩu hiện tại không đúng", wrong.Message);
        // AUTH-14: 7 ký tự bị từ chối, 8 ký tự được chấp nhận ở bước sau
        var shortPw = await d1.PostAsync("/api/auth/change-password", new { currentPassword = Pw, newPassword = "Abc1234" });
        Assert.Equal(400, shortPw.Status);
        Assert.Contains("8", shortPw.Message);
        // AUTH-15: trùng mật khẩu cũ
        var same = await d1.PostAsync("/api/auth/change-password", new { currentPassword = Pw, newPassword = Pw });
        Assert.Equal(400, same.Status);
        Assert.StartsWith("Mật khẩu mới trùng mật khẩu cũ", same.Message);
        // AUTH-12: đổi thành công, các phiên khác bị thu hồi, phiên hiện tại giữ nguyên
        Assert.Equal(204, (await d1.PostAsync("/api/auth/change-password", new { currentPassword = Pw, newPassword = "Abc12345" })).Status);
        Assert.Equal(401, (await d2.GetAsync("/api/auth/me")).Status);
        Assert.Equal(200, (await d1.GetAsync("/api/auth/me")).Status);
        Assert.Equal("INVALID_CREDENTIALS", (await RawLoginAsync(u.Username, Pw)).Res.Code);
        Assert.Equal(200, (await RawLoginAsync(u.Username, "Abc12345")).Res.Status);
    }

    // ------------------------------------------------------------------ Quên mật khẩu (OTP)

    private async Task<(Who User, string Email)> ForgotUserAsync(string status = "Active")
    {
        var email = $"otp{World.Uniq()}@example.com";
        var u = await w.NewAccountAsync("Người dùng OTP " + World.Uniq(), "Staff", "Teacher", status, email);
        return (u, email);
    }

    [Fact]
    public async Task AUTH_16_And_17_Forgot_password_sends_a_code_only_for_active_accounts_with_the_same_answer()
    {
        var (active, email) = await ForgotUserAsync();
        var anon = f.Anonymous();
        var ok = await anon.PostAsync("/api/auth/forgot-password", new { username = active.Username });
        Assert.True(ok.Ok);
        Assert.Equal(1, f.Otp.CountTo(email));
        Assert.Matches("^[0-9]{6}$", f.Otp.LastCodeTo(email)!);
        var code = f.Otp.Sent.Last(s => s.To == email);
        Assert.InRange((code.ExpiresAt - f.Clock.Now).TotalMinutes, 9, 10.1);

        var (locked, lockedMail) = await ForgotUserAsync("Locked");
        var (pending, pendingMail) = await ForgotUserAsync("PendingPasswordChange");
        var r1 = await anon.PostAsync("/api/auth/forgot-password", new { username = locked.Username });
        var r2 = await anon.PostAsync("/api/auth/forgot-password", new { username = pending.Username });
        var r3 = await anon.PostAsync("/api/auth/forgot-password", new { username = "khong.ton.tai" });
        Assert.All(new[] { r1, r2, r3 }, r => Assert.Equal(ok.Status, r.Status));
        Assert.All(new[] { r1.Raw, r2.Raw, r3.Raw }, raw => Assert.Equal(ok.Raw, raw));
        Assert.Equal(0, f.Otp.CountTo(lockedMail) + f.Otp.CountTo(pendingMail));
    }

    [Fact]
    public async Task AUTH_18_Resend_before_60_seconds_is_throttled()
    {
        f.Clock.Set(new DateTime(2026, 12, 2, 9, 0, 0));
        try
        {
            var (u, email) = await ForgotUserAsync();
            var anon = f.Anonymous();
            await anon.PostAsync("/api/auth/forgot-password", new { username = u.Username });
            var first = f.Otp.LastCodeTo(email)!;
            await anon.PostAsync("/api/auth/forgot-password", new { username = u.Username });
            Assert.Equal(1, f.Otp.CountTo(email));
            f.Clock.Set(new DateTime(2026, 12, 2, 9, 1, 5));
            await anon.PostAsync("/api/auth/forgot-password", new { username = u.Username });
            Assert.Equal(2, f.Otp.CountTo(email));
            var second = f.Otp.LastCodeTo(email)!;
            Assert.NotEqual(first, second);
            // mã cũ bị vô hiệu
            var old = await anon.PostAsync("/api/auth/verify-reset-code", new { username = u.Username, code = first });
            Assert.Equal(400, old.Status);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task AUTH_19_Five_wrong_codes_invalidate_the_code()
    {
        var (u, email) = await ForgotUserAsync();
        var anon = f.Anonymous();
        await anon.PostAsync("/api/auth/forgot-password", new { username = u.Username });
        var good = f.Otp.LastCodeTo(email)!;
        var wrong = good == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++)
        {
            var r = await anon.PostAsync("/api/auth/verify-reset-code", new { username = u.Username, code = wrong });
            Assert.Equal(400, r.Status);
            Assert.StartsWith("Mã xác nhận không hợp lệ hoặc đã hết hiệu lực", r.Message);
        }
        Assert.Equal(400, (await anon.PostAsync("/api/auth/verify-reset-code", new { username = u.Username, code = good })).Status);
    }

    [Fact]
    public async Task AUTH_20_An_expired_code_is_rejected()
    {
        f.Clock.Set(new DateTime(2026, 12, 2, 9, 0, 0));
        try
        {
            var (u, email) = await ForgotUserAsync();
            var anon = f.Anonymous();
            await anon.PostAsync("/api/auth/forgot-password", new { username = u.Username });
            var code = f.Otp.LastCodeTo(email)!;
            f.Clock.Set(new DateTime(2026, 12, 2, 9, 11, 0));
            var r = await anon.PostAsync("/api/auth/verify-reset-code", new { username = u.Username, code });
            Assert.Equal(400, r.Status);
            Assert.StartsWith("Mã xác nhận không hợp lệ hoặc đã hết hiệu lực", r.Message);
        }
        finally { f.Clock.Reset(); }
    }

    [Fact]
    public async Task AUTH_21_To_23_Full_reset_flow_and_its_validation()
    {
        var (u, email) = await ForgotUserAsync();
        var anon = f.Anonymous();
        var other = WithToken(f, (await RawLoginAsync(u.Username, Pw)).Res.Body!["token"]!.GetValue<string>());
        // AUTH-22: đặt lại khi chưa xác minh
        var direct = await anon.PostAsync("/api/auth/reset-password", new { username = u.Username, resetToken = new string('a', 64), newPassword = "Mat-khau-moi-1" });
        Assert.Equal(400, direct.Status);
        Assert.Equal("PASSWORD_RESET_VERIFICATION_REQUIRED", direct.Code);
        await anon.PostAsync("/api/auth/forgot-password", new { username = u.Username });
        var verify = await anon.PostAsync("/api/auth/verify-reset-code", new { username = u.Username, code = f.Otp.LastCodeTo(email)! });
        Assert.Equal(200, verify.Status);
        var token = verify.Body!["resetToken"]!.GetValue<string>();
        // AUTH-23: 7 ký tự; trùng mật khẩu hiện tại
        var shortPw = await anon.PostAsync("/api/auth/reset-password", new { username = u.Username, resetToken = token, newPassword = "Abc1234" });
        Assert.Equal(400, shortPw.Status);
        Assert.Contains("8", shortPw.Message);
        var same = await anon.PostAsync("/api/auth/reset-password", new { username = u.Username, resetToken = token, newPassword = Pw });
        Assert.Equal(400, same.Status);
        Assert.Equal("PASSWORD_UNCHANGED", same.Code);
        // AUTH-21: đặt lại thành công, phiên cũ bị thu hồi
        var ok = await anon.PostAsync("/api/auth/reset-password", new { username = u.Username, resetToken = token, newPassword = "Mat-khau-moi-1" });
        Assert.True(ok.Ok);
        Assert.Equal(401, (await other.GetAsync("/api/auth/me")).Status);
        Assert.Equal(200, (await RawLoginAsync(u.Username, "Mat-khau-moi-1")).Res.Status);
        Assert.Equal(401, (await RawLoginAsync(u.Username, Pw)).Res.Status);
    }
}
