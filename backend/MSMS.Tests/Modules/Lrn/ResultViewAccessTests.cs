using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Lrn.Services;
using MSMS.Api.Modules.Scd.Services;
using MSMS.Tests.Modules.Ppl;

namespace MSMS.Tests.Modules.Lrn;

/// <summary>BR-22: kết quả bài đã giao chỉ dành cho giáo viên đang phụ trách chuyên môn ở (lớp, môn); tổ trưởng chỉ có số tổng hợp ở ADM-01.</summary>
public class ResultViewAccessTests
{
    private const int Subject = 1, Assignment = 1;

    private static PplEnv Setup()
    {
        var e = new PplEnv(AccountTypes.Teacher, PplEnv.Teacher);
        e.Db.Subject.Add(new Subject { SubjectId = Subject, DepartmentId = PplEnv.Dept1, Name = "Toán", Abbreviation = "TO", IsActive = true });
        e.Db.Assignment.Add(new Assignment
        {
            AssignmentId = Assignment, SubjectId = Subject, ClassId = PplEnv.Class6A, Title = "Bài 1", OpenAt = e.Clock.Now, DueAt = e.Clock.Now.AddDays(1),
            AssignedByPersonId = PplEnv.OtherTeacher, CreatedAt = e.Clock.Now,
        });
        e.Db.SaveChanges();
        e.Seed("department", PplEnv.Dept1, PplEnv.Teacher, PplEnv.Today.AddDays(-30));
        return e;
    }

    [Fact(DisplayName = "LRN | Xem kết quả | Tổ trưởng của môn nhưng không phụ trách lớp | báo không tìm thấy bài")]
    public async Task DepartmentHead_WithoutResponsibility_CannotViewResults()
    {
        using var e = Setup();
        var access = new LrnAccess(e.Db, e.User, new SubjectResponsibilityService(e.Db, e.Clock));

        var ex = await Assert.ThrowsAsync<ApiException>(() => access.LoadForViewAsync(Assignment));
        Assert.Equal(404, ex.StatusCode);
    }
}
