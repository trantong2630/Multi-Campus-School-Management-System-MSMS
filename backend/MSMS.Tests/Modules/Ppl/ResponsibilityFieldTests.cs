using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Kiểm tra từng trường và từng chức năng của trách nhiệm: gán, thay người, kết thúc, danh sách, lịch sử, xuất Excel (PPL-04, 4.3.27-4.3.35).</summary>
public class ResponsibilityFieldTests
{
    private static DateOnly T => PplEnv.Today;
    private static ResponsibilityAssignRequest Assign(string kind, int target, int staff, DateOnly? start = null) => new(kind, target, staff, start ?? T);

    // ---------- Loại trách nhiệm ----------
    [Fact(DisplayName = "PPL | Trách nhiệm | Loại | loại không hợp lệ bị từ chối khi gán")]
    public async Task Kind_Invalid_Assign()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("principal", 1, PplEnv.Teacher)), "Loại trách nhiệm");
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Loại | loại không hợp lệ bị từ chối khi xem danh sách, lịch sử, kết thúc và xuất Excel")]
    public async Task Kind_Invalid_Everywhere()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Resp.ListCurrentAsync("x", null), "Loại trách nhiệm");
        await PplEnv.Rejects(() => e.Resp.HistoryAsync("x", 1), "Loại trách nhiệm");
        await PplEnv.Rejects(() => e.Resp.EndAsync(new ResponsibilityEndRequest("x", 1, T)), "Loại trách nhiệm");
        await PplEnv.Rejects(() => e.Resp.ExportAsync("x", null), "Loại trách nhiệm");
    }

    // ---------- Đích ----------
    [Theory(DisplayName = "PPL | Trách nhiệm | Đích phân hiệu | không tồn tại hoặc đã ngừng hoạt động bị từ chối")]
    [InlineData(999)] [InlineData(PplEnv.CampusInactive)] [InlineData(0)]
    public async Task Target_Campus_Invalid(int campusId)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("campus", campusId, PplEnv.ViceP)), "Phân hiệu");
    }

    [Theory(DisplayName = "PPL | Trách nhiệm | Đích tổ chuyên môn | không tồn tại hoặc đã ngừng hoạt động bị từ chối")]
    [InlineData(999)] [InlineData(PplEnv.DeptInactive)]
    public async Task Target_Department_Invalid(int deptId)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("department", deptId, PplEnv.Teacher)), "Tổ chuyên môn");
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Đích lớp | không tồn tại hoặc đã ngừng hoạt động bị từ chối")]
    public async Task Target_Class_Invalid()
    {
        using var e = new PplEnv();
        e.Db.Class.Add(new Class { ClassId = 50, CampusId = PplEnv.Campus1, AcademicYearId = PplEnv.Year1, Name = "Cũ", GradeLevel = 7, IsActive = false, CreatedAt = e.Clock.Now });
        e.Db.SaveChanges();
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("homeroom", 999, PplEnv.Teacher)), "Lớp");
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("homeroom", 50, PplEnv.Teacher)), "Lớp");
    }

    // ---------- Người được gán ----------
    [Fact(DisplayName = "PPL | Trách nhiệm | Người được gán | không chọn hoặc không tồn tại bị từ chối")]
    public async Task Staff_Unknown()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("homeroom", PplEnv.Class6A, 999)), "Chọn cán bộ");
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("homeroom", PplEnv.Class6A, 0)), "Chọn cán bộ");
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Người được gán | cán bộ đã ngừng hoạt động bị từ chối")]
    public async Task Staff_Inactive()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("homeroom", PplEnv.Class6A, PplEnv.InactiveStaff)), "ngừng hoạt động");
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Người được gán | cán bộ chưa có tài khoản hoặc tài khoản bị khóa bị từ chối")]
    public async Task Staff_NoAccount_Or_Locked()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("homeroom", PplEnv.Class6A, PplEnv.NoAccountStaff)), "chưa có tài khoản");
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("homeroom", PplEnv.Class6A, PplEnv.LockedTeacher)), "bị khóa");
    }

    [Theory(DisplayName = "PPL | Trách nhiệm | Người được gán | phụ trách phân hiệu chỉ gán cho Hiệu phó")]
    [InlineData(PplEnv.Teacher)] [InlineData(PplEnv.PrincipalId)] [InlineData(PplEnv.Admin)]
    public async Task Campus_OnlyVicePrincipal(int staffId)
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("campus", PplEnv.Campus1, staffId)), "Hiệu phó");
    }

    [Theory(DisplayName = "PPL | Trách nhiệm | Người được gán | tổ trưởng và chủ nhiệm chỉ gán cho Giáo viên")]
    [InlineData("homeroom", PplEnv.ViceP)] [InlineData("homeroom", PplEnv.PrincipalId)] [InlineData("department", PplEnv.ViceP)] [InlineData("department", PplEnv.Admin)]
    public async Task HomeroomAndDepartment_OnlyTeacher(string kind, int staffId)
    {
        using var e = new PplEnv();
        var target = kind == "homeroom" ? PplEnv.Class6A : PplEnv.Dept1;
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign(kind, target, staffId)), "Giáo viên");
    }

    // ---------- Gán và thay người ----------
    [Theory(DisplayName = "PPL | Trách nhiệm | Gán | gán thành công cho từng loại, người được gán hiện là người đang đảm nhiệm")]
    [InlineData("campus", PplEnv.Campus1, PplEnv.ViceP)] [InlineData("department", PplEnv.Dept1, PplEnv.Teacher)] [InlineData("homeroom", PplEnv.Class6A, PplEnv.Teacher)]
    public async Task Assign_Succeeds(string kind, int target, int staff)
    {
        using var e = new PplEnv();
        await e.Resp.AssignAsync(Assign(kind, target, staff));
        var row = (await e.Resp.ListCurrentAsync(kind, null)).Single(r => r.TargetId == target);
        Assert.Equal(staff, row.StaffId);
        Assert.Equal(T, row.StartDate);
        Assert.Equal(PplEnv.Admin, await AssignedBy(e, kind));
    }

    private static async Task<int?> AssignedBy(PplEnv e, string kind) => kind switch
    {
        "campus" => (await e.Db.CampusLeadership.SingleAsync()).AssignedByPersonId,
        "department" => (await e.Db.DepartmentHead.SingleAsync()).AssignedByPersonId,
        _ => (await e.Db.HomeroomAssignment.SingleAsync()).AssignedByPersonId,
    };

    [Fact(DisplayName = "PPL | Trách nhiệm | Thay người | bản cũ kết thúc hôm nay (ngày người mới nhận), bản cũ giữ làm lịch sử")]
    public async Task Replace_ClosesOldToday()
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(-10));
        await e.Resp.AssignAsync(Assign("homeroom", PplEnv.Class6A, PplEnv.OtherTeacher, T));
        var old = await e.Db.HomeroomAssignment.SingleAsync(x => x.StaffId == PplEnv.Teacher);
        Assert.Equal(T, old.EndDate);
        Assert.Equal(PplEnv.OtherTeacher, (await e.Resp.ListCurrentAsync("homeroom", null)).Single(r => r.TargetId == PplEnv.Class6A).StaffId);
        Assert.Equal(2, (await e.Resp.HistoryAsync("homeroom", PplEnv.Class6A)).Count);
    }

    [Theory(DisplayName = "PPL | Trách nhiệm | Thay người | chỉ có hiệu lực ngay hôm nay, hẹn ngày sau bị từ chối (tránh khoảng trống một ngày)")]
    [InlineData(1)] [InlineData(30)]
    public async Task Replace_FutureDate_Rejected(int offsetDays)
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(-60));
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("homeroom", PplEnv.Class6A, PplEnv.OtherTeacher, T.AddDays(offsetDays))), "hiệu lực ngay hôm nay");
        Assert.Null((await e.Db.HomeroomAssignment.SingleAsync()).EndDate);
    }

    [Theory(DisplayName = "PPL | Trách nhiệm | Gán lần đầu | đích đang trống thì được hẹn ngày sau")]
    [InlineData(1)] [InlineData(30)]
    public async Task Assign_EmptyTarget_FutureDate_Accepted(int offsetDays)
    {
        using var e = new PplEnv();
        await e.Resp.AssignAsync(Assign("homeroom", PplEnv.Class6A, PplEnv.OtherTeacher, T.AddDays(offsetDays)));
        Assert.Single(await e.Db.HomeroomAssignment.ToListAsync());
    }

    [Theory(DisplayName = "PPL | Trách nhiệm | Ngày hiệu lực | ngày trong quá khứ bị từ chối khi gán hoặc thay người, hôm nay và ngày sau được chấp nhận")]
    [InlineData(-1, false)] [InlineData(-30, false)] [InlineData(0, true)]
    public async Task StartDate_NotBeforeToday(int offsetDays, bool accepted)
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(-60));      // người đang giữ từ lâu: thay người được
        var request = Assign("homeroom", PplEnv.Class6A, PplEnv.OtherTeacher, T.AddDays(offsetDays));
        if (accepted) await e.Resp.AssignAsync(request);
        else
        {
            await PplEnv.Rejects(() => e.Resp.AssignAsync(request), "trước hôm nay");
            Assert.Null((await e.Db.HomeroomAssignment.SingleAsync()).EndDate);   // không đổi gì
        }
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Ngày hiệu lực | gán mới cho đích chưa có người cũng không cho chọn ngày quá khứ")]
    public async Task StartDate_Past_Rejected_ForNewAssignment()
    {
        using var e = new PplEnv();
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("campus", PplEnv.Campus1, PplEnv.ViceP, T.AddDays(-1))), "trước hôm nay");
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("department", PplEnv.Dept1, PplEnv.Teacher, T.AddDays(-1))), "trước hôm nay");
        Assert.Empty(e.Db.CampusLeadership); Assert.Empty(e.Db.DepartmentHead);
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Gán | chọn lại đúng người đang đảm nhiệm bị từ chối")]
    public async Task Assign_SameHolder_Rejected()
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(-5));
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("homeroom", PplEnv.Class6A, PplEnv.Teacher, T)), "đang đảm nhiệm sẵn");
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Gán | ngày hiệu lực sớm hơn một người đã được hẹn cho đích đó bị từ chối")]
    public async Task Assign_BeforeFutureRecord_Rejected()
    {
        using var e = new PplEnv();
        await e.Resp.AssignAsync(Assign("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(5)));
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("homeroom", PplEnv.Class6A, PplEnv.OtherTeacher, T)), "bắt đầu sau ngày đã chọn");
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Tổ trưởng | một giáo viên đang làm tổ trưởng một tổ thì không gán thêm tổ khác được")]
    public async Task Department_SameTeacherTwoDepartments_Rejected()
    {
        using var e = new PplEnv();
        e.Db.SubjectDepartment.Add(new SubjectDepartment { DepartmentId = 3, Name = "Tổ Văn", IsActive = true });
        e.Db.SaveChanges();
        await e.Resp.AssignAsync(Assign("department", PplEnv.Dept1, PplEnv.Teacher));
        await PplEnv.Rejects(() => e.Resp.AssignAsync(Assign("department", 3, PplEnv.Teacher)), "chỉ làm tổ trưởng một tổ");
        Assert.Equal(1, await e.Db.DepartmentHead.CountAsync(d => d.StaffId == PplEnv.Teacher));
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Tổ trưởng | thay tổ trưởng của tổ: bản cũ kết thúc hôm nay")]
    public async Task Department_Replace()
    {
        using var e = new PplEnv();
        e.Seed("department", PplEnv.Dept1, PplEnv.Teacher, T.AddDays(-20));
        await e.Resp.AssignAsync(Assign("department", PplEnv.Dept1, PplEnv.OtherTeacher, T));
        Assert.Equal(T, (await e.Db.DepartmentHead.SingleAsync(d => d.StaffId == PplEnv.Teacher)).EndDate);
    }

    // ---------- Kết thúc ----------
    [Fact(DisplayName = "PPL | Trách nhiệm | Kết thúc | bản ghi không tồn tại trả 404")]
    public async Task End_Unknown_NotFound()
    {
        using var e = new PplEnv();
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Resp.EndAsync(new ResponsibilityEndRequest("homeroom", 9999, T)));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Kết thúc | ngày kết thúc trước ngày bắt đầu bị từ chối")]
    public async Task End_BeforeStart_Rejected()
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(5));   // đã hẹn bắt đầu 5 ngày nữa
        var id = (await e.Db.HomeroomAssignment.SingleAsync()).Id;
        await PplEnv.Rejects(() => e.Resp.EndAsync(new ResponsibilityEndRequest("homeroom", id, T.AddDays(3))), "trước ngày bắt đầu");
    }

    [Theory(DisplayName = "PPL | Trách nhiệm | Kết thúc | ngày kết thúc trước hôm nay bị từ chối (không sửa lùi lịch sử), hôm nay và ngày sau được chấp nhận")]
    [InlineData(-1, false)] [InlineData(-30, false)] [InlineData(0, true)] [InlineData(7, true)]
    public async Task End_Date_NotBeforeToday(int offsetDays, bool accepted)
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(-60));
        var id = (await e.Db.HomeroomAssignment.SingleAsync()).Id;
        var request = new ResponsibilityEndRequest("homeroom", id, T.AddDays(offsetDays));
        if (accepted) await e.Resp.EndAsync(request);
        else
        {
            await PplEnv.Rejects(() => e.Resp.EndAsync(request), "trước hôm nay");
            Assert.Null((await e.Db.HomeroomAssignment.SingleAsync()).EndDate);
        }
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Kết thúc | trách nhiệm đã kết thúc từ trước bị từ chối")]
    public async Task End_AlreadyEnded_Rejected()
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(-10), T.AddDays(-2));   // đã kết thúc từ trước
        var id = (await e.Db.HomeroomAssignment.SingleAsync()).Id;
        await PplEnv.Rejects(() => e.Resp.EndAsync(new ResponsibilityEndRequest("homeroom", id, T)), "đã kết thúc");
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Kết thúc | kết thúc hôm nay: đích không còn người đảm nhiệm ngay, danh sách hiện người vừa kết thúc")]
    public async Task End_Today_ReleasesTargetImmediately()
    {
        using var e = new PplEnv();
        e.Seed("department", PplEnv.Dept1, PplEnv.Teacher, T.AddDays(-10));
        var id = (await e.Db.DepartmentHead.SingleAsync()).Id;
        await e.Resp.EndAsync(new ResponsibilityEndRequest("department", id, T));
        var row = (await e.Resp.ListCurrentAsync("department", null)).Single(r => r.TargetId == PplEnv.Dept1);
        Assert.Null(row.StaffId);
        Assert.Equal("Giáo viên A", row.EndedStaffName);
        Assert.Equal(T, row.EndedDate);
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Kết thúc rồi gán lại | kết thúc hôm nay rồi gán người mới từ hôm nay được: người cũ vẫn kết thúc hôm nay, người mới đang đảm nhiệm")]
    public async Task End_Then_AssignSameDay()
    {
        using var e = new PplEnv();
        e.Seed("department", PplEnv.Dept1, PplEnv.Teacher, T.AddDays(-10));
        var id = (await e.Db.DepartmentHead.SingleAsync()).Id;
        await e.Resp.EndAsync(new ResponsibilityEndRequest("department", id, T));
        await e.Resp.AssignAsync(Assign("department", PplEnv.Dept1, PplEnv.OtherTeacher, T));
        Assert.Equal(T, (await e.Db.DepartmentHead.SingleAsync(d => d.StaffId == PplEnv.Teacher)).EndDate);
        Assert.Equal(PplEnv.OtherTeacher, (await e.Resp.ListCurrentAsync("department", null)).Single(r => r.TargetId == PplEnv.Dept1).StaffId);
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Kết thúc | ngày kết thúc trong tương lai được chấp nhận, người đó vẫn đang đảm nhiệm hôm nay")]
    public async Task End_Future_StillCurrentToday()
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(-10));
        var id = (await e.Db.HomeroomAssignment.SingleAsync()).Id;
        await e.Resp.EndAsync(new ResponsibilityEndRequest("homeroom", id, T.AddDays(7)));
        var row = (await e.Resp.ListCurrentAsync("homeroom", null)).Single(r => r.TargetId == PplEnv.Class6A);
        Assert.Equal(PplEnv.Teacher, row.StaffId);
        Assert.Equal(T.AddDays(7), row.EndDate);
    }

    // ---------- Danh sách, lịch sử, xuất Excel ----------
    [Fact(DisplayName = "PPL | Trách nhiệm | Danh sách | phân hiệu và tổ ngừng hoạt động không hiện; đích chưa có người hiện là 'chưa có'")]
    public async Task List_OnlyActiveTargets_AndUnassigned()
    {
        using var e = new PplEnv();
        var campuses = await e.Resp.ListCurrentAsync("campus", null);
        Assert.Single(campuses);
        Assert.Null(campuses[0].StaffId);
        var depts = await e.Resp.ListCurrentAsync("department", null);
        Assert.DoesNotContain(depts, d => d.TargetId == PplEnv.DeptInactive);
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Danh sách | chủ nhiệm theo năm học được chọn, năm không có lớp thì rỗng")]
    public async Task List_Homeroom_ByYear()
    {
        using var e = new PplEnv();
        Assert.Equal(2, (await e.Resp.ListCurrentAsync("homeroom", null)).Count);
        Assert.Equal(2, (await e.Resp.ListCurrentAsync("homeroom", PplEnv.Year1)).Count);
        Assert.Empty(await e.Resp.ListCurrentAsync("homeroom", 999));
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Danh sách | người đã được hẹn hiện ở dòng 'sắp nhận' kèm mã người đó")]
    public async Task List_UpcomingHolder()
    {
        using var e = new PplEnv();
        await e.Resp.AssignAsync(Assign("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(3)));
        var row = (await e.Resp.ListCurrentAsync("homeroom", null)).Single(r => r.TargetId == PplEnv.Class6A);
        Assert.Null(row.StaffId);
        Assert.Equal(PplEnv.Teacher, row.NextStaffId);
        Assert.Equal(T.AddDays(3), row.NextStartDate);
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Danh sách | Hiệu phó chỉ thấy phân hiệu mình phụ trách và lớp thuộc phân hiệu đó")]
    public async Task List_ViceScope()
    {
        using var led = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP);
        led.Db.CampusLeadership.Add(new CampusLeadership { CampusId = PplEnv.Campus1, StaffId = PplEnv.ViceP, StartDate = T.AddDays(-5) });
        led.Db.SaveChanges();
        Assert.Single(await led.Resp.ListCurrentAsync("campus", null));
        Assert.Equal(2, (await led.Resp.ListCurrentAsync("homeroom", null)).Count);

        using var none = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP);
        Assert.Empty(await none.Resp.ListCurrentAsync("campus", null));
        Assert.Empty(await none.Resp.ListCurrentAsync("homeroom", null));
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Lịch sử | liệt kê mọi người từng giữ, mới nhất trước, đánh dấu người đang hiệu lực")]
    public async Task History_OrderedAndFlagged()
    {
        using var e = new PplEnv();
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, T.AddDays(-20), T.AddDays(-4));
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.OtherTeacher, T.AddDays(-3));
        var h = await e.Resp.HistoryAsync("homeroom", PplEnv.Class6A);
        Assert.Equal([PplEnv.OtherTeacher, PplEnv.Teacher], h.Select(x => x.StaffId).ToArray());
        Assert.True(h[0].IsEffective);
        Assert.False(h[1].IsEffective);
    }

    [Fact(DisplayName = "PPL | Trách nhiệm | Lịch sử | Hiệu phó không xem được lịch sử ngoài phân hiệu mình phụ trách")]
    public async Task History_ViceOutsideScope_Forbidden()
    {
        using var e = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP);
        e.Db.CampusLeadership.Add(new CampusLeadership { CampusId = PplEnv.Campus1, StaffId = PplEnv.ViceP, StartDate = T.AddDays(-5) });
        e.Db.SaveChanges();
        Assert.Single(await e.Resp.HistoryAsync("campus", PplEnv.Campus1));   // phân hiệu mình phụ trách: xem được
        var ex = await Assert.ThrowsAsync<ApiException>(() => e.Resp.HistoryAsync("campus", 999));
        Assert.Equal(403, ex.StatusCode);
    }

    [Theory(DisplayName = "PPL | Trách nhiệm | Xuất Excel | tạo được file .xlsx cho từng loại")]
    [InlineData("campus")] [InlineData("department")] [InlineData("homeroom")]
    public async Task Export_ReturnsXlsx(string kind)
    {
        using var e = new PplEnv();
        var file = await e.Resp.ExportAsync(kind, null);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.True(file.FileContents.Length > 1000);
    }
}