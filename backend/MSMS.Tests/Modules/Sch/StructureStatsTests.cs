using MSMS.Api.Common.Auth;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Scd.Services;
using MSMS.Api.Modules.Sch.Services;
using MSMS.Tests.Modules.Iam;

namespace MSMS.Tests.Modules.Sch;

/// <summary>Thống kê cấu trúc trên màn Thiết lập: Principal, Vice Principal chỉ xem và chỉ trong phạm vi của mình (BR-01).</summary>
public class StructureStatsTests
{
    private static StructureStatsService Stats(AccountTestFixture f, string role, int personId)
    {
        var user = AccountTestFixture.User(role, personId);
        return new StructureStatsService(f.Db, user, new ScopeResolver(f.Db, user, f.Clock), f.Clock,
            new SubjectResponsibilityService(f.Db, f.Clock));
    }

    private static void Enroll(AccountTestFixture f, int studentId, int classId)
    {
        f.AddPerson(studentId, "Student");
        f.Db.Enrollment.Add(new Enrollment { StudentId = studentId, ClassId = classId, StartDate = f.Clock.Today.AddDays(-30) });
        f.Db.SaveChanges();
    }

    [Fact]
    public async Task VicePrincipal_SeesOnlyLedCampus_PrincipalSeesAll()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(1, AccountTypes.VicePrincipal);
        f.AddAccount(2, AccountTypes.Principal);
        f.AddPerson(3, campusId: 2);
        f.Db.CampusLeadership.Add(new CampusLeadership { StaffId = 1, CampusId = 1, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.SaveChanges();
        Enroll(f, 100, 1);
        Enroll(f, 101, 2);

        var vp = Assert.Single(await Stats(f, AccountTypes.VicePrincipal, 1).CampusesAsync());
        Assert.Equal(1, vp.CampusId);
        Assert.Equal(1, vp.StudentCount);
        Assert.Equal(2, vp.StaffCount);   // hiệu phó và hiệu trưởng cùng phân hiệu chính 1
        Assert.Equal("Nguyễn An 1", Assert.Single(vp.Leaders));

        var all = await Stats(f, AccountTypes.Principal, 2).CampusesAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(1, all.Single(c => c.CampusId == 2).StaffCount);
        Assert.Empty(all.Single(c => c.CampusId == 2).Leaders);
    }

    [Fact]
    public async Task Catalog_CountsHeadTeachersAndPublishedTeaching()
    {
        using var f = new AccountTestFixture();
        f.AddAccount(5);
        f.Db.StaffProfile.Find(5)!.DepartmentId = 1;
        f.Db.DepartmentHead.Add(new DepartmentHead { DepartmentId = 1, StaffId = 5, StartDate = f.Clock.Today.AddDays(-10) });
        f.Db.SaveChanges();
        f.Publish(1, 1, 5);
        f.Publish(2, 2, 5);

        var d = await Stats(f, AccountTypes.Principal, 2).CatalogAsync();
        var dept = Assert.Single(d.Departments);
        Assert.Equal("Nguyễn An 5", dept.HeadName);
        Assert.Equal(1, dept.TeacherCount);
        var subject = Assert.Single(d.Subjects);
        Assert.Equal(1, subject.TeacherCount);
        Assert.Equal(2, subject.ClassCount);
        Assert.Null(d.TeachersWithoutDepartment);   // chỉ School Admin cần (người sửa hồ sơ)
    }

    [Fact]
    public async Task StudentsWithoutClass_OnlyForSchoolAdmin()
    {
        using var f = new AccountTestFixture();
        f.AddPerson(100, "Student");
        Enroll(f, 101, 1);

        Assert.Equal(1, (await Stats(f, AccountTypes.SchoolAdmin, 900).ClassesAsync()).StudentsWithoutClass);
        Assert.Null((await Stats(f, AccountTypes.Principal, 2).ClassesAsync()).StudentsWithoutClass);
    }
}
