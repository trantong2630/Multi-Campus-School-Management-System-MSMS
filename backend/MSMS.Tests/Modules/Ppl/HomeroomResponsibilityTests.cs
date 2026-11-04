using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Mỗi giáo viên chỉ chủ nhiệm một lớp trong một năm học (PPL-04, 4.3.30).</summary>
public class HomeroomResponsibilityTests
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    private static (AccountTestFixture f, ResponsibilityService svc) Setup()
    {
        var f = new AccountTestFixture();
        f.AddAccount(20, AccountTypes.Teacher);
        f.AddAccount(21, AccountTypes.Teacher);
        // Lớp của năm học sau, để kiểm tra việc xếp trước chủ nhiệm cho năm sau vẫn được.
        f.Db.AcademicYear.Add(new AcademicYear { AcademicYearId = 2, Name = "2027-2028", IsCurrent = false, StartDate = new(2027, 9, 1), EndDate = new(2028, 5, 31) });
        f.Db.Class.Add(new Class { ClassId = 3, CampusId = 1, AcademicYearId = 2, Name = "7A", IsActive = true });
        f.Db.SaveChanges();
        var user = AccountTestFixture.User(AccountTypes.SchoolAdmin, 900);
        return (f, new ResponsibilityService(f.Db, user, f.Clock, new ScopeResolver(f.Db, user, f.Clock)));
    }

    private static ResponsibilityAssignRequest Assign(int classId, int staffId, DateOnly start) =>
        new(ResponsibilityKinds.Homeroom, classId, staffId, start);

    [Fact]
    public async Task Teacher_with_a_homeroom_class_cannot_get_a_second_class_in_the_same_year()
    {
        var (f, svc) = Setup();
        await svc.AssignAsync(Assign(1, 20, Today));

        var ex = await Assert.ThrowsAsync<ApiException>(() => svc.AssignAsync(Assign(2, 20, Today)));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("chỉ chủ nhiệm một lớp", ex.Message);
        Assert.Single(f.Db.HomeroomAssignment);
    }

    [Fact]
    public async Task A_class_of_another_academic_year_is_allowed()
    {
        var (f, svc) = Setup();
        await svc.AssignAsync(Assign(1, 20, Today));

        await svc.AssignAsync(Assign(3, 20, new DateOnly(2027, 9, 1)));

        Assert.Equal(2, f.Db.HomeroomAssignment.Count());
    }

    [Fact]
    public async Task After_the_first_homeroom_ended_the_teacher_can_take_another_class()
    {
        var (f, svc) = Setup();
        await svc.AssignAsync(Assign(1, 20, Today.AddDays(-30)));
        f.Db.HomeroomAssignment.Single().EndDate = Today.AddDays(-1);
        await f.Db.SaveChangesAsync();

        await svc.AssignAsync(Assign(2, 20, Today));

        Assert.Equal(2, f.Db.HomeroomAssignment.Count());
    }

    [Fact]
    public async Task Replacing_the_homeroom_teacher_of_the_same_class_is_not_blocked()
    {
        var (f, svc) = Setup();
        await svc.AssignAsync(Assign(1, 20, Today.AddDays(-10)));

        await svc.AssignAsync(Assign(1, 21, Today));   // thay người ở chính lớp đó
        await svc.AssignAsync(Assign(2, 20, Today));   // giáo viên cũ được giải phóng

        Assert.Equal(3, f.Db.HomeroomAssignment.Count());
    }
}