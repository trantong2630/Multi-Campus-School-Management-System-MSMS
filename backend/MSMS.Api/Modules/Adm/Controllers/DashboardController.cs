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
    public Task<DashboardDto> Get([FromQuery] int? campusId) => dashboard.GetAsync(campusId);
}

/// <summary>Dashboard của School Admin: dữ liệu tổ chức, tài khoản, hoạt động hệ thống.</summary>
[ApiController, Route("api/adm/dashboard/admin"), Authorize(Policy = Policies.SchoolAdmin)]
public class AdminDashboardController(AdminDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public Task<AdminDashboardDto> Get() => dashboard.GetAsync();
}

/// <summary>Dashboard giáo viên: tiết, lớp, bài tập của chính mình.</summary>
[ApiController, Route("api/adm/dashboard/teacher"), Authorize(Policy = Policies.Teacher)]
public class TeacherDashboardController(TeacherDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public Task<TeacherDashboardDto> Get() => dashboard.GetAsync();
}

/// <summary>Theo dõi lớp chủ nhiệm. Policy chỉ chặn loại tài khoản; trách nhiệm chủ nhiệm do service kiểm.</summary>
[ApiController, Route("api/adm/homeroom"), Authorize(Policy = Policies.Teacher)]
public class HomeroomOverviewController(HomeroomOverviewService homeroom) : ControllerBase
{
    [HttpGet]
    public Task<HomeroomOverviewDto> Get([FromQuery] int? classId) => homeroom.GetAsync(classId);
}
