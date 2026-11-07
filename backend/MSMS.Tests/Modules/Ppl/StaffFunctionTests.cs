using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Chức năng danh bạ cán bộ: tìm, lọc, phạm vi theo vai trò, ngừng hoạt động (PPL-01, 4.3.2-4.3.3, 4.3.7).</summary>
public class StaffFunctionTests
{
    private static Task<MSMS.Api.Common.Errors.PagedResult<MSMS.Api.Modules.Ppl.Dtos.StaffDto>> List(PplEnv e, int? campus = null, int? dept = null, string? type = null,
        bool? hasAbbr = null, string? search = null, bool inactive = false, int page = 1, int size = 50) => e.Staff.ListAsync(campus, dept, type, hasAbbr, search, inactive, page, size);

    [Fact(DisplayName = "PPL | Cán bộ | Danh bạ | mặc định chỉ hiện cán bộ đang hoạt động; bật 'tất cả hồ sơ' thì hiện cả người đã nghỉ")]
    public async Task List_ActiveOnly_ByDefault()
    {
        using var e = new PplEnv();
        var active = await List(e);
        Assert.DoesNotContain(active.Items, s => s.StaffId == PplEnv.InactiveStaff);
        Assert.Equal(active.Total + 1, (await List(e, inactive: true)).Total);
    }

    [Theory(DisplayName = "PPL | Cán bộ | Danh bạ | lọc theo loại tài khoản")]
    [InlineData(AccountTypes.Teacher, 3)] [InlineData(AccountTypes.VicePrincipal, 1)] [InlineData(AccountTypes.Principal, 1)] [InlineData(AccountTypes.SchoolAdmin, 1)] [InlineData(AccountTypes.Parent, 0)]
    public async Task List_FilterByAccountType(string type, int expected)
    {
        using var e = new PplEnv();
        Assert.Equal(expected, (await List(e, type: type)).Total);
    }

    [Fact(DisplayName = "PPL | Cán bộ | Danh bạ | lọc theo phân hiệu chính và tổ chuyên môn")]
    public async Task List_FilterByCampusAndDepartment()
    {
        using var e = new PplEnv();
        Assert.Equal((await List(e)).Total, (await List(e, campus: PplEnv.Campus1)).Total);
        Assert.Equal(0, (await List(e, campus: PplEnv.CampusInactive)).Total);
        Assert.Equal(2, (await List(e, dept: PplEnv.Dept1)).Total);
    }

    [Fact(DisplayName = "PPL | Cán bộ | Danh bạ | lọc theo có hoặc chưa có viết tắt giáo viên")]
    public async Task List_FilterByAbbr()
    {
        using var e = new PplEnv();
        Assert.Equal(2, (await List(e, hasAbbr: true)).Total);
        Assert.Equal((await List(e)).Total - 2, (await List(e, hasAbbr: false)).Total);
    }

    [Fact(DisplayName = "PPL | Cán bộ | Danh bạ | tìm theo tên, mã hồ sơ và viết tắt chính xác")]
    public async Task List_Search()
    {
        using var e = new PplEnv();
        Assert.Equal(1, (await List(e, search: "Giáo viên A")).Total);
        Assert.Equal(1, (await List(e, search: "CB000010")).Total);
        Assert.Equal(1, (await List(e, search: "ABC")).Total);
        Assert.Equal(0, (await List(e, search: "AB")).Total);   // viết tắt phải khớp đúng, không tìm gần đúng
        Assert.Equal(0, (await List(e, search: "không có")).Total);
    }

    [Fact(DisplayName = "PPL | Cán bộ | Danh bạ | phân trang")]
    public async Task List_Paging()
    {
        using var e = new PplEnv();
        var p1 = await List(e, page: 1, size: 3);
        var p2 = await List(e, page: 2, size: 3);
        Assert.Equal(3, p1.Items.Count);
        Assert.Empty(p1.Items.Select(s => s.StaffId).Intersect(p2.Items.Select(s => s.StaffId)));
    }

    [Fact(DisplayName = "PPL | Cán bộ | Danh bạ | Hiệu phó chỉ thấy cán bộ thuộc phân hiệu mình phụ trách")]
    public async Task List_ViceScope()
    {
        using (var none = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP))
            Assert.Empty((await List(none)).Items);
        using var led = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP);
        led.Db.CampusLeadership.Add(new CampusLeadership { CampusId = PplEnv.Campus1, StaffId = PplEnv.ViceP, StartDate = PplEnv.Today.AddDays(-3) });
        led.Db.SaveChanges();
        Assert.True((await List(led)).Total > 0);
    }

    [Theory(DisplayName = "PPL | Cán bộ | Danh bạ | giáo viên, học sinh, phụ huynh không xem được")]
    [InlineData(AccountTypes.Teacher)] [InlineData(AccountTypes.Student)] [InlineData(AccountTypes.Parent)]
    public async Task List_NonManagement_Forbidden(string type)
    {
        using var e = new PplEnv(type, 777);
        var ex = await Assert.ThrowsAsync<ApiException>(() => List(e));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact(DisplayName = "PPL | Cán bộ | Xem một hồ sơ | không tồn tại trả 404")]
    public async Task Get_Unknown_NotFound()
    {
        using var e = new PplEnv();
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Staff.GetAsync(9999));
        Assert.Equal(404, ex.StatusCode);
    }

    // ---------- Ngừng hoạt động ----------
    [Fact(DisplayName = "PPL | Cán bộ | Ngừng hoạt động | cán bộ không giữ trách nhiệm nào ngừng được rồi kích hoạt lại")]
    public async Task Deactivate_Then_Reactivate()
    {
        using var e = new PplEnv();
        await e.Staff.SetActiveAsync(PplEnv.Teacher, false);
        Assert.False((await e.Db.StaffProfile.FindAsync(PplEnv.Teacher))!.IsActive);
        await e.Staff.SetActiveAsync(PplEnv.Teacher, true);
        Assert.True((await e.Db.StaffProfile.FindAsync(PplEnv.Teacher))!.IsActive);
    }

    [Theory(DisplayName = "PPL | Cán bộ | Ngừng hoạt động | bị chặn khi đang giữ phụ trách phân hiệu, tổ trưởng hoặc chủ nhiệm")]
    [InlineData("campus", "phụ trách phân hiệu")] [InlineData("department", "tổ trưởng")] [InlineData("homeroom", "chủ nhiệm")]
    public async Task Deactivate_BlockedByCurrentDuty(string kind, string label)
    {
        using var e = new PplEnv();
        var since = PplEnv.Today.AddDays(-10);
        if (kind == "campus") e.Db.CampusLeadership.Add(new CampusLeadership { CampusId = PplEnv.Campus1, StaffId = PplEnv.Teacher, StartDate = since });
        if (kind == "department") e.Db.DepartmentHead.Add(new DepartmentHead { DepartmentId = PplEnv.Dept1, StaffId = PplEnv.Teacher, StartDate = since });
        if (kind == "homeroom") e.Db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = PplEnv.Class6A, StaffId = PplEnv.Teacher, StartDate = since });
        e.Db.SaveChanges();
        await PplEnv.Rejects(() => e.Staff.SetActiveAsync(PplEnv.Teacher, false), label);
        Assert.True((await e.Db.StaffProfile.FindAsync(PplEnv.Teacher))!.IsActive);
    }

    [Theory(DisplayName = "PPL | Cán bộ | Ngừng hoạt động | trách nhiệm đã kết thúc từ hôm nay trở về trước không còn chặn")]
    [InlineData(0)] [InlineData(-1)] [InlineData(-30)]
    public async Task Deactivate_NotBlockedByEndedDuty(int endOffsetDays)
    {
        using var e = new PplEnv();
        e.Db.HomeroomAssignment.Add(new HomeroomAssignment { ClassId = PplEnv.Class6A, StaffId = PplEnv.Teacher, StartDate = PplEnv.Today.AddDays(-60), EndDate = PplEnv.Today.AddDays(endOffsetDays) });
        e.Db.SaveChanges();
        await e.Staff.SetActiveAsync(PplEnv.Teacher, false);
        Assert.False((await e.Db.StaffProfile.FindAsync(PplEnv.Teacher))!.IsActive);
    }

    [Fact(DisplayName = "PPL | Cán bộ | Ngừng hoạt động | bị chặn khi còn tiết Đã xếp lịch từ hôm nay, tiết quá khứ hoặc đã hủy không chặn")]
    public async Task Deactivate_BlockedByUpcomingSessions()
    {
        using var e = new PplEnv();
        TeachingSession S(int days, string status) => new() { TeacherId = PplEnv.Teacher, ClassId = PplEnv.Class6A, SubjectId = 1, PeriodSlotId = 1,
            SessionDate = PplEnv.Today.AddDays(days), Status = status, Source = "Timetable", CreatedAt = e.Clock.Now };
        e.Db.TeachingSession.AddRange(S(-3, "Scheduled"), S(2, "Cancelled"));
        e.Db.SaveChanges();
        await e.Staff.SetActiveAsync(PplEnv.Teacher, false);   // chỉ có tiết quá khứ và tiết đã hủy

        await e.Staff.SetActiveAsync(PplEnv.Teacher, true);
        e.Db.TeachingSession.Add(S(1, "Scheduled")); e.Db.SaveChanges();
        await PplEnv.Rejects(() => e.Staff.SetActiveAsync(PplEnv.Teacher, false), "tiết");
    }

    [Fact(DisplayName = "PPL | Cán bộ | Ngừng hoạt động | cán bộ không tồn tại trả 404")]
    public async Task Deactivate_Unknown_NotFound()
    {
        using var e = new PplEnv();
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Staff.SetActiveAsync(9999, false));
        Assert.Equal(404, ex.StatusCode);
    }
}