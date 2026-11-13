using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Controllers;
using MSMS.Api.Modules.Adm.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Adm;

/// <summary>ADM-02: tra cứu, lọc, xem chi tiết nhật ký và tính chỉ ghi thêm (task 4.9.8–4.9.11, BR-28).</summary>
public class AuditLogTests
{
    private const int Admin = 1, Vice = 2, Teacher = 3;
    private static readonly DateTime Day = new(2026, 11, 2, 8, 0, 0);

    private static (MsmsDbContext Db, AuditLogService Svc) Create()
    {
        var db = NewDb();
        db.Person.AddRange(
            new Person { PersonId = Admin, PersonType = "Staff", PersonCode = "CB1", FullName = "An Quản Trị", CreatedAt = Day },
            new Person { PersonId = Vice, PersonType = "Staff", PersonCode = "CB2", FullName = "Bình Hiệu Phó", CreatedAt = Day },
            new Person { PersonId = Teacher, PersonType = "Staff", PersonCode = "CB3", FullName = "Châu Giáo Viên", CreatedAt = Day });
        db.SaveChanges();
        return (db, new AuditLogService(db));
    }

    private static AuditLog Entry(long id, string category, string action, int actor, DateTime at, string? targets = null, string? detail = null) => new()
    {
        AuditId = id, Category = category, Action = action, ActorPersonId = actor, CreatedAt = at, TargetIds = targets, Detail = detail,
    };

    private static void Seed(MsmsDbContext db)
    {
        db.AuditLog.AddRange(
            Entry(1, AuditCategories.Account, "CreateAccount", Admin, Day, "[10]"),
            Entry(2, AuditCategories.TimetablePublish, "PublishTimetable", Vice, Day.AddDays(1), "[5]"),
            Entry(3, AuditCategories.ScheduleAdjust, "CancelSession", Vice, Day.AddDays(2), "[77]"),
            Entry(4, AuditCategories.Account, "BulkCreateAccounts", Admin, Day.AddDays(2), "[10,11,12]"));
        db.SaveChanges();
    }

    [Fact]
    public async Task List_NewestFirst_BreaksSameSecondTiesById_AndCountsTargets()
    {
        var (db, svc) = Create();
        Seed(db);

        var page = await svc.ListAsync(null, null, null, null, 1, 20);

        Assert.Equal(4, page.Total);
        Assert.Equal([4L, 3L, 2L, 1L], page.Items.Select(i => i.AuditId));
        Assert.Equal(3, page.Items[0].TargetCount);
        Assert.Equal("An Quản Trị", page.Items[0].ActorName);
    }

    [Fact]
    public async Task List_Pages()
    {
        var (db, svc) = Create();
        Seed(db);

        var second = await svc.ListAsync(null, null, null, null, 2, 3);

        Assert.Equal(4, second.Total);
        Assert.Equal(1L, Assert.Single(second.Items).AuditId);
    }

    [Fact]
    public async Task Filter_ByCategory_ByActor_AndBothTogether()
    {
        var (db, svc) = Create();
        Seed(db);

        Assert.Equal([4L, 1L], (await svc.ListAsync(AuditCategories.Account, null, null, null, 1, 20)).Items.Select(i => i.AuditId));
        Assert.Equal([3L, 2L], (await svc.ListAsync(null, Vice, null, null, 1, 20)).Items.Select(i => i.AuditId));
        Assert.Empty((await svc.ListAsync(AuditCategories.TimetablePublish, Admin, null, null, 1, 20)).Items);
    }

    [Fact]
    public async Task Filter_ByDateRange_IncludesTheWholeEndDay()
    {
        var (db, svc) = Create();
        db.AuditLog.AddRange(
            Entry(1, AuditCategories.Account, "LockAccount", Admin, new DateTime(2026, 11, 1, 23, 59, 59)),
            Entry(2, AuditCategories.Account, "LockAccount", Admin, new DateTime(2026, 11, 2, 0, 0, 0)),
            Entry(3, AuditCategories.Account, "LockAccount", Admin, new DateTime(2026, 11, 3, 23, 59, 59)),
            Entry(4, AuditCategories.Account, "LockAccount", Admin, new DateTime(2026, 11, 4, 0, 0, 0)));
        db.SaveChanges();

        var page = await svc.ListAsync(null, null, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), 1, 20);

        Assert.Equal([3L, 2L], page.Items.Select(i => i.AuditId));
    }

    [Fact]
    public async Task Filter_RejectsFromAfterTo()
    {
        var (_, svc) = Create();

        await Assert.ThrowsAsync<ApiException>(() => svc.ListAsync(null, null, new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 2), 1, 20));
    }

    [Fact]
    public async Task Actors_OnlyPeopleWithRecords()
    {
        var (db, svc) = Create();
        Seed(db);

        Assert.Equal([Admin, Vice], (await svc.ActorsAsync()).Select(a => a.PersonId));
    }

    [Fact]
    public async Task Detail_BulkAccounts_ListsEveryIdWithName_AndFallsBackWhenMissing()
    {
        var (db, svc) = Create();
        db.Account.Add(new Account { AccountId = 10, PersonId = Teacher, Username = "chaugv", AccountType = "Teacher", Status = "Active", PasswordHash = "x", CreatedAt = Day });
        db.AuditLog.Add(Entry(1, AuditCategories.Account, "BulkCreateAccounts", Admin, Day, "[10,99]", JsonSerializer.Serialize(new { count = 2 })));
        db.SaveChanges();

        var d = await svc.GetAsync(1);

        Assert.Equal("Account", d.TargetKind);
        Assert.Equal("Châu Giáo Viên (chaugv)", d.Targets.Single(t => t.Id == 10).Label);
        Assert.Null(d.Targets.Single(t => t.Id == 99).Label);
        Assert.Equal("2", d.Fields.Single(f => f.Key == "count").Value);
    }

    [Fact]
    public async Task Detail_NamesKnownIds_FallsBackToHashId_AndSkipsEmptyValues()
    {
        var (db, svc) = Create();
        db.AuditLog.Add(Entry(1, AuditCategories.ScheduleAdjust, "ChangeSessionTeacher", Vice, Day, "[77]",
            JsonSerializer.Serialize(new { sessionId = 77, classId = 404, date = new DateOnly(2026, 11, 2), fromTeacherId = Teacher, toTeacherId = 500, absenceId = (int?)null, slotIds = Array.Empty<int>() })));
        db.SaveChanges();

        var d = await svc.GetAsync(1);
        var fields = d.Fields.ToDictionary(f => f.Key, f => f.Value);

        Assert.Equal("TeachingSession", d.TargetKind);
        Assert.Null(Assert.Single(d.Targets).Label);
        Assert.Equal("Châu Giáo Viên", fields["fromTeacherId"]);
        Assert.Equal("#500", fields["toTeacherId"]);
        Assert.Equal("#404", fields["classId"]);
        Assert.Equal("77", fields["sessionId"]);
        Assert.Equal("2026-11-02", fields["date"]);
        Assert.False(fields.ContainsKey("absenceId"));
        Assert.False(fields.ContainsKey("slotIds"));
    }

    [Theory]
    [InlineData(AuditCategories.TimetablePublish, "PublishTimetable", "TimetableVersion")]
    [InlineData(AuditCategories.ScheduleAdjust, "DeclareTeacherAbsence", "TeacherAbsence")]
    [InlineData(AuditCategories.ScheduleAdjust, "DeleteTeacherAbsence", "TeacherAbsence")]
    [InlineData(AuditCategories.ScheduleAdjust, "ResolvePendingCancel", "TeachingSession")]
    public async Task Detail_TargetKindFollowsTheAction(string category, string action, string kind)
    {
        var (db, svc) = Create();
        db.AuditLog.Add(Entry(1, category, action, Vice, Day, "[1]"));
        db.SaveChanges();

        Assert.Equal(kind, (await svc.GetAsync(1)).TargetKind);
    }

    [Fact]
    public async Task Detail_NotFound()
    {
        var (_, svc) = Create();

        await Assert.ThrowsAsync<ApiException>(() => svc.GetAsync(1));
    }

    // ---------------------------------------------------------------- 4.9.11: chỉ ghi thêm

    [Fact]
    public void Api_IsSchoolAdminOnly_AndOnlyHasGet()
    {
        var type = typeof(AuditLogController);
        Assert.Equal(Policies.SchoolAdmin, type.GetCustomAttribute<AuthorizeAttribute>()!.Policy);

        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.NotEmpty(actions);
        Assert.All(actions, m => Assert.All(m.GetCustomAttributes<HttpMethodAttribute>(), h => Assert.IsType<HttpGetAttribute>(h)));
        Assert.All(actions, m => Assert.NotEmpty(m.GetCustomAttributes<HttpMethodAttribute>()));
    }

    [Fact]
    public void Api_NoOtherControllerServesTheAuditLog()
    {
        var controllers = typeof(AuditLogController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && t.GetCustomAttribute<RouteAttribute>()?.Template.Contains("audit", StringComparison.OrdinalIgnoreCase) == true);

        Assert.Equal(typeof(AuditLogController), Assert.Single(controllers));
    }

    [Fact]
    public void Writer_OnlyAdds()
    {
        var methods = typeof(AuditWriter).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name).Order();

        Assert.Equal(["Add", "AddLong"], methods);
    }

    [Fact]
    public void ExactlyThreeCategories()
    {
        var categories = typeof(AuditCategories).GetFields().Select(f => (string)f.GetRawConstantValue()!).Order();

        Assert.Equal(["Account", "ScheduleAdjust", "TimetablePublish"], categories);
    }

    /// <summary>Đăng nhập, nộp bài, điểm danh, trách nhiệm, kết chuyển không vào nhật ký: chỉ các service của ba nhóm mới được giữ AuditWriter.</summary>
    [Fact]
    public void OnlyTheThreeGroupsServicesCanWrite()
    {
        var writers = typeof(AuditWriter).Assembly.GetTypes()
            .Where(t => t.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(AuditWriter))))
            .Select(t => t.Name).Order();

        // CalendarService: khai ngày nghỉ hủy các tiết chưa khóa trong ngày, thuộc nhóm Điều chỉnh lịch.
        Assert.Equal(["AccountService", "CalendarService", "EnrollmentService", "ScheduleAdjustmentService", "TeacherAbsenceService", "TimetablePublishService"], writers);
    }
}
