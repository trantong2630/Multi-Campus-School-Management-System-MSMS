using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Audit;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Adm;

/// <summary>
/// 4.9.11: CSDL chặn UPDATE và DELETE trên nhật ký (BR-28). Trigger chỉ có khi chạy migration nên test dùng Migrate, không dùng EnsureCreated.
/// Chỉ chạy khi có MSMS_IAM_SQLSERVER; mỗi test tạo và xóa một DB ngẫu nhiên riêng.
/// </summary>
public class AuditLogSqlServerTests
{
    private static async Task WithDatabaseAsync(Func<MsmsDbContext, AuditLog, Task> test)
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MSMS_IAM_SQLSERVER"))
        { InitialCatalog = $"MSMS_AuditTests_{Guid.NewGuid():N}", TrustServerCertificate = true };
        await using var db = new MsmsDbContext(new DbContextOptionsBuilder<MsmsDbContext>().UseSqlServer(builder.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            var now = new DateTime(2026, 11, 6, 8, 0, 0);
            var person = new Person { PersonType = "Staff", PersonCode = "CB1", FullName = "An Quản Trị", CreatedAt = now };
            db.Person.Add(person);
            await db.SaveChangesAsync();
            var entry = new AuditLog { Category = AuditCategories.Account, Action = "LockAccount", ActorPersonId = person.PersonId, TargetIds = "[1]", CreatedAt = now };
            db.AuditLog.Add(entry);
            await db.SaveChangesAsync();   // trigger không được cản INSERT của EF (có OUTPUT)
            await test(db, entry);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [SqlServerFact]
    public Task Insert_StillWorks_AndGeneratesTheId() => WithDatabaseAsync(async (db, entry) =>
    {
        Assert.True(entry.AuditId > 0);
        Assert.Equal(1, await db.AuditLog.CountAsync());
    });

    [SqlServerFact]
    public Task Update_IsBlocked_AndTheRowStaysUnchanged() => WithDatabaseAsync(async (db, entry) =>
    {
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE AuditLog SET Action = 'Tampered'"));
        Assert.Equal(50028, error.Number);
        Assert.Equal("LockAccount", await db.AuditLog.AsNoTracking().Select(a => a.Action).SingleAsync());
    });

    [SqlServerFact]
    public Task Delete_IsBlocked_AndTheRowStays() => WithDatabaseAsync(async (db, entry) =>
    {
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM AuditLog"));
        Assert.Equal(50028, error.Number);
        Assert.Equal(1, await db.AuditLog.AsNoTracking().CountAsync());
    });

    [SqlServerFact]
    public Task EfDelete_IsBlockedToo() => WithDatabaseAsync(async (db, entry) =>
    {
        db.AuditLog.Remove(entry);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    });

    /// <summary>InMemory không dịch LINQ sang SQL: chạy các câu tra cứu trên SQL Server thật.</summary>
    [SqlServerFact]
    public Task ReadQueries_TranslateToSql() => WithDatabaseAsync(async (db, entry) =>
    {
        db.Account.Add(new Account { AccountId = 1, PersonId = entry.ActorPersonId, Username = "anqt", AccountType = "SchoolAdmin", Status = "Active", PasswordHash = "x", CreatedAt = entry.CreatedAt });
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Account ON");
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Account OFF");
        var svc = new AuditLogService(db);
        var day = DateOnly.FromDateTime(entry.CreatedAt);

        var page = await svc.ListAsync(AuditCategories.Account, entry.ActorPersonId, day, day, 1, 20);
        Assert.Equal(1, page.Total);
        Assert.Equal("An Quản Trị", Assert.Single(page.Items).ActorName);

        Assert.Equal(entry.ActorPersonId, Assert.Single(await svc.ActorsAsync()).PersonId);

        var detail = await svc.GetAsync(entry.AuditId);
        Assert.Equal("An Quản Trị (anqt)", Assert.Single(detail.Targets).Label);
    });
}
