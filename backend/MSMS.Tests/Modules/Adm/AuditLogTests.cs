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
            Entry(1, AuditCategories.Account, AuditActions.CreateAccount, Admin, Day, "[10]"),
            Entry(2, AuditCategories.TimetablePublish, AuditActions.PublishTimetable, Vice, Day.AddDays(1), "[5]"),
            Entry(3, AuditCategories.ScheduleAdjust, AuditActions.CancelSession, Vice, Day.AddDays(2), "[77]"),
            Entry(4, AuditCategories.Account, AuditActions.BulkCreateAccounts, Admin, Day.AddDays(2), "[10,11,12]"));
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
            Entry(1, AuditCategories.Account, AuditActions.LockAccount, Admin, new DateTime(2026, 11, 1, 23, 59, 59)),
            Entry(2, AuditCategories.Account, AuditActions.LockAccount, Admin, new DateTime(2026, 11, 2, 0, 0, 0)),
            Entry(3, AuditCategories.Account, AuditActions.LockAccount, Admin, new DateTime(2026, 11, 3, 23, 59, 59)),
            Entry(4, AuditCategories.Account, AuditActions.LockAccount, Admin, new DateTime(2026, 11, 4, 0, 0, 0)));
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
        db.AuditLog.Add(Entry(1, AuditCategories.Account, AuditActions.BulkCreateAccounts, Admin, Day, "[10,99]", JsonSerializer.Serialize(new Dictionary<string, object?> { ["Số tài khoản"] = 2 })));
        db.SaveChanges();

        var d = await svc.GetAsync(1);

        Assert.Equal("Account", d.TargetKind);
        Assert.Equal("Châu Giáo Viên (chaugv)", d.Targets.Single(t => t.Id == 10).Label);
        Assert.Null(d.Targets.Single(t => t.Id == 99).Label);
        Assert.Equal("2", d.Fields.Single(f => f.Key == "Số tài khoản").Value);
    }

    [Fact]
    public async Task Detail_NamesKnownIds_FallsBackToHashId_AndSkipsEmptyValues()
    {
        var (db, svc) = Create();
        db.AuditLog.Add(Entry(1, AuditCategories.ScheduleAdjust, AuditActions.ChangeSessionTeacher, Vice, Day, "[77]",
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["Lớp"] = 404, ["Ngày"] = "02/11/2026", ["Giáo viên cũ"] = Teacher, ["Giáo viên mới"] = 500,
                ["Mã khai báo vắng"] = null, ["Các tiết"] = Array.Empty<int>(),
            })));
        db.SaveChanges();

        var d = await svc.GetAsync(1);
        var fields = d.Fields.ToDictionary(f => f.Key, f => f.Value);

        Assert.Equal("TeachingSession", d.TargetKind);
        Assert.Null(Assert.Single(d.Targets).Label);
        Assert.Equal("Châu Giáo Viên", fields["Giáo viên cũ"]);
        Assert.Equal("#500", fields["Giáo viên mới"]);
        Assert.Equal("#404", fields["Lớp"]);
        Assert.Equal("02/11/2026", fields["Ngày"]);
        Assert.False(fields.ContainsKey("Mã khai báo vắng"));
        Assert.False(fields.ContainsKey("Các tiết"));
    }

    [Fact]
    public async Task Detail_LegacyRecordsWithEnglishCodes_StillResolveNamesAndTargetKind()
    {
        var (db, svc) = Create();
        db.AuditLog.Add(Entry(1, "ScheduleAdjust", "DeclareTeacherAbsence", Vice, Day, "[5]",
            JsonSerializer.Serialize(new { teacherId = Teacher, slotIds = Array.Empty<int>() })));
        db.SaveChanges();

        var d = await svc.GetAsync(1);

        Assert.Equal("TeacherAbsence", d.TargetKind);
        Assert.Equal("Châu Giáo Viên", d.Fields.Single(f => f.Key == "teacherId").Value);
    }

    [Fact]
    public async Task Filter_ByVietnameseCategory_AlsoFindsLegacyRecords()
    {
        var (db, svc) = Create();
        db.AuditLog.AddRange(
            Entry(1, "Account", "LockAccount", Admin, Day, "[10]"),
            Entry(2, AuditCategories.Account, AuditActions.LockAccount, Admin, Day.AddDays(1), "[11]"),
            Entry(3, AuditCategories.Responsibility, AuditActions.AssignResponsibility, Admin, Day.AddDays(2)));
        db.SaveChanges();

        Assert.Equal([2L, 1L], (await svc.ListAsync(AuditCategories.Account, null, null, null, 1, 20)).Items.Select(i => i.AuditId));
    }

    [Theory]
    [InlineData(AuditCategories.TimetablePublish, AuditActions.PublishTimetable, "TimetableVersion")]
    [InlineData(AuditCategories.ScheduleAdjust, AuditActions.DeclareTeacherAbsence, "TeacherAbsence")]
    [InlineData(AuditCategories.ScheduleAdjust, AuditActions.DeleteTeacherAbsence, "TeacherAbsence")]
    [InlineData(AuditCategories.ScheduleAdjust, AuditActions.ResolvePendingCancel, "TeachingSession")]
    [InlineData(AuditCategories.Attendance, AuditActions.CorrectAttendance, "TeachingSession")]
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
    public void ExactlyFiveCategories_AllInVietnamese()
    {
        var categories = typeof(AuditCategories).GetFields().Where(f => f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).Order();

        Assert.Equal(new[] { "Tài khoản", "Phát hành thời khóa biểu", "Điều chỉnh lịch", "Trách nhiệm", "Điểm danh" }.Order(), categories);
        Assert.All(AuditCategories.All, c => Assert.True(c.Length <= 30));   // cột Category dài tối đa 30
    }

    [Fact]
    public void EveryAction_IsVietnameseAndFitsTheColumn()
    {
        var actions = typeof(AuditActions).GetFields().Select(f => (string)f.GetRawConstantValue()!).ToList();

        Assert.NotEmpty(actions);
        Assert.All(actions, a => Assert.Matches("[^\x00-\x7F]", a));      // có chữ có dấu, không phải mã tiếng Anh
        Assert.All(actions, a => Assert.DoesNotMatch("^[A-Za-z]+$", a));   // không có mã dạng CamelCase
        Assert.All(actions, a => Assert.True(a.Length <= 50));              // cột Action dài tối đa 50
    }

    /// <summary>Đăng nhập, nộp bài, kết chuyển không vào nhật ký; điểm danh chỉ vào khi School Admin nhập bù hoặc sửa. Chỉ các service của năm nhóm mới được giữ AuditWriter.</summary>
    [Fact]
    public void OnlyTheFiveGroupsServicesCanWrite()
    {
        var writers = typeof(AuditWriter).Assembly.GetTypes()
            .Where(t => t.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(AuditWriter))))
            .Select(t => t.Name).Order();

        // CalendarService: khai ngày nghỉ hủy các tiết chưa khóa trong ngày, thuộc nhóm Điều chỉnh lịch.
        Assert.Equal(["AccountService", "AttendanceService", "CalendarService", "EnrollmentService", "ResponsibilityService",
            "ScheduleAdjustmentService", "TeacherAbsenceService", "TimetablePublishService"], writers);
    }
}
