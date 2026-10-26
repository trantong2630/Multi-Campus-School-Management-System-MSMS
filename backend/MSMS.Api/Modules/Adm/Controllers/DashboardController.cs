using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Adm.Dtos;
using MSMS.Api.Modules.Adm.Services;

namespace MSMS.Api.Modules.Adm.Controllers;
// check 
[ApiController, Route("api/adm/dashboard"), Authorize(Policy = Policies.DashboardReader)]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get([FromQuery] int? campusId) => dashboard.GetAsync(campusId);
}
