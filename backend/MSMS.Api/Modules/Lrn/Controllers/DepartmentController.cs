using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Lrn.Dtos;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Api.Modules.Lrn.Controllers;

[ApiController, Route("api/lrn/department"), Authorize(Policy = Policies.Teacher)]
public class DepartmentController(DepartmentService service) : ControllerBase
{
    [HttpGet("assignments")]
    public Task<DepartmentOverviewDto> Overview([FromQuery] int? subjectId) => service.OverviewAsync(subjectId);
}
