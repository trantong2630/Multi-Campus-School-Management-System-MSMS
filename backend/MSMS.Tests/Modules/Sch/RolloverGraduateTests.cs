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

/// <summary>Kết chuyển lớp 9: bấm mũi tên phải là cho tốt nghiệp (không có khối 10).</summary>
public class RolloverGraduateTests
{
    private const int Campus1 = 1, Year1 = 1, Year2 = 2, C9A1 = 11, C6A1 = 12;
    private static readonly DateOnly Year1End = new(2027, 5, 31);

    private static (MsmsDbContext Db, EnrollmentService Svc, RolloverService Rollover) Create()
    {
        var db = NewDb();
        var clock = ClockAt(new DateOnly(2027, 6, 10));
        var user = UserOf(AccountTypes.SchoolAdmin);
        AddCampus(db, Campus1, "Phân hiệu 1");
        AddYear(db, Year1, "2026-2027", new DateOnly(2026, 9, 5), Year1End, current: true);
        AddYear(db, Year2, "2027-2028", new DateOnly(2027, 9, 5), new DateOnly(2028, 5, 31));
        AddClass(db, C9A1, Campus1, Year1, "9A1", 9);
        AddClass(db, C6A1, Campus1, Year1, "6A1", 6);
        foreach (var id in new[] { 101, 102, 103 })
        {
            db.Person.Add(new Person { PersonId = id, PersonType = "Student", PersonCode = $"HS{id}", FullName = $"Học sinh {id}", CreatedAt = DateTime.UtcNow });
            db.StudentProfile.Add(new StudentProfile { StudentId = id, Status = "Studying" });
        }
        db.Enrollment.AddRange(
            new Enrollment { StudentId = 101, ClassId = C9A1, StartDate = new DateOnly(2026, 9, 5), CreatedAt = DateTime.UtcNow },
            new Enrollment { StudentId = 102, ClassId = C9A1, StartDate = new DateOnly(2026, 9, 5), CreatedAt = DateTime.UtcNow },
            new Enrollment { StudentId = 103, ClassId = C6A1, StartDate = new DateOnly(2026, 9, 5), CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        var svc = new EnrollmentService(db, clock, user, new ScopeResolver(db, user, clock), new AuditWriter(db, user, clock),
            new SessionService(db, clock, Options.Create(new JwtOptions())));
        return (db, svc, new RolloverService(db, clock));
    }

    [Fact]
    public async Task Graduate_EndsEnrollment_SetsStatus_OnlyForTheChosenStudents()
    {
        var (db, svc, _) = Create();

        Assert.Equal(1, await svc.GraduateAsync(C9A1, new GraduateRequest([101])));

        var e = await db.Enrollment.SingleAsync(x => x.StudentId == 101);
        Assert.Equal(Year1End, e.EndDate);
        Assert.Equal(EnrollmentCloseReasons.Graduated, e.Reason);
        Assert.Equal("Graduated", (await db.StudentProfile.FindAsync(101))!.Status);
        Assert.Equal("Studying", (await db.StudentProfile.FindAsync(102))!.Status);
    }

    [Fact]
    public async Task Graduate_RejectsNonFinalGrade_OtherClassStudents_AndEmptyList()
    {
        var (_, svc, _) = Create();

        await Assert.ThrowsAsync<ApiException>(() => svc.GraduateAsync(C6A1, new GraduateRequest([103])));   // chưa phải khối cuối
        await Assert.ThrowsAsync<ApiException>(() => svc.GraduateAsync(C9A1, new GraduateRequest([103])));   // em của lớp khác
        await Assert.ThrowsAsync<ApiException>(() => svc.GraduateAsync(C9A1, new GraduateRequest([])));
    }

    [Fact]
    public async Task Graduate_Twice_IsRejected_AndGraduatesStayVisibleInTheRolloverList()
    {
        var (_, svc, rollover) = Create();
        await svc.GraduateAsync(C9A1, new GraduateRequest([101]));

        await Assert.ThrowsAsync<ApiException>(() => svc.GraduateAsync(C9A1, new GraduateRequest([101])));
        var list = await rollover.StudentsAsync(C9A1, Year2);

        Assert.Equal([101, 102], list.Select(s => s.StudentId));
        Assert.True(list.Single(s => s.StudentId == 101).Graduated);
        Assert.False(list.Single(s => s.StudentId == 102).Graduated);
    }
}
