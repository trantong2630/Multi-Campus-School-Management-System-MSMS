using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Tests.Modules.Iam;

internal sealed class AuthTestFixture : IDisposable
{
    internal sealed class TestClock : IClock
    {
        public DateTime Now { get; set; } = new(2026, 10, 27, 14, 0, 0, DateTimeKind.Unspecified);
        public DateOnly Today => DateOnly.FromDateTime(Now);
    }

    public MsmsDbContext Db { get; } = new(new DbContextOptionsBuilder<MsmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    public TestClock Clock { get; } = new();
    public IOptions<JwtOptions> Jwt { get; } = Options.Create(new JwtOptions
    {
        Key = "test-signing-key-with-at-least-thirty-two-bytes-for-hmac-sha256",
        Issuer = "msms-tests",
        ExpiryMinutes = 15,
        RefreshDays = 7,
    });
    public SessionService Sessions => new(Db, Clock, Jwt);
    public AuthService Auth => new(Db, Clock, Jwt, Sessions);

    public async Task<Account> AddAccountAsync(string accountType = AccountTypes.Teacher,
        string status = AccountStatuses.Active)
    {
        var account = new Account
        {
            Person = new Person
            {
                PersonType = accountType == AccountTypes.Student ? "Student" : accountType == AccountTypes.Parent ? "Parent" : "Staff",
                PersonCode = "GV001",
                FullName = "Nguyễn Văn An",
                CreatedAt = Clock.Now,
            },
            Username = "annguyenvan",
            AccountType = accountType,
            Status = status,
            CreatedAt = Clock.Now,
        };
        account.PasswordHash = AuthService.HashPassword(account, "CorrectPassword123");
        Db.Account.Add(account);
        await Db.SaveChangesAsync();
        return account;
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public void Dispose() => Db.Dispose();
}
