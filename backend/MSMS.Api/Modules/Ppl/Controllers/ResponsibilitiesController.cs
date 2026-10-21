using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;

namespace MSMS.Api.Modules.Ppl.Controllers;

[ApiController, Route("api/ppl/responsibilities")]
public class ResponsibilitiesController(ResponsibilityService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.CampusReader)]
    public Task<List<ResponsibilityRow>> List([FromQuery] string kind, [FromQuery] int? yearId) => service.ListCurrentAsync(kind, yearId);

    [HttpGet("history"), Authorize(Policy = Policies.CampusReader)]
    public Task<List<ResponsibilityHistoryRow>> History([FromQuery] string kind, [FromQuery] int targetId) => service.HistoryAsync(kind, targetId);

    // Principal và School Admin gán trách nhiệm (spec 2.2).
    [HttpPost("assign"), Authorize(Policy = Policies.ResponsibilityManager)]
    public async Task<IActionResult> Assign(ResponsibilityAssignRequest r) { await service.AssignAsync(r); return NoContent(); }

    [HttpPost("end"), Authorize(Policy = Policies.ResponsibilityManager)]
    public async Task<IActionResult> End(ResponsibilityEndRequest r) { await service.EndAsync(r); return NoContent(); }
}
