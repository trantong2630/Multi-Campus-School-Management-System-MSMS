using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>SCH-05 (4.2.25–4.2.28): lớp thuộc một phân hiệu và một năm học, tên duy nhất trong phạm vi đó, khối 6–9 (BR-07).</summary>
public class ClassServiceTests
{
    private const int Campus1 = 1, Campus2 = 2, Year1 = 1, Year2 = 2;

    private static (MsmsDbContext Db, ClassService Service) Create()
    {
        var db = NewDb();
        AddCampus(db, Campus1, "Phân hiệu 1");
        AddCampus(db, Campus2, "Phân hiệu 2");
        AddYear(db, Year1, "2026-2027", D(2026, 9, 5), D(2027, 5, 31), current: true);
        AddYear(db, Year2, "2027-2028", D(2027, 9, 5), D(2028, 5, 31));
        return (db, new ClassService(db, ClockAt()));
    }

    private static ClassRequest Req(int campus, int year, string name, byte grade = 6) => new(campus, year, name, grade);

    [Theory]
    [InlineData(null, true)]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task ListVisible_TeacherOnlySeesCurrentHomeroomClasses(int? endOffset, bool visible)
    {
        var (db, svc) = Create();
        var own = await svc.CreateAsync(Req(Campus1, Year1, "6A1"));
        await svc.CreateAsync(Req(Campus2, Year1, "6B1"));
        AddStaff(db, 50, "Giáo viên chủ nhiệm");
        db.HomeroomAssignment.Add(new HomeroomAssignment
        {
            ClassId = own.ClassId, StaffId = 50, StartDate = Today.AddDays(-10),
            EndDate = endOffset is int offset ? Today.AddDays(offset) : null, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var scope = new ScopeResolver(db, UserOf(AccountTypes.Teacher, 50), ClockAt());

        var classes = await svc.ListVisibleAsync(null, null, false, scope);

        Assert.Equal(visible ? new[] { own.ClassId } : Array.Empty<int>(), classes.Select(c => c.ClassId));
    }

    [Fact]
    public async Task Create_StoresClassAsActive_WithNormalizedName()
    {
        var (_, svc) = Create();

        var cls = await svc.CreateAsync(Req(Campus1, Year1, "  6A1 "));

        Assert.Equal("6A1", cls.Name);
        Assert.True(cls.IsActive);
        Assert.Equal(0, cls.StudentCount);
        Assert.Equal("Phân hiệu 1", cls.CampusName);
    }

    [Fact]
    public async Task Create_SameNameInSameCampusAndYear_IsRejected_ButOtherScopesAreFine()
    {
        var (_, svc) = Create();
        await svc.CreateAsync(Req(Campus1, Year1, "6A1"));

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Req(Campus1, Year1, "6A1")));
        await svc.CreateAsync(Req(Campus2, Year1, "6A1"));   // phân hiệu khác
        await svc.CreateAsync(Req(Campus1, Year2, "6A1"));   // năm học khác
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    public async Task Create_GradeOutsideSixToNine_IsRejected(byte grade)
    {
        var (db, svc) = Create();

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Req(Campus1, Year1, "X", grade)));

        Assert.Empty(db.Class);
    }

    [Fact]
    public async Task Create_InactiveOrUnknownCampus_OrUnknownYear_IsRejected()
    {
        var (db, svc) = Create();
        AddCampus(db, 3, "Phân hiệu đã đóng", active: false);

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Req(3, Year1, "6A1")));
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Req(99, Year1, "6A1")));
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateAsync(Req(Campus1, 99, "6A1")));
    }

    [Fact]
    public async Task Update_RenamesAndRegrades_ButCannotTakeASiblingsName()
    {
        var (_, svc) = Create();
        var a = await svc.CreateAsync(Req(Campus1, Year1, "6A1"));
        await svc.CreateAsync(Req(Campus1, Year1, "6A2"));

        await svc.UpdateAsync(a.ClassId, new ClassUpdateRequest("6A1", 7));   // giữ tên, đổi khối
        await Assert.ThrowsAsync<ApiException>(() => svc.UpdateAsync(a.ClassId, new ClassUpdateRequest("6A2", 7)));
        await Assert.ThrowsAsync<ApiException>(() => svc.UpdateAsync(a.ClassId, new ClassUpdateRequest("6A1", 10)));

        Assert.Equal(7, (await svc.ListAsync(Campus1, Year1, false)).Single(c => c.ClassId == a.ClassId).GradeLevel);
        Assert.Equal(404, (await Assert.ThrowsAsync<ApiException>(() => svc.UpdateAsync(999, new ClassUpdateRequest("X", 6)))).StatusCode);
    }

    [Fact]
    public async Task Deactivate_WithOpenEnrollment_IsRejected_AndAllowedOnceEnrollmentHasEnded()
    {
        var (db, svc) = Create();
        var cls = await svc.CreateAsync(Req(Campus1, Year1, "6A1"));
        var enrollment = new Enrollment { StudentId = 101, ClassId = cls.ClassId, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow };
        db.Enrollment.Add(enrollment);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.SetActiveAsync(cls.ClassId, false));
        Assert.Contains("1 học sinh", ex.Message);

        enrollment.EndDate = Today.AddDays(-1);
        await db.SaveChangesAsync();
        await svc.SetActiveAsync(cls.ClassId, false);
        Assert.Empty(await svc.ListAsync(Campus1, Year1, includeInactive: false));

        await svc.SetActiveAsync(cls.ClassId, true);
        Assert.Single(await svc.ListAsync(Campus1, Year1, includeInactive: false));
    }

    [Fact]
    public async Task List_FiltersByCampusYearAndActive_AndCountsOnlyStudentsEffectiveToday()
    {
        var (db, svc) = Create();
        var a = await svc.CreateAsync(Req(Campus1, Year1, "6A1"));
        await svc.CreateAsync(Req(Campus2, Year1, "6B1"));
        await svc.CreateAsync(Req(Campus1, Year2, "7A1", 7));
        var stopped = await svc.CreateAsync(Req(Campus1, Year1, "6A9"));
        await svc.SetActiveAsync(stopped.ClassId, false);
        db.Enrollment.AddRange(
            new Enrollment { StudentId = 101, ClassId = a.ClassId, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow },
            new Enrollment { StudentId = 102, ClassId = a.ClassId, StartDate = D(2026, 9, 5), EndDate = Today.AddDays(-1), CreatedAt = DateTime.UtcNow },
            new Enrollment { StudentId = 103, ClassId = a.ClassId, StartDate = Today.AddDays(1), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        Assert.Equal(["6A1"], (await svc.ListAsync(Campus1, Year1, false)).Select(c => c.Name));
        Assert.Equal(["6A1", "6A9"], (await svc.ListAsync(Campus1, Year1, true)).Select(c => c.Name));
        Assert.Equal(3, (await svc.ListAsync(null, null, true)).Count(c => c.AcademicYearId == Year1));   // 6A1, 6B1, 6A9
        Assert.Equal(1, (await svc.ListAsync(Campus1, Year1, false)).Single().StudentCount);   // chỉ em 101 đang hiệu lực hôm nay
    }

    [Fact]
    public async Task List_ShowsTheHomeroomTeacherEffectiveToday()
    {
        var (db, svc) = Create();
        var cls = await svc.CreateAsync(Req(Campus1, Year1, "6A1"));
        AddStaff(db, 50, "Nguyễn Thị Cũ");
        AddStaff(db, 51, "Trần Văn Mới");
        db.HomeroomAssignment.AddRange(
            new HomeroomAssignment { ClassId = cls.ClassId, StaffId = 50, StartDate = D(2026, 9, 5), EndDate = Today.AddDays(-1), CreatedAt = DateTime.UtcNow },
            new HomeroomAssignment { ClassId = cls.ClassId, StaffId = 51, StartDate = Today, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var listed = (await svc.ListAsync(Campus1, Year1, false)).Single();

        Assert.Equal("Trần Văn Mới", listed.HomeroomName);
    }
}
