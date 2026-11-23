using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Hồ sơ cán bộ, chỉ xem (PPL-01, 4.3.2): phạm vi theo vai trò, trách nhiệm và giảng dạy.</summary>
public class StaffDetailTests
{
    private static readonly DateOnly YearStart = new(2026, 9, 5);

    private static async Task NotFound(Func<Task> act) => Assert.Equal(404, (await Assert.ThrowsAsync<ApiException>(act)).StatusCode);

    /// <summary>Học kỳ hiện hành, thời khóa biểu đã phát hành của lớp 6A: giáo viên dạy Toán 2 tiết.</summary>
    private static void SeedTeaching(PplEnv e, int teacher)
    {
        e.Db.Semester.Add(new Semester { SemesterId = 1, AcademicYearId = PplEnv.Year1, SemesterNo = 1, Name = "HK1", StartDate = YearStart, EndDate = new(2027, 1, 15) });
        e.Db.Subject.Add(new Subject { SubjectId = 1, DepartmentId = PplEnv.Dept1, Name = "Toán", Abbreviation = "TO", IsActive = true });
        e.Db.TimetableVersion.Add(new TimetableVersion { VersionId = 1, CampusId = PplEnv.Campus1, SemesterId = 1, Status = "Published", ImportedAt = e.Clock.Now, PublishedAt = e.Clock.Now });
        e.Db.TimetableVersionClass.Add(new TimetableVersionClass { VersionId = 1, ClassId = PplEnv.Class6A });
        e.Db.TimetableEntry.AddRange(
            new TimetableEntry { VersionId = 1, ClassId = PplEnv.Class6A, DayOfWeek = 2, PeriodSlotId = 1, SubjectId = 1, TeacherId = teacher },
            new TimetableEntry { VersionId = 1, ClassId = PplEnv.Class6A, DayOfWeek = 3, PeriodSlotId = 1, SubjectId = 1, TeacherId = teacher });
        e.Db.SaveChanges();
    }

    [Fact(DisplayName = "PPL | Hồ sơ cán bộ | School Admin | thấy hồ sơ, trách nhiệm (đang giữ trước, đã kết thúc sau) và giảng dạy")]
    public async Task Admin_ProfileAndResponsibilities()
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, YearStart);
        e.Seed("department", PplEnv.Dept1, PplEnv.Teacher, new(2025, 9, 1), new(2026, 8, 31));
        SeedTeaching(e, PplEnv.Teacher);

        var d = await e.Staff.GetDetailAsync(PplEnv.Teacher);
        Assert.Equal("Giáo viên A", d.Profile.FullName);
        Assert.Collection(d.Responsibilities,
            r => { Assert.Equal("homeroom", r.Kind); Assert.Equal("6A", r.TargetName); Assert.True(r.IsCurrent); },
            r => { Assert.Equal("department", r.Kind); Assert.False(r.IsCurrent); });
        Assert.Single(d.Teaching);
    }

    [Fact(DisplayName = "PPL | Hồ sơ cán bộ | Hiệu trưởng | thấy lớp và môn đang dạy kèm số tiết mỗi tuần")]
    public async Task Principal_SeesTeaching()
    {
        using var e = new PplEnv(AccountTypes.Principal, PplEnv.PrincipalId);
        SeedTeaching(e, PplEnv.Teacher);
        var t = Assert.Single((await e.Staff.GetDetailAsync(PplEnv.Teacher)).Teaching);
        Assert.Equal(("6A", "Toán", 2), (t.ClassName, t.SubjectName, t.PeriodsPerWeek));
    }

    [Fact(DisplayName = "PPL | Hồ sơ cán bộ | Hiệu phó | chỉ mở được cán bộ thuộc phân hiệu mình phụ trách")]
    public async Task VicePrincipal_ByCampus()
    {
        using (var none = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP))
            await NotFound(() => none.Staff.GetDetailAsync(PplEnv.Teacher));

        using var e = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP);
        e.Seed("campus", PplEnv.Campus1, PplEnv.ViceP, YearStart);
        SeedTeaching(e, PplEnv.Teacher);
        Assert.Single((await e.Staff.GetDetailAsync(PplEnv.Teacher)).Teaching);
    }

    [Fact(DisplayName = "PPL | Hồ sơ cán bộ | Mã không tồn tại | báo không tìm thấy")]
    public async Task Missing()
    {
        using var e = new PplEnv();
        await NotFound(() => e.Staff.GetDetailAsync(9999));
    }
}
