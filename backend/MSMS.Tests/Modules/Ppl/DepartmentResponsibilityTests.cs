using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Mỗi giáo viên chỉ làm tổ trưởng một tổ tại một thời điểm (PPL-04, 4.3.29).</summary>
public class DepartmentResponsibilityTests
{
    private static DateOnly T => PplEnv.Today;
    private static PplEnv Env()
    {
        var e = new PplEnv();
        e.Db.SubjectDepartment.Add(new SubjectDepartment { DepartmentId = 3, Name = "Tổ Văn", IsActive = true });
        e.Db.SaveChanges();
        return e;
    }
    private static ResponsibilityAssignRequest Assign(int dept, int staff, DateOnly start) => new("department", dept, staff, start);

    [Fact(DisplayName = "PPL | Tổ trưởng | Một người một tổ | giáo viên đã là tổ trưởng một tổ thì không nhận thêm tổ khác, thông báo nêu tên tổ đang giữ")]
    public async Task SecondDepartment_Blocked()
    {
        using var e = Env();
        await e.Resp.AssignAsync(Assign(PplEnv.Dept1, PplEnv.Teacher, T));
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Resp.AssignAsync(Assign(3, PplEnv.Teacher, T)));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("Tổ Toán", ex.Message);
        Assert.Single(e.Db.DepartmentHead);
    }

    [Fact(DisplayName = "PPL | Tổ trưởng | Một người một tổ | người được hẹn làm tổ trưởng từ ngày mai cũng chặn tổ khác")]
    public async Task UpcomingAssignment_AlsoBlocks()
    {
        using var e = Env();
        await e.Resp.AssignAsync(Assign(PplEnv.Dept1, PplEnv.Teacher, T.AddDays(5)));
        await Assert.ThrowsAsync<ApiException>(() => e.Resp.AssignAsync(Assign(3, PplEnv.Teacher, T)));
    }

    [Fact(DisplayName = "PPL | Tổ trưởng | Một người một tổ | sau khi kết thúc trách nhiệm cũ thì nhận được tổ khác")]
    public async Task AfterFirstEnded_AnotherDepartmentAllowed()
    {
        using var e = Env();
        e.Db.DepartmentHead.Add(new DepartmentHead { DepartmentId = PplEnv.Dept1, StaffId = PplEnv.Teacher, StartDate = T.AddDays(-30), EndDate = T.AddDays(-1) });
        e.Db.SaveChanges();
        await e.Resp.AssignAsync(Assign(3, PplEnv.Teacher, T));
        Assert.Equal(2, await e.Db.DepartmentHead.CountAsync());
    }

    [Fact(DisplayName = "PPL | Tổ trưởng | Một người một tổ | thay tổ trưởng của chính tổ đó không bị chặn, người cũ được giải phóng sang tổ khác")]
    public async Task Replacing_SameDepartment_NotBlocked()
    {
        using var e = Env();
        e.Db.DepartmentHead.Add(new DepartmentHead { DepartmentId = PplEnv.Dept1, StaffId = PplEnv.Teacher, StartDate = T.AddDays(-10) });
        e.Db.SaveChanges();
        await e.Resp.AssignAsync(Assign(PplEnv.Dept1, PplEnv.OtherTeacher, T));    // thay người ở chính tổ đó
        await e.Resp.AssignAsync(Assign(3, PplEnv.Teacher, T));                    // giáo viên cũ nhận tổ khác
        Assert.Equal(3, await e.Db.DepartmentHead.CountAsync());
    }

    [Fact(DisplayName = "PPL | Tổ trưởng | Một người một tổ | giáo viên khác nhau làm tổ trưởng hai tổ khác nhau thì không ảnh hưởng nhau")]
    public async Task DifferentTeachers_DifferentDepartments_Allowed()
    {
        using var e = Env();
        await e.Resp.AssignAsync(Assign(PplEnv.Dept1, PplEnv.Teacher, T));
        await e.Resp.AssignAsync(Assign(3, PplEnv.OtherTeacher, T));
        Assert.Equal(2, await e.Db.DepartmentHead.CountAsync());
    }

    [Fact(DisplayName = "PPL | Tổ trưởng | Một người một tổ | danh sách hiện người sắp nhận (mã người) để giao diện làm mờ họ ở tổ khác")]
    public async Task List_ExposesNextHolderId()
    {
        using var e = Env();
        await e.Resp.AssignAsync(Assign(PplEnv.Dept1, PplEnv.Teacher, T.AddDays(3)));
        var row = (await e.Resp.ListCurrentAsync("department", null)).Single(r => r.TargetId == PplEnv.Dept1);
        Assert.Equal(PplEnv.Teacher, row.NextStaffId);
    }
}