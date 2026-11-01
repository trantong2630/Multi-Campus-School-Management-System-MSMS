using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Data.Entities;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Authorization;

/// <summary>WBS 4.1.8: từng loại tài khoản và từng trách nhiệm dùng dữ liệu/phạm vi riêng.</summary>
public class IamScopeTests
{
    [Theory]
    [InlineData(AccountTypes.Principal, true)]
    [InlineData(AccountTypes.SchoolAdmin, true)]
    [InlineData(AccountTypes.VicePrincipal, false)]
    [InlineData(AccountTypes.Teacher, false)]
    [InlineData(AccountTypes.Student, false)]
    [InlineData(AccountTypes.Parent, false)]
    public async Task Roles_ResolveSchoolWideFlagWithoutInheritingUnrelatedDuties(string role, bool schoolWide)
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, role);
        var scope = await new AccountScopeResolver(f.Db, f.Clock).ResolveAsync(1, role);
        Assert.Equal(schoolWide, scope.IsSchoolWide);
        Assert.Equal(f.Clock.Today, scope.AsOfDate);
        Assert.Empty(scope.LedCampusIds);
        Assert.Empty(scope.HomeroomClassIds);
        Assert.Empty(scope.HeadDepartmentIds);
        Assert.Empty(scope.SubjectResponsibilities);
        Assert.Empty(scope.Enrollments);
        Assert.Empty(scope.LinkedStudentIds);
    }

    [Fact]
    public async Task VicePrincipal_UsesEffectiveCampus_NotNewestOrAnotherPersonsCampus()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        f.AddAccount(2, AccountTypes.VicePrincipal);
        f.Db.CampusLeadership.AddRange(
            new CampusLeadership { StaffId = 1, CampusId = 1, StartDate = f.Clock.Today.AddDays(-10) },
            new CampusLeadership { StaffId = 1, CampusId = 2, StartDate = f.Clock.Today.AddDays(10) },
            new CampusLeadership { StaffId = 2, CampusId = 2, StartDate = f.Clock.Today.AddDays(-10) });
        await f.Db.SaveChangesAsync();
        var resolver = new ScopeResolver(f.Db, AccountTestFixture.User(AccountTypes.VicePrincipal, 1), f.Clock);
        Assert.Equal(new[] { 1 }, await resolver.LedCampusIdsAsync());
        Assert.True(await resolver.CanManageCampusAsync(1));
        Assert.False(await resolver.CanReadCampusAsync(2));
        var classes = await (await resolver.FilterClassesAsync(f.Db.Class)).ToListAsync();
        Assert.Equal(1, Assert.Single(classes).CampusId);
    }

    [Fact]
    public async Task Teacher_CombinesHomeroomAndPublishedSubjects_HeadDutyDoesNotGrantUnassignedClasses()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1);
        f.AddAccount(2);
        f.Db.HomeroomAssignment.Add(new HomeroomAssignment { StaffId = 1, ClassId = 1, StartDate = f.Clock.Today });
        f.Db.DepartmentHead.Add(new DepartmentHead { StaffId = 1, DepartmentId = 1, StartDate = f.Clock.Today });
        await f.Db.SaveChangesAsync();
        f.Publish(1, 1, 1, 5);
        f.Publish(2, 1, 2, 2); // Lớp 1 đã đổi giáo viên: không lấy bản cũ của người 1.
        f.Publish(3, 2, 1, 1);
        f.Publish(4, 2, 2, 0, "Draft"); // Bản nháp không thay người đang phụ trách.
        var scope = await new AccountScopeResolver(f.Db, f.Clock).ResolveAsync(1, AccountTypes.Teacher);
        Assert.Equal(new[] { 1 }, scope.HomeroomClassIds);
        Assert.Equal(new[] { 1 }, scope.HeadDepartmentIds);
        Assert.Equal(2, Assert.Single(scope.SubjectResponsibilities).ClassId);
        var resolver = new ScopeResolver(f.Db, AccountTestFixture.User(AccountTypes.Teacher, 1), f.Clock);
        Assert.Equal(new[] { 1, 2 }, await (await resolver.FilterClassesAsync(f.Db.Class)).OrderBy(c => c.ClassId).Select(c => c.ClassId).ToArrayAsync());
        Assert.False(await resolver.CanManageCampusAsync(1));
        Assert.Empty((await new AccountScopeResolver(f.Db, f.Clock).ResolveAsync(2, AccountTypes.Teacher)).HeadDepartmentIds);
    }

    [Fact]
    public async Task TeacherWithoutAssignment_AndOutsideCurrentSemester_HasNoSubjectScope()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1);
        f.AddAccount(2);
        f.Publish(1, 1, 2);
        var resolver = new AccountScopeResolver(f.Db, f.Clock);
        Assert.Empty((await resolver.ResolveAsync(1, AccountTypes.Teacher)).SubjectResponsibilities);
        f.Clock.Now = new(2027, 2, 1);
        Assert.Empty((await resolver.ResolveAsync(2, AccountTypes.Teacher)).SubjectResponsibilities);
    }

    [Theory]
    [InlineData(-2, -1, false)]
    [InlineData(-2, 0, false)]
    [InlineData(0, 1, true)]
    [InlineData(1, 2, false)]
    public async Task Responsibilities_BoundariesAreCheckedIndependentlyForAllThreeDuties(int start, int end, bool effective)
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        f.AddAccount(2);
        var from = f.Clock.Today.AddDays(start);
        var to = f.Clock.Today.AddDays(end);
        f.Db.CampusLeadership.Add(new CampusLeadership { StaffId = 1, CampusId = 1, StartDate = from, EndDate = to });
        f.Db.DepartmentHead.Add(new DepartmentHead { StaffId = 2, DepartmentId = 1, StartDate = from, EndDate = to });
        f.Db.HomeroomAssignment.Add(new HomeroomAssignment { StaffId = 2, ClassId = 1, StartDate = from, EndDate = to });
        await f.Db.SaveChangesAsync();
        var resolver = new AccountScopeResolver(f.Db, f.Clock);
        Assert.Equal(effective, (await resolver.ResolveAsync(1, AccountTypes.VicePrincipal)).LedCampusIds.Any());
        var teacher = await resolver.ResolveAsync(2, AccountTypes.Teacher);
        Assert.Equal(effective, teacher.HomeroomClassIds.Any());
        Assert.Equal(effective, teacher.HeadDepartmentIds.Any());
    }

    [Fact]
    public async Task Student_SeesOnlyOwnEffectiveEnrollment_ParentOnlyLinkedChildren()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.Student);
        f.AddAccount(2, AccountTypes.Student);
        f.AddAccount(3, AccountTypes.Student);
        f.AddAccount(10, AccountTypes.Parent);
        f.AddAccount(11, AccountTypes.Parent);
        f.Db.Enrollment.AddRange(
            new Enrollment { StudentId = 1, ClassId = 1, StartDate = f.Clock.Today.AddDays(-10) },
            new Enrollment { StudentId = 1, ClassId = 2, StartDate = f.Clock.Today.AddDays(10) },
            new Enrollment { StudentId = 2, ClassId = 2, StartDate = f.Clock.Today.AddDays(-10) },
            new Enrollment { StudentId = 3, ClassId = 1, StartDate = f.Clock.Today.AddDays(-10), EndDate = f.Clock.Today.AddDays(-1) });
        f.Db.ParentStudentLink.AddRange(new ParentStudentLink { ParentId = 10, StudentId = 1 },
            new ParentStudentLink { ParentId = 10, StudentId = 3 }, new ParentStudentLink { ParentId = 11, StudentId = 2 });
        await f.Db.SaveChangesAsync();
        var resolver = new AccountScopeResolver(f.Db, f.Clock);
        var student = await resolver.ResolveAsync(1, AccountTypes.Student);
        Assert.Equal(1, Assert.Single(student.Enrollments).ClassId);
        Assert.Empty(student.LinkedStudentIds);
        var parent = await resolver.ResolveAsync(10, AccountTypes.Parent);
        Assert.Equal(new[] { 1, 3 }, parent.LinkedStudentIds);
        Assert.Equal(1, Assert.Single(parent.Enrollments).StudentId);
        Assert.Equal(2, Assert.Single((await resolver.ResolveAsync(11, AccountTypes.Parent)).Enrollments).StudentId);
        f.Clock.Now = new(2027, 6, 1);
        Assert.Empty((await resolver.ResolveAsync(1, AccountTypes.Student)).Enrollments);
    }
}
