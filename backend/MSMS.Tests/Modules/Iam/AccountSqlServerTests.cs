using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Tests.Modules.Iam;

/// <summary>Chỉ chạy khi có MSMS_IAM_SQLSERVER. Mỗi test tạo/xóa một DB ngẫu nhiên riêng, không dùng DB ứng dụng.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MSMS_IAM_SQLSERVER")))
            Skip = "Set MSMS_IAM_SQLSERVER to run isolated SQL Server transaction tests.";
    }
}

public class AccountSqlServerTests
{
    private sealed class FailSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Enabled ? throw new InvalidOperationException("Simulated persistence failure") : ValueTask.FromResult(result);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string name = $"MSMS_IamTests_{Guid.NewGuid():N}";
        public readonly FailSave Failure = new();
        public readonly AuthTestFixture.TestClock Clock = new();
        public readonly IOptions<JwtOptions> Jwt = Options.Create(new JwtOptions
        {
            Key = "sqlserver-integration-test-signing-key-at-least-thirty-two-bytes", Issuer = "msms-tests", RefreshDays = 7,
        });
        public MsmsDbContext Db { get; }

        public TestDatabase()
        {
            var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MSMS_IAM_SQLSERVER"))
            { InitialCatalog = name, TrustServerCertificate = true };
            Db = new MsmsDbContext(new DbContextOptionsBuilder<MsmsDbContext>().UseSqlServer(builder.ConnectionString)
                .AddInterceptors(Failure).Options);
        }

        public async Task<(Account Admin, Account Teacher, Account Vice, TeachingSession Lesson)> SeedAsync()
        {
            await Db.Database.EnsureCreatedAsync();
            var campus = new Campus { Code = "TEST", Name = "Phân hiệu kiểm thử", IsActive = true };
            var year = new AcademicYear { Name = "2026-2027", StartDate = Clock.Today.AddDays(-30), EndDate = Clock.Today.AddMonths(6), IsCurrent = true };
            var cls = new Class { Name = "6A", Campus = campus, AcademicYear = year, IsActive = true };
            var subject = new Subject { Name = "Toán", Abbreviation = "TOAN", IsActive = true,
                Department = new SubjectDepartment { Name = "Tổ Toán", IsActive = true } };
            Account AccountOf(string role, string code) => new()
            {
                AccountType = role, Status = AccountStatuses.Active, Username = code.ToLowerInvariant(), PasswordHash = "hash",
                Person = new Person { PersonCode = code, PersonType = "Staff", FullName = role,
                    StaffProfile = new StaffProfile { PrimaryCampus = campus, IsActive = true } },
            };
            var admin = AccountOf(AccountTypes.SchoolAdmin, "CB900001");
            var teacher = AccountOf(AccountTypes.Teacher, "CB900002");
            var vice = AccountOf(AccountTypes.VicePrincipal, "CB900003");
            var lesson = new TeachingSession { Class = cls, Teacher = teacher.Person.StaffProfile, Subject = subject,
                PeriodSlot = new PeriodSlot { SessionOfDay = "Morning", PeriodNo = 1, StartTime = new(7, 0), EndTime = new(7, 45), IsActive = true },
                SessionDate = Clock.Today.AddDays(1), Status = "Scheduled", Source = "Timetable" };
            Db.Account.AddRange(admin, teacher, vice);
            Db.TeachingSession.Add(lesson);
            Db.CampusLeadership.Add(new CampusLeadership { Campus = campus, Staff = vice.Person.StaffProfile!, StartDate = Clock.Today.AddDays(-10) });
            Db.HomeroomAssignment.Add(new HomeroomAssignment { Class = cls, Staff = teacher.Person.StaffProfile!, StartDate = Clock.Today.AddDays(-10) });
            await Db.SaveChangesAsync();
            return (admin, teacher, vice, lesson);
        }

        public SessionService Sessions => new(Db, Clock, Jwt);
        public AccountService Service(Account admin)
        {
            var user = new CurrentUser(new HttpContextAccessor { HttpContext = new DefaultHttpContext
                { User = AccountTestFixture.Principal(admin.AccountType, admin.PersonId, admin.AccountId) } });
            return new AccountService(Db, user, Clock, new AuditWriter(Db, user, Clock), Sessions, new NotificationSender(Db, Clock));
        }

        public async ValueTask DisposeAsync()
        {
            Failure.Enabled = false;
            // Chỉ xóa đúng DB ngẫu nhiên do fixture này tạo.
            var catalog = new SqlConnectionStringBuilder(Db.Database.GetConnectionString()).InitialCatalog;
            if (catalog != name || !catalog.StartsWith("MSMS_IamTests_", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to delete a database outside this test fixture.");
            await Db.Database.EnsureDeletedAsync();
            await Db.DisposeAsync();
        }
    }

    [SqlServerFact]
    public async Task Lock_CommitsAccountSessionLessonDutyNotificationAndAuditTogether()
    {
        await using var f = new TestDatabase();
        var (admin, teacher, vice, lesson) = await f.SeedAsync();
        var (session, _) = await f.Sessions.CreateAsync(teacher.AccountId, "sql-device");
        var service = f.Service(admin);
        var directory = await service.DirectoryAsync("Staff", "Teacher", 1, 20);
        Assert.Equal(teacher.PersonId, Assert.Single(directory.Items).PersonId);
        await service.LockAsync(teacher.AccountId);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(AccountStatuses.Locked, (await f.Db.Account.FindAsync(teacher.AccountId))!.Status);
        Assert.False(await f.Sessions.IsActiveAsync(session.SessionId, teacher.AccountId));
        var saved = await f.Db.TeachingSession.FindAsync(lesson.SessionId);
        Assert.Equal("Pending", saved!.Status);
        Assert.Null(saved.TeacherId);
        Assert.Equal(f.Clock.Today, (await f.Db.HomeroomAssignment.SingleAsync()).EndDate);
        Assert.Equal(vice.PersonId, (await f.Db.Notification.SingleAsync()).RecipientPersonId);
        Assert.Equal("LockAccount", (await f.Db.AuditLog.SingleAsync()).Action);
    }

    [SqlServerFact]
    public async Task FailedLock_RollsBackIncludingExecuteUpdateSessionRevocation()
    {
        await using var f = new TestDatabase();
        var (admin, teacher, _, lesson) = await f.SeedAsync();
        var (session, _) = await f.Sessions.CreateAsync(teacher.AccountId, "sql-device");
        f.Failure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service(admin).LockAsync(teacher.AccountId));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(AccountStatuses.Active, (await f.Db.Account.FindAsync(teacher.AccountId))!.Status);
        Assert.True(await f.Sessions.IsActiveAsync(session.SessionId, teacher.AccountId));
        Assert.Equal("Scheduled", (await f.Db.TeachingSession.FindAsync(lesson.SessionId))!.Status);
        Assert.Null((await f.Db.HomeroomAssignment.SingleAsync()).EndDate);
        Assert.Empty(f.Db.Notification);
        Assert.Empty(f.Db.AuditLog);
    }

    [SqlServerFact]
    public async Task DatabaseUniqueConstraint_RejectsSecondAccountForSamePerson()
    {
        await using var f = new TestDatabase();
        var (_, teacher, _, _) = await f.SeedAsync();
        f.Db.ChangeTracker.Clear();
        f.Db.Account.Add(new Account { PersonId = teacher.PersonId, Username = "different-login",
            PasswordHash = "hash", AccountType = AccountTypes.Teacher, Status = AccountStatuses.PendingPasswordChange });
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear();
        Assert.Equal(1, await f.Db.Account.CountAsync(a => a.PersonId == teacher.PersonId));
    }
}
