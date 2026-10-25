using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Services;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Tests.Modules.Scd;

/// <summary>BR-12 sửa bản Nháp: chuyển, đổi chỗ, thêm, đổi giáo viên; xung đột vẫn lưu và đánh dấu ô (BR-13).</summary>
public class TimetableDraftTests
{
    private const int CampusId = 1, ViceId = 1, TeacherA = 2, TeacherB = 3, Class1 = 11, Class2 = 12, Slot1 = 21, Slot2 = 22, Subj1 = 31, Subj2 = 32;
    private const int DraftId = 41, PublishedId = 42;

    private sealed class FakeClock(DateOnly today) : IClock
    {
        public DateTime Now { get; } = today.ToDateTime(new TimeOnly(8, 0));
        public DateOnly Today { get; } = today;
    }

    private sealed class Accessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }

    private sealed class Fixture
    {
        public required MsmsDbContext Db { get; init; }
        public required TimetableDraftService Draft { get; init; }
    }

    private static Fixture Create()
    {
        var db = new MsmsDbContext(new DbContextOptionsBuilder<MsmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var clock = new FakeClock(new DateOnly(2026, 10, 25));
        var claims = new[]
        {
            new Claim(CurrentUser.AccountIdClaim, ViceId.ToString()), new Claim(CurrentUser.PersonIdClaim, ViceId.ToString()),
            new Claim(CurrentUser.TypeClaim, AccountTypes.VicePrincipal), new Claim(CurrentUser.SessionIdClaim, Guid.NewGuid().ToString()),
        };
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) };
        var user = new CurrentUser(new Accessor(http));
        var scope = new ScopeResolver(db, user, clock);

        Seed(db, clock.Today);
        var publish = new TimetablePublishService(db, user, scope, clock, new AuditWriter(db, user, clock), new NotificationSender(db, clock));
        var query = new ScheduleQueryService(db, user, scope, publish);
        return new Fixture { Db = db, Draft = new TimetableDraftService(db, scope, query) };
    }

    private static void Seed(MsmsDbContext db, DateOnly today)
    {
        db.Campus.Add(new Campus { CampusId = CampusId, Code = "C1", Name = "Cơ sở 1", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.AcademicYear.Add(new AcademicYear { AcademicYearId = 1, Name = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2027, 5, 31), IsCurrent = true });
        db.Semester.Add(new Semester { SemesterId = 1, AcademicYearId = 1, SemesterNo = 1, Name = "Học kỳ 1", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2027, 1, 15) });
        db.Class.AddRange(
            new Class { ClassId = Class1, CampusId = CampusId, AcademicYearId = 1, Name = "10A1", GradeLevel = 10, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = Class2, CampusId = CampusId, AcademicYearId = 1, Name = "10A2", GradeLevel = 10, IsActive = true, CreatedAt = DateTime.UtcNow });
        db.PeriodSlot.AddRange(
            new PeriodSlot { PeriodSlotId = Slot1, SessionOfDay = SessionOfDay.Morning, PeriodNo = 1, StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(7, 45), IsActive = true },
            new PeriodSlot { PeriodSlotId = Slot2, SessionOfDay = SessionOfDay.Morning, PeriodNo = 2, StartTime = new TimeOnly(7, 50), EndTime = new TimeOnly(8, 35), IsActive = true });
        db.Subject.AddRange(
            new Subject { SubjectId = Subj1, DepartmentId = 1, Name = "Toán", Abbreviation = "TOAN", IsActive = true },
            new Subject { SubjectId = Subj2, DepartmentId = 1, Name = "Ngữ văn", Abbreviation = "VAN", IsActive = true });

        void Staff(int id, string name, string? abbr, string accountType)
        {
            db.Person.Add(new Person { PersonId = id, PersonType = "Staff", PersonCode = $"S{id}", FullName = name, CreatedAt = DateTime.UtcNow });
            db.StaffProfile.Add(new StaffProfile { StaffId = id, PrimaryCampusId = CampusId, TeacherAbbr = abbr, IsActive = true });
            db.Account.Add(new Account
            {
                AccountId = id, PersonId = id, Username = $"u{id}", PasswordHash = "x", AccountType = accountType,
                Status = AccountStatuses.Active, CreatedAt = DateTime.UtcNow,
            });
        }
        Staff(ViceId, "Hiệu phó", null, AccountTypes.VicePrincipal);
        Staff(TeacherA, "Nguyễn Văn A", "NVA", AccountTypes.Teacher);
        Staff(TeacherB, "Trần Thị B", "TTB", AccountTypes.Teacher);
        db.CampusLeadership.Add(new CampusLeadership { Id = 1, CampusId = CampusId, StaffId = ViceId, StartDate = new DateOnly(2026, 1, 1), CreatedAt = DateTime.UtcNow });

        db.TimetableVersion.AddRange(
            new TimetableVersion { VersionId = DraftId, CampusId = CampusId, SemesterId = 1, Status = "Draft", ImportedAt = DateTime.UtcNow },
            new TimetableVersion { VersionId = PublishedId, CampusId = CampusId, SemesterId = 1, Status = "Published", ImportedAt = DateTime.UtcNow });
        db.TimetableVersionClass.AddRange(
            new TimetableVersionClass { VersionId = DraftId, ClassId = Class1 },
            new TimetableVersionClass { VersionId = PublishedId, ClassId = Class1 });
        // Lớp 10A1 chỉ có tiết Toán ở Thứ 2, tiết 1 (giáo viên A) và Văn ở Thứ 2, tiết 2 (giáo viên B); 10A2 không có trong phiên bản.
        db.TimetableEntry.AddRange(
            new TimetableEntry { VersionId = DraftId, ClassId = Class1, DayOfWeek = 2, PeriodSlotId = Slot1, SubjectId = Subj1, TeacherId = TeacherA },
            new TimetableEntry { VersionId = DraftId, ClassId = Class1, DayOfWeek = 2, PeriodSlotId = Slot2, SubjectId = Subj2, TeacherId = TeacherB });
        db.SaveChanges();
    }

    private static GridCellDto? Cell(VersionGridDto g, int classId, int slotId, int day = 2)
    {
        var ci = g.Classes.ToList().FindIndex(c => c.ClassId == classId);
        return g.Rows.First(r => r.DayOfWeek == day && r.PeriodSlotId == slotId).Cells[ci];
    }

    [Fact]
    public async Task Move_ToEmptyCell_MovesTheLesson_AndMarksItEdited()
    {
        var f = Create();
        f.Db.TimetableVersion.Find(DraftId)!.ImportSnapshot = DraftSnapshot.Serialize(f.Db.TimetableEntry.Where(e => e.VersionId == DraftId));
        f.Db.SaveChanges();

        var grid = await f.Draft.MoveAsync(DraftId, new MoveEntryRequest(Class1, 2, Slot1, 3, Slot1));

        Assert.Null(Cell(grid, Class1, Slot1));
        var moved = Cell(grid, Class1, Slot1, day: 3);
        Assert.Equal("TOAN", moved!.SubjectAbbr);
        Assert.True(moved.Edited);
        Assert.False(Cell(grid, Class1, Slot2)!.Edited);
    }

    [Fact]
    public async Task Move_ToFilledCell_SwapsTheTwoLessons()
    {
        var f = Create();

        var grid = await f.Draft.MoveAsync(DraftId, new MoveEntryRequest(Class1, 2, Slot1, 2, Slot2));

        Assert.Equal("VAN", Cell(grid, Class1, Slot1)!.SubjectAbbr);
        Assert.Equal(TeacherB, Cell(grid, Class1, Slot1)!.TeacherId);
        Assert.Equal("TOAN", Cell(grid, Class1, Slot2)!.SubjectAbbr);
        Assert.Equal(TeacherA, Cell(grid, Class1, Slot2)!.TeacherId);
        Assert.Empty(grid.Conflicts);
    }

    [Fact]
    public async Task AddOrChangeTeacher_ToBusyTeacher_IsSaved_AndBothCellsAreFlaggedAsConflict()
    {
        var f = Create();

        // Giáo viên A dạy tiết 1 của 10A1; thêm lớp 10A2 vào phiên bản rồi xếp tiếp A vào đúng khung giờ đó.
        f.Db.TimetableVersionClass.Add(new TimetableVersionClass { VersionId = DraftId, ClassId = Class2 });
        f.Db.SaveChanges();
        var grid = await f.Draft.AddAsync(DraftId, new AddEntryRequest(Class2, 2, Slot1, Subj2, TeacherA));

        Assert.True(Cell(grid, Class1, Slot1)!.InConflict);
        Assert.True(Cell(grid, Class2, Slot1)!.InConflict);
        Assert.False(Cell(grid, Class1, Slot2)!.InConflict);
        Assert.Single(grid.Conflicts);

        // Đổi sang giáo viên B (đang dạy 10A1 tiết 2) vẫn khác khung giờ: hết xung đột.
        grid = await f.Draft.ChangeTeacherAsync(DraftId, new ChangeTeacherRequest(Class2, 2, Slot1, TeacherB));
        Assert.Empty(grid.Conflicts);
        Assert.False(Cell(grid, Class2, Slot1)!.InConflict);
    }

    [Fact]
    public async Task Add_ToOccupiedCell_IsRejected()
    {
        var f = Create();
        await Assert.ThrowsAsync<ApiException>(() => f.Draft.AddAsync(DraftId, new AddEntryRequest(Class1, 2, Slot1, Subj2, TeacherB)));
    }

    [Fact]
    public async Task Edit_ClassNotInVersion_IsRejected()
    {
        var f = Create();
        await Assert.ThrowsAsync<ApiException>(() => f.Draft.AddAsync(DraftId, new AddEntryRequest(Class2, 3, Slot1, Subj1, TeacherA)));
    }

    [Fact]
    public async Task Edit_PublishedVersion_IsRejected()
    {
        var f = Create();
        await Assert.ThrowsAsync<ApiException>(() => f.Draft.AddAsync(PublishedId, new AddEntryRequest(Class1, 3, Slot1, Subj1, TeacherA)));
    }

    [Fact]
    public async Task Add_LockedTeacher_IsRejected()
    {
        var f = Create();
        f.Db.Account.Find(TeacherB)!.Status = AccountStatuses.Locked;
        f.Db.SaveChanges();
        await Assert.ThrowsAsync<ApiException>(() => f.Draft.AddAsync(DraftId, new AddEntryRequest(Class1, 3, Slot1, Subj1, TeacherB)));
    }

    [Fact]
    public async Task Remove_DeletesTheLesson()
    {
        var f = Create();
        var grid = await f.Draft.RemoveAsync(DraftId, Class1, 2, Slot1);
        Assert.Null(Cell(grid, Class1, Slot1));
        Assert.NotNull(Cell(grid, Class1, Slot2));
    }
}
