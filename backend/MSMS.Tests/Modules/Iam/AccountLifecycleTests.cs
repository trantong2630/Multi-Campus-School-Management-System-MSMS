using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Dtos;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Tests.Modules.Iam;

/// <summary>WBS 4.1.9–4.1.18: tài khoản, mật khẩu một lần và tác động lên lịch/trách nhiệm.</summary>
public class AccountLifecycleTests
{
    [Theory]
    [InlineData("Staff", AccountTypes.SchoolAdmin)]
    [InlineData("Staff", AccountTypes.Principal)]
    [InlineData("Staff", AccountTypes.VicePrincipal)]
    [InlineData("Staff", AccountTypes.Teacher)]
    [InlineData("Student", AccountTypes.Student)]
    [InlineData("Parent", AccountTypes.Parent)]
    public async Task CreateSingle_UsesExistingProfile_GeneratesUsernameAndHashedTemporaryPassword(string personType, string role)
    {
        using var f = new AccountTestFixture();
        var person = f.AddPerson(1, personType, "Trần Đức Kiên");
        var result = await f.Accounts.CreateAsync(new(1, role));
        var account = await f.Db.Account.SingleAsync();
        Assert.Equal($"kientd{person.PersonCode.ToLowerInvariant()}", result.Username);
        Assert.Equal(role, account.AccountType);
        Assert.Equal(AccountStatuses.PendingPasswordChange, account.Status);
        Assert.Equal(12, result.TempPassword.Length);
        Assert.Contains(result.TempPassword, char.IsDigit);
        Assert.Contains(result.TempPassword, char.IsLetter);
        Assert.Contains(result.TempPassword, c => "@#$%".Contains(c));
        Assert.NotEqual(result.TempPassword, account.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<Account>().VerifyHashedPassword(account, account.PasswordHash, result.TempPassword));
        Assert.Equal(person.PersonId, account.PersonId);
        var audit = await f.Db.AuditLog.SingleAsync();
        Assert.Equal("CreateAccount", audit.Action);
        Assert.DoesNotContain(result.TempPassword, JsonSerializer.Serialize(audit));
        await Assert.ThrowsAsync<ApiException>(() => f.Accounts.CreateAsync(new(1, role)));
        Assert.Equal(1, await f.Db.Account.CountAsync());
    }

    [Theory]
    [InlineData("Student", AccountTypes.Teacher)]
    [InlineData("Parent", AccountTypes.Student)]
    [InlineData("Staff", AccountTypes.Parent)]
    [InlineData("Staff", "Unknown")]
    public async Task CreateSingle_RejectsIncompatibleRole(string type, string role)
    {
        using var f = new AccountTestFixture();
        f.AddPerson(1, type);
        await Assert.ThrowsAsync<ApiException>(() => f.Accounts.CreateAsync(new(1, role)));
        Assert.Empty(f.Db.Account);
        Assert.Empty(f.Db.AuditLog);
    }

    [Theory]
    [InlineData("Student", AccountTypes.Student)]
    [InlineData("Parent", AccountTypes.Parent)]
    [InlineData("Staff", AccountTypes.VicePrincipal)]
    public async Task BulkCreate_AutomaticFamilyRoles_SkipsExistingAndMissing_Deduplicates_OneAudit(string type, string role)
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, role);
        f.AddPerson(2, type);
        f.AddPerson(3, type);
        var result = await f.Accounts.BulkCreateAsync(new([1, 2, 2, 3, 404], type == "Staff" ? role : null));
        Assert.Equal(2, result.Created.Count);
        Assert.All(result.Created, item => Assert.Equal(role, item.AccountType));
        Assert.Equal(2, result.Skipped.Count);
        Assert.Contains(result.Skipped, item => item.PersonId == 1);
        Assert.Contains(result.Skipped, item => item.PersonId == 404);
        Assert.NotEqual(result.Created[0].TempPassword, result.Created[1].TempPassword);
        var audit = await f.Db.AuditLog.SingleAsync();
        Assert.Equal("BulkCreateAccounts", audit.Action);
        Assert.Equal(2, JsonSerializer.Deserialize<int[]>(audit.TargetIds!)!.Length);
        Assert.All(result.Created, item => Assert.DoesNotContain(item.TempPassword, audit.Detail!));
        var repeat = await f.Accounts.BulkCreateAsync(new([2, 3], role));
        Assert.Empty(repeat.Created);
        Assert.Equal(2, repeat.Skipped.Count);
        Assert.Single(f.Db.AuditLog);
    }

    [Fact]
    public async Task InvalidBulk_DoesNotCreateAccounts()
    {
        using var f = new AccountTestFixture();
        f.AddPerson(1, active: false);
        f.AddPerson(2);
        await Assert.ThrowsAsync<ApiException>(() => f.Accounts.BulkCreateAsync(new([], AccountTypes.Teacher)));
        await Assert.ThrowsAsync<ApiException>(() => f.Accounts.BulkCreateAsync(new(Enumerable.Range(1, 201).ToArray(), AccountTypes.Teacher)));
        var result = await f.Accounts.BulkCreateAsync(new([1, 2], AccountTypes.Teacher));
        Assert.Single(result.Created);
        Assert.Equal(1, Assert.Single(result.Skipped).PersonId);
        Assert.Equal("BulkCreateAccounts", (await f.Db.AuditLog.SingleAsync()).Action);
        f.AddPerson(3);
        Assert.Empty((await f.Accounts.BulkCreateAsync(new([3], null))).Created);
    }

    [Fact]
    public async Task ListAndDirectory_ApplySearchTypeStatusCampusAndPagination_ExposeNoPasswords()
    {
        using var f = new AccountTestFixture();
        var teacher = f.AddAccount(1);
        teacher.LastLoginAt = f.Clock.Now;
        f.AddAccount(2, AccountTypes.Teacher, AccountStatuses.Locked, campusId: 2);
        f.AddAccount(3, AccountTypes.Student);
        f.AddPerson(4, "Parent", "Phụ huynh chưa có tài khoản");
        await f.Db.SaveChangesAsync();
        var filtered = await f.Accounts.ListAsync("CB000001", AccountTypes.Teacher, AccountStatuses.Active, 1, 1, 20);
        var item = Assert.Single(filtered.Items);
        Assert.Equal(1, item.PersonId);
        Assert.Equal("Phân hiệu 1", item.CampusName);
        Assert.Equal(f.Clock.Now, item.LastLoginAt);
        Assert.Single((await f.Accounts.ListAsync("user2", null, null, null, 1, 20)).Items);
        Assert.Empty((await f.Accounts.ListAsync(null, AccountTypes.Student, null, 1, 1, 20)).Items);
        var page = await f.Accounts.ListAsync(null, null, null, null, 2, 1);
        Assert.Equal(3, page.Total);
        Assert.Single(page.Items);
        Assert.DoesNotContain("Password", JsonSerializer.Serialize(page));
        var directory = await f.Accounts.DirectoryAsync("Parent", "chưa có", 1, 20);
        Assert.False(Assert.Single(directory.Items).HasAccount);
        Assert.True(Assert.Single((await f.Accounts.DirectoryAsync("Student", null, 1, 20)).Items).HasAccount);
        await Assert.ThrowsAsync<ApiException>(() => f.Accounts.DirectoryAsync("Unknown", null, 1, 20));
    }

    [Fact]
    public async Task LockTeacher_MovesOnlyUnlockedScheduledLessonsAcrossCampuses_NotifiesCurrentVicePrincipals_ClosesCurrentDuties()
    {
        using var f = new AccountTestFixture();
        var teacher = f.AddAccount(1);
        f.AddDuties(1);
        f.AddAccount(2, AccountTypes.VicePrincipal);
        f.AddAccount(3, AccountTypes.VicePrincipal, campusId: 2);
        f.AddAccount(4, AccountTypes.VicePrincipal, AccountStatuses.Locked);
        f.AddAccount(5, AccountTypes.VicePrincipal);
        f.Db.CampusLeadership.AddRange(
            new CampusLeadership { StaffId = 2, CampusId = 1, StartDate = f.Clock.Today.AddDays(-1) },
            new CampusLeadership { StaffId = 3, CampusId = 2, StartDate = f.Clock.Today },
            new CampusLeadership { StaffId = 4, CampusId = 1, StartDate = f.Clock.Today.AddDays(-1) },
            new CampusLeadership { StaffId = 5, CampusId = 1, StartDate = f.Clock.Today.AddDays(-2), EndDate = f.Clock.Today });
        var futureDuty = new HomeroomAssignment { ClassId = 2, StaffId = 1, StartDate = f.Clock.Today.AddDays(5) };
        f.Db.HomeroomAssignment.Add(futureDuty);
        var today = f.AddLesson(1, 1);
        var future = f.AddLesson(1, 2, 5);
        var past = f.AddLesson(1, 1, -1);
        var cancelled = f.AddLesson(1, 1, 2, "Cancelled");
        var locked = f.AddLesson(1, 1, locked: true);
        var pending = f.AddLesson(1, 1, 3, "Pending");
        var other = f.AddLesson(2, 2, 5);
        var (session1, token1) = await f.Sessions.CreateAsync(1, "device1");
        var (session2, _) = await f.Sessions.CreateAsync(1, "device2");
        var (unrelated, _) = await f.Sessions.CreateAsync(2, "vice");
        var preview = await f.Accounts.LockImpactAsync(1);
        Assert.Equal(2, preview.SessionCount);
        Assert.Equal(2, preview.Campuses.Count);
        Assert.Equal(3, preview.Duties.Count);
        Assert.Equal(AccountStatuses.Active, teacher.Status);

        await f.Accounts.LockAsync(1);

        Assert.Equal(AccountStatuses.Locked, teacher.Status);
        foreach (var lesson in new[] { today, future })
        {
            Assert.Equal("Pending", lesson.Status);
            Assert.Equal("Manual", lesson.Source);
            Assert.Null(lesson.TeacherId);
            Assert.Null(lesson.TeacherAbsenceId);
            Assert.Equal(900, lesson.UpdatedByPersonId);
        }
        Assert.Equal("Scheduled", past.Status);
        Assert.Equal("Scheduled", locked.Status);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(1, pending.TeacherId);
        Assert.Equal(2, other.TeacherId);
        Assert.Single(f.Db.AttendanceRecord);
        Assert.Equal(f.Clock.Today, f.Db.DepartmentHead.Single().EndDate);
        Assert.Equal(f.Clock.Today, f.Db.HomeroomAssignment.Single(d => d.ClassId == 1).EndDate);
        Assert.Null(futureDuty.EndDate);
        Assert.Equal(new[] { 2, 3 }, await f.Db.Notification.OrderBy(n => n.RecipientPersonId).Select(n => n.RecipientPersonId).ToArrayAsync());
        Assert.All(f.Db.Notification, n => Assert.Equal(NotificationTypes.SessionPending, n.NotificationType));
        Assert.False(await f.Sessions.IsActiveAsync(session1.SessionId, 1));
        Assert.False(await f.Sessions.IsActiveAsync(session2.SessionId, 1));
        Assert.Equal(RefreshOutcome.Invalid, (await f.Sessions.RefreshAsync(token1)).Outcome);
        var oldPrincipal = AccountTestFixture.Principal(AccountTypes.Teacher, 1, 1, session1.SessionId);
        Assert.Equal(401, (await new AccountAccessValidator(f.Db, f.Sessions).ValidateAsync(oldPrincipal))!.StatusCode);
        Assert.True(await f.Sessions.IsActiveAsync(unrelated.SessionId, 2));
        using var detail = JsonDocument.Parse(f.Db.AuditLog.Single().Detail!);
        Assert.Equal(2, detail.RootElement.GetProperty("pendingSessions").GetInt32());
        Assert.Equal(3, detail.RootElement.GetProperty("closedDuties").GetInt32());

        await f.Accounts.UnlockAsync(1);
        Assert.Equal(AccountStatuses.PendingPasswordChange, teacher.Status);
        var scope = await new AccountScopeResolver(f.Db, f.Clock).ResolveAsync(1, AccountTypes.Teacher);
        Assert.Empty(scope.HomeroomClassIds);
        Assert.Empty(scope.HeadDepartmentIds);
        Assert.Equal("Pending", today.Status);
        Assert.Equal(401, (await new AccountAccessValidator(f.Db, f.Sessions).ValidateAsync(oldPrincipal))!.StatusCode);
    }

    [Theory]
    [InlineData(AccountTypes.Principal)]
    [InlineData(AccountTypes.VicePrincipal)]
    [InlineData(AccountTypes.SchoolAdmin)]
    public async Task LockNonTeacher_ClosesStaffDuties_LeavesLessonsUntouched(string role)
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, role);
        f.AddDuties(1);
        var lesson = f.AddLesson(1, 1, 2);
        await f.Accounts.LockAsync(1);
        Assert.Equal("Scheduled", lesson.Status);
        Assert.Equal(f.Clock.Today, f.Db.CampusLeadership.Single().EndDate);
        Assert.Equal(f.Clock.Today, f.Db.DepartmentHead.Single().EndDate);
        Assert.Equal(f.Clock.Today, f.Db.HomeroomAssignment.Single().EndDate);
        Assert.Empty(f.Db.Notification);
    }

    [Fact]
    public async Task ChangeType_ClosesDuties_ReleasesLessons_RevokesSessions_RetainsProfileAndHistory()
    {
        using var f = new AccountTestFixture();
        var teacher = f.AddAccount(1);
        f.AddDuties(1);
        var past = f.AddLesson(1, 1, -1, locked: true);
        var future = f.AddLesson(1, 2, 2);
        var (session, _) = await f.Sessions.CreateAsync(1, "teacher");
        var oldHash = teacher.PasswordHash;
        var result = await f.Accounts.ChangeTypeAsync(1, new(AccountTypes.VicePrincipal));
        Assert.Equal(AccountTypes.VicePrincipal, teacher.AccountType);
        Assert.Equal(AccountStatuses.PendingPasswordChange, teacher.Status);
        Assert.Null(teacher.UpdatedAt);
        Assert.NotEqual(oldHash, teacher.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<Account>().VerifyHashedPassword(teacher, teacher.PasswordHash, result.TempPassword));
        Assert.False(await f.Sessions.IsActiveAsync(session.SessionId, 1));
        var oldPrincipal = AccountTestFixture.Principal(AccountTypes.Teacher, 1, 1, session.SessionId);
        Assert.Equal(401, (await new AccountAccessValidator(f.Db, f.Sessions).ValidateAsync(oldPrincipal))!.StatusCode);
        Assert.Equal(1, teacher.PersonId);
        Assert.Single(f.Db.Person);
        Assert.Equal(1, past.TeacherId);
        Assert.Single(f.Db.AttendanceRecord);
        Assert.Equal("Pending", future.Status);
        Assert.Equal(f.Clock.Today, f.Db.HomeroomAssignment.Single().EndDate);
        var audit = Assert.Single(f.Db.AuditLog);
        Assert.Equal("ChangeAccountType", audit.Action);
        Assert.DoesNotContain(result.TempPassword, audit.Detail!);
        Assert.DoesNotContain(oldHash, audit.Detail!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unlock_RestoresCorrectPasswordState_WithoutRestoringSessions(bool changedPassword)
    {
        using var f = new AccountTestFixture();
        var account = f.AddAccount(1, status: AccountStatuses.Locked);
        account.UpdatedAt = changedPassword ? f.Clock.Now : null;
        await f.Db.SaveChangesAsync();
        await f.Accounts.UnlockAsync(1);
        Assert.Equal(changedPassword ? AccountStatuses.Active : AccountStatuses.PendingPasswordChange, account.Status);
        Assert.Equal("UnlockAccount", f.Db.AuditLog.Single().Action);
        await Assert.ThrowsAsync<ApiException>(() => f.Accounts.UnlockAsync(1));
    }

    [Fact]
    public async Task InvalidMutations_DoNotChangeAccountOrHistory()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(900, AccountTypes.SchoolAdmin);
        var student = f.AddAccount(1, AccountTypes.Student);
        await Assert.ThrowsAsync<ApiException>(() => f.Accounts.LockAsync(900));
        await Assert.ThrowsAsync<ApiException>(() => f.Accounts.ChangeTypeAsync(900, new(AccountTypes.Teacher)));
        await Assert.ThrowsAsync<ApiException>(() => f.Accounts.ChangeTypeAsync(1, new(AccountTypes.Teacher)));
        await Assert.ThrowsAsync<ApiException>(() => f.Accounts.ChangeTypeAsync(1, new(AccountTypes.Student)));
        await Assert.ThrowsAsync<ApiException>(() => f.Accounts.LockAsync(404));
        Assert.Equal(AccountStatuses.Active, student.Status);
        Assert.Empty(f.Db.AuditLog);
    }

    [Fact]
    public async Task TemporaryPasswordSheet_IsBuiltOnlyFromImmediateResult_AndNeverStoredOrReturnedInLists()
    {
        using var f = new AccountTestFixture();
        f.AddPerson(1, "Student");
        var result = await f.Accounts.CreateAsync(new(1, AccountTypes.Student));
        var file = f.Accounts.BuildTempPasswordSheet(new([result]));
        using var workbook = new XLWorkbook(new MemoryStream(file.FileContents));
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Học sinh", sheet.Cell(2, 5).GetString());
        Assert.Equal(result.TempPassword, sheet.Cell(2, 6).GetString());
        Assert.Equal(result.Username, sheet.Cell(2, 4).GetString());
        var list = await f.Accounts.ListAsync(null, null, null, null, 1, 20);
        Assert.DoesNotContain(result.TempPassword, JsonSerializer.Serialize(list));
        Assert.DoesNotContain(result.TempPassword, f.Db.Account.Single().PasswordHash);
        Assert.Throws<ApiException>(() => f.Accounts.BuildTempPasswordSheet(new([])));
    }

    [Fact]
    public async Task BulkWithOneSelectedProfile_StillRecordsBulkOperation()
    {
        using var f = new AccountTestFixture();
        f.AddPerson(1, "Parent");
        await f.Accounts.BulkCreateAsync(new([1], null));
        Assert.Equal("BulkCreateAccounts", f.Db.AuditLog.Single().Action);
    }
}
