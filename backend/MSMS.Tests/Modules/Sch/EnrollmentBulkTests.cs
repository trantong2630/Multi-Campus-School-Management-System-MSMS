using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;
using static MSMS.Tests.Modules.Sch.SchTestSupport;

namespace MSMS.Tests.Modules.Sch;

/// <summary>Ghi danh, chuyển lớp, kết thúc ghi danh cho nhiều học sinh cùng lúc (SCH-05).</summary>
public class EnrollmentBulkTests
{
    private const int Campus1 = 1, Year1 = 1, ClassA = 11, ClassB = 12;
    private static readonly DateOnly Start = new(2026, 9, 5), End = new(2027, 5, 31);

    private static (MsmsDbContext Db, EnrollmentService Svc) Create()
    {
        var db = NewDb();
        var clock = ClockAt(new DateOnly(2026, 10, 1));
        var user = UserOf(AccountTypes.SchoolAdmin);
        AddCampus(db, Campus1, "Phân hiệu 1");
        AddYear(db, Year1, "2026-2027", Start, End, current: true);
        AddClass(db, ClassA, Campus1, Year1, "6A1", 6);
        AddClass(db, ClassB, Campus1, Year1, "6A2", 6);
        foreach (var (id, status) in new[] { (101, "Studying"), (102, "Studying"), (103, "Studying"), (104, "Transferred") })
        {
            db.Person.Add(new Person { PersonId = id, PersonType = "Student", PersonCode = $"HS{id}", FullName = $"Học sinh {id}", CreatedAt = DateTime.UtcNow });
            db.StudentProfile.Add(new StudentProfile { StudentId = id, Status = status });
        }
        db.SaveChanges();
        var svc = new EnrollmentService(db, clock, user, new ScopeResolver(db, user, clock), new AuditWriter(db, user, clock),
            new SessionService(db, clock, Options.Create(new JwtOptions())));
        return (db, svc);
    }

    [Fact]
    public async Task EnrollMany_AddsEveryChosenStudentWithTheSameStartDate()
    {
        var (db, svc) = Create();

        await svc.EnrollManyAsync(ClassA, new EnrollManyRequest([101, 102, 103], Start));

        var rows = await db.Enrollment.Where(e => e.ClassId == ClassA).ToListAsync();
        Assert.Equal([101, 102, 103], rows.Select(e => e.StudentId).Order());
        Assert.All(rows, e => Assert.Equal(Start, e.StartDate));
    }

    [Fact]
    public async Task EnrollMany_OneInvalidStudent_EnrollsNobody_AndNamesTheStudent()
    {
        var (db, svc) = Create();
        await svc.EnrollAsync(ClassB, new EnrollRequest(102, Start));   // 102 đã có lớp trong năm

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.EnrollManyAsync(ClassA, new EnrollManyRequest([101, 102], Start)));
        await Assert.ThrowsAsync<ApiException>(() => svc.EnrollManyAsync(ClassA, new EnrollManyRequest([101, 104], Start)));   // đã chuyển trường
        await Assert.ThrowsAsync<ApiException>(() => svc.EnrollManyAsync(ClassA, new EnrollManyRequest([], Start)));
        await Assert.ThrowsAsync<ApiException>(() => svc.EnrollManyAsync(ClassA, new EnrollManyRequest([101], Start.AddDays(-1))));   // ngoài năm học

        Assert.Contains("Học sinh 102", ex.Message);
        Assert.Empty(db.Enrollment.Where(e => e.ClassId == ClassA));
    }

    [Fact]
    public async Task TransferMany_MovesAllChosenStudents_ClosingTheOldEnrollmentTheDayBefore()
    {
        var (db, svc) = Create();
        await svc.EnrollManyAsync(ClassA, new EnrollManyRequest([101, 102], Start));
        var ids = await db.Enrollment.Select(e => e.EnrollmentId).ToListAsync();
        var effective = new DateOnly(2026, 10, 10);

        await svc.TransferManyAsync(new TransferManyRequest(ids, ClassB, effective));

        Assert.All(await db.Enrollment.Where(e => e.ClassId == ClassA).ToListAsync(), e =>
        {
            Assert.Equal(effective.AddDays(-1), e.EndDate);
            Assert.Equal(EnrollmentCloseReasons.ClassTransfer, e.Reason);
        });
        Assert.Equal(2, await db.Enrollment.CountAsync(e => e.ClassId == ClassB && e.StartDate == effective));
    }

    [Fact]
    public async Task TransferMany_OneInvalidEnrollment_MovesNobody()
    {
        var (db, svc) = Create();
        await svc.EnrollManyAsync(ClassA, new EnrollManyRequest([101], Start));
        await svc.EnrollManyAsync(ClassB, new EnrollManyRequest([102], Start));
        var ids = await db.Enrollment.Select(e => e.EnrollmentId).ToListAsync();   // một em đã ở sẵn lớp đích

        await Assert.ThrowsAsync<ApiException>(() => svc.TransferManyAsync(new TransferManyRequest(ids, ClassB, new DateOnly(2026, 10, 10))));

        db.ChangeTracker.Clear();   // bỏ các thay đổi chưa lưu của yêu cầu bị từ chối
        Assert.Equal(2, await db.Enrollment.CountAsync());
        Assert.All(db.Enrollment, e => Assert.Null(e.EndDate));
    }

    [Fact]
    public async Task CloseMany_EndsEveryChosenEnrollment_TodayWithTheReason()
    {
        var (db, svc) = Create();
        await svc.EnrollManyAsync(ClassA, new EnrollManyRequest([101, 102], Start));
        var ids = await db.Enrollment.Select(e => e.EnrollmentId).ToListAsync();

        await svc.CloseManyAsync(new CloseManyRequest(ids, EnrollmentCloseReasons.ClassTransfer));

        Assert.All(await db.Enrollment.ToListAsync(), e =>
        {
            Assert.Equal(new DateOnly(2026, 10, 1), e.EndDate);
            Assert.Equal(EnrollmentCloseReasons.ClassTransfer, e.Reason);
        });
    }

    [Fact]
    public async Task CloseMany_Invalid_IsRejected()
    {
        var (db, svc) = Create();
        await svc.EnrollManyAsync(ClassA, new EnrollManyRequest([101], Start));
        var id = await db.Enrollment.Select(e => e.EnrollmentId).SingleAsync();

        await Assert.ThrowsAsync<ApiException>(() => svc.CloseManyAsync(new CloseManyRequest([id], "Bogus")));
        await Assert.ThrowsAsync<ApiException>(() => svc.CloseManyAsync(new CloseManyRequest([], EnrollmentCloseReasons.ClassTransfer)));
        await svc.CloseManyAsync(new CloseManyRequest([id], EnrollmentCloseReasons.ClassTransfer));
        await Assert.ThrowsAsync<ApiException>(() => svc.CloseManyAsync(new CloseManyRequest([id], EnrollmentCloseReasons.ClassTransfer)));   // đã kết thúc
    }
}
