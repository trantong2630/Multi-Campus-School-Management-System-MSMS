using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>SCH-06 (4.2.35–4.2.38): kết chuyển năm học — gợi ý lớp, tạo lớp năm mới, chuyển nguyên lớp (BR-09).</summary>
public class RolloverTests
{
    private const int Campus1 = 1, Campus2 = 2, Year1 = 1, Year2 = 2;
    private const int C6A1 = 11, C7A1 = 12, C9A1 = 13, C6B1 = 14;
    private const int Left = 103, Graduated = 104;
    private static readonly DateOnly Year1End = D(2027, 5, 31), Year2Start = D(2027, 9, 5);

    // ---- Quy tắc gợi ý ----
    [Theory]
    [InlineData(6, 7)]
    [InlineData(8, 9)]
    public void NextGrade_IsOnePlus(byte grade, byte expected) => Assert.Equal(expected, RolloverRules.NextGrade(grade));

    [Fact]
    public void NextGrade_ForGrade9_HasNoSuggestion() => Assert.Null(RolloverRules.NextGrade(9));

    [Theory]
    [InlineData("6A1", 6, 7, "7A1")]
    [InlineData("Lớp 6B", 6, 7, "Lớp 7B")]
    [InlineData("8C", 8, 9, "9C")]
    [InlineData("6A6", 6, 7, "7A6")]            // chỉ thay lần đầu
    [InlineData("16A", 6, 7, "16A")]            // số 6 nằm trong số khác thì không đụng
    [InlineData("Chuyên Toán", 6, 7, "Chuyên Toán")]   // không có số khối thì giữ nguyên
    public void SuggestName_ReplacesTheStandaloneGradeDigit(string name, byte oldGrade, byte newGrade, string expected) =>
        Assert.Equal(expected, RolloverRules.SuggestName(name, oldGrade, newGrade));

    // ---- Dựng dữ liệu ----
    private static (MsmsDbContext Db, RolloverService Service) Create()
    {
        var db = NewDb();
        AddCampus(db, Campus1, "Phân hiệu 1");
        AddCampus(db, Campus2, "Phân hiệu 2");
        AddYear(db, Year1, "2026-2027", D(2026, 9, 5), Year1End, current: true);
        AddYear(db, Year2, "2027-2028", Year2Start, D(2028, 5, 31));
        AddClass(db, C6A1, Campus1, Year1, "6A1", 6);
        AddClass(db, C7A1, Campus1, Year1, "7A1", 7);
        AddClass(db, C9A1, Campus1, Year1, "9A1", 9);
        AddClass(db, C6B1, Campus2, Year1, "6B1", 6);

        foreach (var (id, status) in new[] { (101, "Studying"), (102, "Studying"), (Left, "Studying"), (Graduated, "Graduated"), (105, "Studying"), (106, "Studying") })
        {
            db.Person.Add(new Person { PersonId = id, PersonType = "Student", PersonCode = $"HS{id}", FullName = $"Học sinh {id}", CreatedAt = DateTime.UtcNow });
            db.StudentProfile.Add(new StudentProfile { StudentId = id, Status = status });
        }
        db.Enrollment.AddRange(
            Enroll(101, C6A1), Enroll(102, C6A1),
            Enroll(Left, C6A1, end: D(2026, 12, 31)),   // chuyển đi giữa năm
            Enroll(Graduated, C6A1),                    // trạng thái không còn học
            Enroll(105, C7A1), Enroll(106, C9A1));
        AddStaff(db, 50, "Giáo viên chủ nhiệm");
        db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = C6A1, StaffId = 50, StartDate = D(2026, 9, 5), CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        return (db, new RolloverService(db, ClockAt(D(2027, 6, 10))));
    }

    private static Enrollment Enroll(int student, int classId, DateOnly? end = null) =>
        new() { StudentId = student, ClassId = classId, StartDate = D(2026, 9, 5), EndDate = end, CreatedAt = DateTime.UtcNow };

    private static RolloverClassItem Item(int source, string name, byte grade) => new(source, name, grade);

    // ---- 4.2.35 Gợi ý ----
    [Fact]
    public async Task Overview_SuggestsNameAndGradePlusOne_AndNothingForGrade9()
    {
        var (_, svc) = Create();

        var rows = await svc.OverviewAsync(Year1, Year2, null);

        Assert.Equal(["6A1", "7A1", "9A1", "6B1"], rows.Select(r => r.SourceName));   // theo phân hiệu rồi khối rồi tên
        var r6 = rows.Single(r => r.SourceName == "6A1");
        Assert.Equal("7A1", r6.SuggestedName);
        Assert.Equal((byte)7, r6.SuggestedGrade);
        Assert.Null(rows.Single(r => r.SourceName == "9A1").SuggestedGrade);
        Assert.All(rows, r => Assert.Null(r.Target));
    }

    [Fact]
    public async Task Overview_CountsOnlyStudentsStillStudyingOnTheLastDayOfTheOldYear()
    {
        var (_, svc) = Create();

        var rows = await svc.OverviewAsync(Year1, Year2, null);

        Assert.Equal(2, rows.Single(r => r.SourceName == "6A1").StudentCount);   // 101, 102; bỏ em đã chuyển đi và em đã tốt nghiệp
        Assert.Equal(1, rows.Single(r => r.SourceName == "7A1").StudentCount);
    }

    [Fact]
    public async Task Overview_FiltersByCampus()
    {
        var (_, svc) = Create();
        Assert.Equal(["6B1"], (await svc.OverviewAsync(Year1, Year2, Campus2)).Select(r => r.SourceName));
    }

    [Fact]
    public async Task Overview_TargetClass_IsFoundByCopyLink_OrByMatchingNameAndGradeInTheSameCampus()
    {
        var (db, svc) = Create();
        await svc.CreateClassesAsync(new RolloverCreateRequest(Year1, Year2, [Item(C6A1, "7A1", 7)]));   // có liên kết lớp gốc
        AddClass(db, 31, Campus1, Year2, "8A1", 8);                                                        // tạo tay, không liên kết

        var rows = await svc.OverviewAsync(Year1, Year2, null);

        Assert.Equal("7A1", rows.Single(r => r.SourceName == "6A1").Target!.Name);
        Assert.Equal("8A1", rows.Single(r => r.SourceName == "7A1").Target!.Name);   // gợi ý 8A1, cùng phân hiệu, khối 8
        Assert.Null(rows.Single(r => r.SourceName == "6B1").Target);
    }

    [Fact]
    public async Task Overview_Grade9_IsNotMatchedToAnUnrelatedGrade9ClassOfTheNewYear()
    {
        var (db, svc) = Create();
        AddClass(db, 32, Campus1, Year2, "9A1", 9);   // lớp khối 9 năm sau là của cohort khối 8 lên, không phải kế tiếp của 9A1

        Assert.Null((await svc.OverviewAsync(Year1, Year2, null)).Single(r => r.SourceName == "9A1").Target);
    }

    [Fact]
    public async Task Overview_NewYearMustComeAfterTheOldYear()
    {
        var (_, svc) = Create();
        await Assert.ThrowsAsync<ApiException>(() => svc.OverviewAsync(Year2, Year1, null));
        await Assert.ThrowsAsync<ApiException>(() => svc.OverviewAsync(Year1, Year1, null));
    }

    // ---- 4.2.36 Tạo lớp ----
    [Fact]
    public async Task CreateClasses_MakesActiveClassesInTheNewYear_WithSourceLinkAndNothingElseCopied()
    {
        var (db, svc) = Create();

        var result = await svc.CreateClassesAsync(new RolloverCreateRequest(Year1, Year2, [Item(C6A1, "7A1", 7), Item(C6B1, "7B1", 7)]));

        Assert.Equal(2, result.Created);
        var created = await db.Class.Where(c => c.AcademicYearId == Year2).OrderBy(c => c.Name).ToListAsync();
        Assert.Equal(["7A1", "7B1"], created.Select(c => c.Name));
        Assert.All(created, c => Assert.True(c.IsActive));
        Assert.Equal([Campus1, Campus2], created.Select(c => c.CampusId));
        Assert.Equal([C6A1, C6B1], created.Select(c => c.CopiedFromClassId!.Value));
        // không mang theo trách nhiệm, thời khóa biểu hay học sinh của lớp cũ
        Assert.Equal(1, await db.HomeroomAssignment.CountAsync());
        Assert.Empty(db.TimetableEntry);
        Assert.Empty(db.TeachingSession);
        Assert.Equal(6, await db.Enrollment.CountAsync());
    }

    [Fact]
    public async Task CreateClasses_AllowsTheSameNameInAnotherCampus()
    {
        var (_, svc) = Create();
        var result = await svc.CreateClassesAsync(new RolloverCreateRequest(Year1, Year2, [Item(C6A1, "7A1", 7), Item(C6B1, "7A1", 7)]));
        Assert.Equal(2, result.Created);
    }

    [Fact]
    public async Task CreateClasses_DuplicateNameInTheListOrInTheNewYear_RejectsEverything()
    {
        var (db, svc) = Create();
        AddClass(db, 33, Campus1, Year2, "8A1", 8);

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateClassesAsync(
            new RolloverCreateRequest(Year1, Year2, [Item(C6A1, "7A1", 7), Item(C7A1, "7A1", 7)])));   // trùng trong danh sách
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateClassesAsync(
            new RolloverCreateRequest(Year1, Year2, [Item(C6A1, "7A1", 7), Item(C7A1, " 8a1 ", 8)])));   // trùng lớp đã có (không phân biệt hoa thường)

        Assert.Equal(1, await db.Class.CountAsync(c => c.AcademicYearId == Year2));   // không tạo lớp nào, kể cả lớp hợp lệ đứng trước
    }

    [Fact]
    public async Task CreateClasses_RunningTwice_DoesNotCreateASecondClassForTheSameSource()
    {
        var (db, svc) = Create();
        await svc.CreateClassesAsync(new RolloverCreateRequest(Year1, Year2, [Item(C6A1, "7A1", 7)]));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.CreateClassesAsync(
            new RolloverCreateRequest(Year1, Year2, [Item(C6A1, "7A9", 7)])));

        Assert.Contains("đã có lớp tương ứng", ex.Message);
        Assert.Equal(1, await db.Class.CountAsync(c => c.AcademicYearId == Year2));
    }

    [Fact]
    public async Task CreateClasses_InvalidInput_IsRejected()
    {
        var (_, svc) = Create();

        await Assert.ThrowsAsync<ApiException>(() => svc.CreateClassesAsync(new RolloverCreateRequest(Year1, Year2, [])));                          // rỗng
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateClassesAsync(new RolloverCreateRequest(Year1, Year2, [Item(C9A1, "10A1", 10)])));    // khối 10
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateClassesAsync(new RolloverCreateRequest(Year1, Year2, [Item(C6A1, "  ", 7)])));       // thiếu tên
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateClassesAsync(new RolloverCreateRequest(Year1, Year2, [Item(9999, "7A1", 7)])));      // lớp gốc không có
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateClassesAsync(new RolloverCreateRequest(Year1, Year2, [Item(C6A1, "7A1", 7), Item(C6A1, "7A2", 7)])));   // một lớp gốc hai lần
    }

    [Fact]
    public async Task CreateClasses_ForSourcesOfAnotherYear_IsRejected()
    {
        var (db, svc) = Create();
        AddClass(db, 34, Campus1, Year2, "7Z", 7);   // lớp thuộc chính năm đích
        await Assert.ThrowsAsync<ApiException>(() => svc.CreateClassesAsync(new RolloverCreateRequest(Year1, Year2, [Item(34, "8Z", 8)])));
    }

    // ---- 4.2.37 Chuyển nguyên lớp ----
    private static async Task<(MsmsDbContext Db, RolloverService Svc, int TargetOf6A1)> WithTarget()
    {
        var (db, svc) = Create();
        var created = await svc.CreateClassesAsync(new RolloverCreateRequest(Year1, Year2, [Item(C6A1, "7A1", 7)]));
        return (db, svc, created.Classes.Single().ClassId);
    }

    [Fact]
    public async Task Move_TransfersOnlyStudentsStillStudyingOnTheLastDay_FromTheStartOfTheNewYear()
    {
        var (db, svc, target) = await WithTarget();

        var result = await svc.MoveStudentsAsync(new RolloverMoveRequest([new RolloverMovePair(C6A1, target)]));

        Assert.Equal(2, result.Moved);
        Assert.Equal(0, result.Skipped);
        var moved = await db.Enrollment.Where(e => e.ClassId == target).ToListAsync();
        Assert.Equal([101, 102], moved.Select(e => e.StudentId).Order());
        Assert.All(moved, e => { Assert.Equal(Year2Start, e.StartDate); Assert.Null(e.EndDate); });
        Assert.DoesNotContain(moved, e => e.StudentId is Left or Graduated);
    }

    [Fact]
    public async Task Move_NeverTouchesTheOldYearsEnrollments()
    {
        var (db, svc, target) = await WithTarget();
        var before = await db.Enrollment.Where(e => e.ClassId == C6A1).Select(e => new { e.EnrollmentId, e.StartDate, e.EndDate, e.Reason }).ToListAsync();

        await svc.MoveStudentsAsync(new RolloverMoveRequest([new RolloverMovePair(C6A1, target)]));

        var after = await db.Enrollment.Where(e => e.ClassId == C6A1).Select(e => new { e.EnrollmentId, e.StartDate, e.EndDate, e.Reason }).ToListAsync();
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Move_StudentsAreInTheNewClassFromDayOneOfTheNewYear_AndNoLongerInTheOldOne()
    {
        var (db, svc, target) = await WithTarget();
        await svc.MoveStudentsAsync(new RolloverMoveRequest([new RolloverMovePair(C6A1, target)]));

        Assert.Equal(2, await db.Enrollment.RosterAt(target, Year2Start).CountAsync());
        Assert.Empty(await db.Enrollment.RosterAt(target, Year2Start.AddDays(-1)).ToListAsync());
        Assert.Contains(101, (await db.Enrollment.RosterAt(C6A1, Year1End).ToListAsync()).Select(e => e.StudentId));   // cuối năm cũ vẫn thấy lớp cũ
        Assert.Empty(await db.Enrollment.RosterAt(C6A1, Year2Start).ToListAsync());          // sang năm mới lớp cũ không còn ai
    }

    [Fact]
    public async Task Move_RunningAgain_CreatesNoDuplicateEnrollments()
    {
        var (db, svc, target) = await WithTarget();
        var request = new RolloverMoveRequest([new RolloverMovePair(C6A1, target)]);
        await svc.MoveStudentsAsync(request);
        var enrollmentsAfterFirst = await db.Enrollment.CountAsync();

        var again = await svc.MoveStudentsAsync(request);

        Assert.Equal(0, again.Moved);
        Assert.Equal(2, again.Skipped);
        Assert.Equal(enrollmentsAfterFirst, await db.Enrollment.CountAsync());
    }

    [Fact]
    public async Task Move_SkipsAStudentWhoAlreadyHasAnEnrollmentInTheNewYear()
    {
        var (db, svc, target) = await WithTarget();
        AddClass(db, 35, Campus1, Year2, "7Chuyên", 7);
        db.Enrollment.Add(new Enrollment { StudentId = 102, ClassId = 35, StartDate = Year2Start, CreatedAt = DateTime.UtcNow });   // em 102 đã được xếp riêng
        await db.SaveChangesAsync();

        var result = await svc.MoveStudentsAsync(new RolloverMoveRequest([new RolloverMovePair(C6A1, target)]));

        Assert.Equal(1, result.Moved);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(1, await db.Enrollment.CountAsync(e => e.StudentId == 102 && e.Class.AcademicYearId == Year2));
    }

    [Fact]
    public async Task Move_SeveralClassesInOneRequest_ReportsPerClassResults()
    {
        var (db, svc, target6A1) = await WithTarget();
        var created = await svc.CreateClassesAsync(new RolloverCreateRequest(Year1, Year2, [Item(C7A1, "8A1", 8)]));

        var result = await svc.MoveStudentsAsync(new RolloverMoveRequest(
            [new RolloverMovePair(C6A1, target6A1), new RolloverMovePair(C7A1, created.Classes.Single().ClassId)]));

        Assert.Equal(3, result.Moved);
        Assert.Equal([2, 1], result.Items.Select(i => i.Moved));
        Assert.Equal(3, await db.Enrollment.CountAsync(e => e.Class.AcademicYearId == Year2));
    }

    [Fact]
    public async Task Move_TwoOldClassesIntoOneNewClass_IsAllowed()
    {
        var (db, svc) = Create();
        AddClass(db, 36, Campus1, Year2, "Gộp", 7);

        var result = await svc.MoveStudentsAsync(new RolloverMoveRequest([new RolloverMovePair(C6A1, 36), new RolloverMovePair(C7A1, 36)]));

        Assert.Equal(3, result.Moved);
        Assert.Equal(3, await db.Enrollment.CountAsync(e => e.ClassId == 36));
    }

    [Fact]
    public async Task Move_InvalidPairs_AreRejected_AndMoveNothing()
    {
        var (db, svc, target) = await WithTarget();
        AddClass(db, 37, Campus1, Year1, "Cùng năm", 7);
        AddClass(db, 38, Campus1, Year2, "Đã đóng", 7, active: false);
        var enrollmentsBefore = await db.Enrollment.CountAsync();

        await Assert.ThrowsAsync<ApiException>(() => svc.MoveStudentsAsync(new RolloverMoveRequest([])));
        await Assert.ThrowsAsync<ApiException>(() => svc.MoveStudentsAsync(new RolloverMoveRequest([new RolloverMovePair(C6A1, 37)])));   // cùng năm học
        await Assert.ThrowsAsync<ApiException>(() => svc.MoveStudentsAsync(new RolloverMoveRequest([new RolloverMovePair(C6A1, 38)])));   // lớp đích ngừng hoạt động
        await Assert.ThrowsAsync<ApiException>(() => svc.MoveStudentsAsync(new RolloverMoveRequest([new RolloverMovePair(target, C6A1)]))); // chiều ngược
        await Assert.ThrowsAsync<ApiException>(() => svc.MoveStudentsAsync(new RolloverMoveRequest([new RolloverMovePair(C6A1, 9999)])));
        await Assert.ThrowsAsync<ApiException>(() => svc.MoveStudentsAsync(new RolloverMoveRequest(   // một lớp nguồn hai lần
            [new RolloverMovePair(C6A1, target), new RolloverMovePair(C6A1, target)])));
        // một cặp hợp lệ đi kèm một cặp sai thì không chuyển cặp nào
        await Assert.ThrowsAsync<ApiException>(() => svc.MoveStudentsAsync(new RolloverMoveRequest(
            [new RolloverMovePair(C6A1, target), new RolloverMovePair(C7A1, 37)])));

        Assert.Equal(enrollmentsBefore, await db.Enrollment.CountAsync());
    }

    [Fact]
    public async Task Overview_AfterMoving_ShowsTheStudentsAsAlreadyEnrolled()
    {
        var (_, svc, target) = await WithTarget();
        await svc.MoveStudentsAsync(new RolloverMoveRequest([new RolloverMovePair(C6A1, target)]));

        var row = (await svc.OverviewAsync(Year1, Year2, null)).Single(r => r.SourceName == "6A1");

        Assert.Equal(2, row.StudentCount);
        Assert.Equal(2, row.AlreadyEnrolled);
        Assert.Equal(target, row.Target!.ClassId);
    }
}
