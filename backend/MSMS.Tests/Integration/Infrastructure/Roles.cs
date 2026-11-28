namespace MSMS.Tests.Integration.Infrastructure;

/// <summary>Sáu máy khách đăng nhập, mỗi loại tài khoản một (từ dữ liệu mẫu của DevSeeder), dùng cho kiểm tra ma trận quyền.</summary>
public static class Roles
{
    public const string Admin = "SchoolAdmin", Principal = "Principal", Vice = "VicePrincipal", Teacher = "Teacher", Student = "Student", Parent = "Parent";
    public static readonly string[] All = [Admin, Principal, Vice, Teacher, Student, Parent];

    public static async Task<Dictionary<string, ApiClient>> LoginAllAsync(this ApiFactory f)
    {
        await f.World.EnsureAsync();
        return new Dictionary<string, ApiClient>
        {
            [Admin] = await f.LoginAsPersonAsync("Trần Đức Kiên"), [Principal] = await f.LoginAsPersonAsync("Nguyễn Văn Hùng"),
            [Vice] = await f.LoginAsPersonAsync("Lê Thị Mai"), [Teacher] = await f.LoginAsPersonAsync("Phạm Minh Tuấn"),
            [Student] = await f.LoginAsPersonAsync("Hoàng Gia Bảo"), [Parent] = await f.LoginAsPersonAsync("Vũ Thị Lan"),
        };
    }

    /// <summary>GET <paramref name="url"/> bằng từng loại tài khoản: loại trong <paramref name="allowed"/> nhận 200, loại khác nhận 403.</summary>
    public static async Task AssertGetByRoleAsync(this ApiFactory f, string url, params string[] allowed)
    {
        var roles = await f.LoginAllAsync();
        foreach (var role in All)
        {
            var r = await roles[role].GetAsync(url);
            Assert.True(allowed.Contains(role) ? r.Status == 200 : r.Status == 403, $"GET {url} as {role}: {r.Status} {r.Message}");
        }
    }
}
