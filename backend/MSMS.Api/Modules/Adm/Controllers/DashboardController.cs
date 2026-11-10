using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Adm.Dtos;
using MSMS.Api.Modules.Adm.Services;

namespace MSMS.Api.Modules.Adm.Controllers;

/// <summary>Dashboard cấp quản lý: Principal, Vice Principal.</summary>
[ApiController, Route("api/adm/dashboard"), Authorize(Policy = Policies.DashboardReader)]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get([FromQuery] int? campusId, [FromQuery] int? gradeLevel) => dashboard.GetAsync(campusId, gradeLevel);
}

/// <summary>Dashboard của School Admin: dữ liệu tổ chức, tài khoản, hoạt động hệ thống.</summary>
[ApiController, Route("api/adm/dashboard/admin"), Authorize(Policy = Policies.SchoolAdmin)]
public class AdminDashboardController(AdminDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public Task<AdminDashboardDto> Get() => dashboard.GetAsync();
}

/// <summary>Tình hình tổ chuyên môn của tổ trưởng: tải dạy và điểm danh các tiết thuộc môn của tổ. Policy chỉ chặn loại tài khoản; trách nhiệm tổ trưởng do service kiểm.</summary>
[ApiController, Route("api/adm/dashboard/department"), Authorize(Policy = Policies.Teacher)]
public class DepartmentBoardController(DepartmentBoardService board) : ControllerBase
{
    [HttpGet]
    public Task<DepartmentBoardDto> Get() => board.GetAsync();
}

/// <summary>Tiến độ bài tập rèn luyện theo phạm vi: Hiệu trưởng toàn trường, Hiệu phó phân hiệu phụ trách, chủ nhiệm lớp mình. Policy chỉ chặn loại tài khoản; phạm vi do service kiểm.</summary>
[ApiController, Route("api/adm/progress"), Authorize(Policy = Policies.AssignmentOversight)]
public class ProgressController(ProgressService progress) : ControllerBase
{
    [HttpGet]
    public Task<ProgressDto> Get([FromQuery] int? campusId) => progress.GetAsync(campusId);
}

/// <summary>Theo dõi lớp chủ nhiệm. Policy chỉ chặn loại tài khoản; trách nhiệm chủ nhiệm do service kiểm.</summary>
[ApiController, Route("api/adm/homeroom"), Authorize(Policy = Policies.Teacher)]
public class HomeroomOverviewController(HomeroomOverviewService homeroom) : ControllerBase
{
    [HttpGet]
    public Task<HomeroomOverviewDto> Get([FromQuery] int? classId) => homeroom.GetAsync(classId);
}
