using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Sch.Controllers;

[ApiController, Route("api/sch/period-slots")]
public class PeriodSlotsController(PeriodSlotService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.Staff)]
    public Task<List<PeriodSlotDto>> List() => service.ListAsync();

    [HttpPost, Authorize(Policy = Policies.SchoolAdmin)]
    public Task<PeriodSlotDto> Create(PeriodSlotRequest r) => service.CreateAsync(r);

    [HttpPut("{id:int}"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> UpdateTimes(int id, PeriodSlotTimesRequest r) { await service.UpdateTimesAsync(id, r); return NoContent(); }

    [HttpPut("{id:int}/active"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> SetActive(int id, ActiveRequest r) { await service.SetActiveAsync(id, r.IsActive); return NoContent(); }
}
