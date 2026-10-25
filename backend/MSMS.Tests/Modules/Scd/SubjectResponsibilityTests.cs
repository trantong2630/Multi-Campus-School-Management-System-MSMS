using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Tests.Modules.Scd;

/// <summary>BR-14: phụ trách chuyên môn tính từ phiên bản Đã phát hành mới nhất của từng lớp, không lưu riêng.</summary>
public class SubjectResponsibilityTests
{
    private const int Campus1 = 1, Campus2 = 2, ClassA = 11, ClassB = 12, ClassC = 13, Toan = 31, Van = 32, TeacherX = 2, TeacherY = 3;
    private const int V1 = 41, V2 = 42, VDraft = 43;

    private sealed class FakeClock(DateOnly today) : IClock
    {
        public DateTime Now { get; } = today.ToDateTime(new TimeOnly(8, 0));
        public DateOnly Today { get; } = today;
    }

    private static (MsmsDbContext Db, SubjectResponsibilityService Service) Create()
    {
        var db = new MsmsDbContext(new DbContextOptionsBuilder<MsmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Campus.AddRange(
            new Campus { CampusId = Campus1, Code = "C1", Name = "Cơ sở 1", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Campus { CampusId = Campus2, Code = "C2", Name = "Cơ sở 2", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.AcademicYear.Add(new AcademicYear { AcademicYearId = 1, Name = "2026-2027", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2027, 5, 31), IsCurrent = true });
        db.Semester.Add(new Semester { SemesterId = 1, AcademicYearId = 1, SemesterNo = 1, Name = "Học kỳ 1", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2027, 1, 15) });
        db.Class.AddRange(
            new Class { ClassId = ClassA, CampusId = Campus1, AcademicYearId = 1, Name = "9A", GradeLevel = 9, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = ClassB, CampusId = Campus1, AcademicYearId = 1, Name = "9B", GradeLevel = 9, IsActive = true, CreatedAt = DateTime.UtcNow },
            new Class { ClassId = ClassC, CampusId = Campus2, AcademicYearId = 1, Name = "9C", GradeLevel = 9, IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Subject.AddRange(
            new Subject { SubjectId = Toan, DepartmentId = 1, Name = "Toán", Abbreviation = "TOAN", IsActive = true },
            new Subject { SubjectId = Van, DepartmentId = 1, Name = "Ngữ văn", Abbreviation = "VAN", IsActive = true });
        foreach (var (id, name) in new[] { (TeacherX, "Nguyễn Văn X"), (TeacherY, "Trần Thị Y") })
        {
            db.Person.Add(new Person { PersonId = id, PersonType = "Staff", PersonCode = $"S{id}", FullName = name, CreatedAt = DateTime.UtcNow });
            db.StaffProfile.Add(new StaffProfile { StaffId = id, PrimaryCampusId = Campus1, TeacherAbbr = $"T{id}", IsActive = true });
        }

        // V1 (cũ hơn) phủ cả ba lớp; V2 (mới hơn) chỉ phủ lớp A; VDraft là bản Nháp nên không tính.
        db.TimetableVersion.AddRange(
            new TimetableVersion { VersionId = V1, CampusId = Campus1, SemesterId = 1, Status = "Published", ImportedAt = DateTime.UtcNow, PublishedAt = new DateTime(2026, 9, 1) },
            new TimetableVersion { VersionId = V2, CampusId = Campus1, SemesterId = 1, Status = "Published", ImportedAt = DateTime.UtcNow, PublishedAt = new DateTime(2026, 10, 1) },
            new TimetableVersion { VersionId = VDraft, CampusId = Campus1, SemesterId = 1, Status = "Draft", ImportedAt = DateTime.UtcNow });
        db.TimetableVersionClass.AddRange(
            new TimetableVersionClass { VersionId = V1, ClassId = ClassA }, new TimetableVersionClass { VersionId = V1, ClassId = ClassB },
            new TimetableVersionClass { VersionId = V1, ClassId = ClassC }, new TimetableVersionClass { VersionId = V2, ClassId = ClassA },
            new TimetableVersionClass { VersionId = VDraft, ClassId = ClassB });

        var slot = 1;
        void Entry(int version, int classId, int subject, int teacher, int count)
        {
            for (var i = 0; i < count; i++)
                db.TimetableEntry.Add(new TimetableEntry { VersionId = version, ClassId = classId, DayOfWeek = (byte)(2 + slot / 10), PeriodSlotId = slot++, SubjectId = subject, TeacherId = teacher });
        }
        Entry(V1, ClassA, Toan, TeacherX, 4);   // bị V2 thay thế
        Entry(V1, ClassB, Toan, TeacherX, 3);
        Entry(V1, ClassC, Toan, TeacherX, 2);
        Entry(V2, ClassA, Toan, TeacherY, 4);   // lớp A đổi sang giáo viên Y
        Entry(V2, ClassA, Van, TeacherX, 2);
        Entry(VDraft, ClassB, Van, TeacherY, 5);
        db.SaveChanges();

        return (db, new SubjectResponsibilityService(db, new FakeClock(new DateOnly(2026, 10, 25))));
    }

    [Fact]
    public async Task Each_class_follows_its_own_latest_published_version()
    {
        var (_, service) = Create();
        var result = await service.ListAsync(null, null);

        var x = result.Teachers.Single(t => t.TeacherId == TeacherX);
        Assert.Equal(["Ngữ văn 9A", "Toán 9B", "Toán 9C"], x.Items.Select(i => $"{i.SubjectName} {i.ClassName}"));
        Assert.Equal([2, 3, 2], x.Items.Select(i => i.PeriodsPerWeek));
        Assert.Equal(7, x.TotalPeriods);

        var y = result.Teachers.Single(t => t.TeacherId == TeacherY);
        Assert.Equal([("Toán", "9A", 4)], y.Items.Select(i => (i.SubjectName, i.ClassName, i.PeriodsPerWeek)));   // bản Nháp không tính
    }

    [Fact]
    public async Task Campus_filter_selects_teachers_but_keeps_all_their_items()
    {
        var (_, service) = Create();
        var onlyCampus2 = await service.ListAsync(Campus2, null);

        var x = Assert.Single(onlyCampus2.Teachers);
        Assert.Equal(TeacherX, x.TeacherId);
        Assert.Equal(3, x.Items.Count);   // vẫn đủ lớp ở phân hiệu khác
        Assert.Equal(7, x.TotalPeriods);
    }

    [Fact]
    public async Task Subject_filter_selects_teachers_who_teach_the_subject()
    {
        var (_, service) = Create();
        var result = await service.ListAsync(null, Van);

        Assert.Equal([TeacherX], result.Teachers.Select(t => t.TeacherId));
    }

    [Fact]
    public async Task Nothing_is_returned_before_any_publish()
    {
        var (db, service) = Create();
        db.TimetableVersion.Where(v => v.Status == "Published").ToList().ForEach(v => v.Status = "Draft");
        await db.SaveChangesAsync();

        var result = await service.ListAsync(null, null);

        Assert.Empty(result.Teachers);
    }
}
