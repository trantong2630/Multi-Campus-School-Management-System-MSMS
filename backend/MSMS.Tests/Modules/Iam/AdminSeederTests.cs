using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MSMS.Api.Common.Auth;
using MSMS.Api.Data;
using MSMS.Api.Modules.Iam.Dtos;

namespace MSMS.Tests.Modules.Iam;

/// <summary>School Admin đầu tiên cho môi trường thật: chỉ tạo khi chưa có tài khoản nào và đã đặt cấu hình.</summary>
public class AdminSeederTests
{
    private static IConfiguration Config(string? username, string? password) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            [AdminSeeder.UsernameKey] = username,
            [AdminSeeder.PasswordKey] = password,
        }).Build();

    private static Task Run(AuthTestFixture f, IConfiguration config) =>
        AdminSeeder.RunAsync(f.Db, config, f.Clock, NullLogger.Instance, _ => Task.FromResult(1));

    [Fact]
    public async Task EmptyDatabase_CreatesPendingSchoolAdminWhoCanLogIn()
    {
        using var f = new AuthTestFixture();

        await Run(f, Config("  Admin  ", "FirstPassword123"));

        var account = await f.Db.Account.Include(a => a.Person).SingleAsync();
        Assert.Equal("admin", account.Username);   // bỏ khoảng trắng, chữ thường
        Assert.Equal(AccountTypes.SchoolAdmin, account.AccountType);
        Assert.Equal(AccountStatuses.PendingPasswordChange, account.Status);
        Assert.Equal("Staff", account.Person.PersonType);
        Assert.Equal("CB1", account.Person.PersonCode);
        Assert.NotEqual("FirstPassword123", account.PasswordHash);   // lưu băm, không lưu mật khẩu

        var login = await f.Auth.LoginAsync(new LoginRequest("admin", "FirstPassword123"), "test-device");
        Assert.NotNull(login);
    }

    [Fact]
    public async Task ExistingAccount_DoesNothing()
    {
        using var f = new AuthTestFixture();
        await f.AddAccountAsync(AccountTypes.Teacher);

        await Run(f, Config("admin", "FirstPassword123"));

        Assert.Equal(1, await f.Db.Account.CountAsync());
        Assert.False(await f.Db.Account.AnyAsync(a => a.Username == "admin"));
    }

    [Theory]
    [InlineData(null, "FirstPassword123")]
    [InlineData("admin", null)]
    [InlineData("", "")]
    [InlineData("   ", "FirstPassword123")]
    public async Task MissingConfig_CreatesNothing(string? username, string? password)
    {
        using var f = new AuthTestFixture();

        await Run(f, Config(username, password));

        Assert.Equal(0, await f.Db.Account.CountAsync());
        Assert.Equal(0, await f.Db.Person.CountAsync());
    }

    [Fact]
    public async Task ShortPassword_FailsAtStartupAndCreatesNothing()
    {
        using var f = new AuthTestFixture();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(f, Config("admin", "short")));

        Assert.Equal(0, await f.Db.Account.CountAsync());
    }

    [Fact]
    public async Task RunTwice_DoesNotCreateSecondAdmin()
    {
        using var f = new AuthTestFixture();

        await Run(f, Config("admin", "FirstPassword123"));
        await Run(f, Config("other", "AnotherPassword123"));

        var account = await f.Db.Account.SingleAsync();
        Assert.Equal("admin", account.Username);
    }
}
