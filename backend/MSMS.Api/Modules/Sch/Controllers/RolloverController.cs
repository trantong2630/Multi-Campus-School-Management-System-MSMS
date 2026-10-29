using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Sch.Controllers;

[ApiController, Route("api/sch/rollover")]
public class RolloverController(RolloverService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.SchoolAdmin)]
    public Task<List<RolloverRowDto>> Overview([FromQuery] int fromYearId, [FromQuery] int toYearId, [FromQuery] int? campusId)
        => service.OverviewAsync(fromYearId, toYearId, campusId);

    [HttpPost("classes"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<RolloverCreateResult> CreateClasses(RolloverCreateRequest r) => service.CreateClassesAsync(r);

    [HttpPost("move"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<RolloverMoveResult> Move(RolloverMoveRequest r) => service.MoveStudentsAsync(r);
}
