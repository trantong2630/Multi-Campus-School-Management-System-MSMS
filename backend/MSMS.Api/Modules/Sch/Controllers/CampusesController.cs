using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Sch.Controllers;

[ApiController, Route("api/sch/campuses")]
public class CampusesController(CampusService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.CampusReader)]   // SCH-01: School Admin, Hiệu trưởng, Hiệu phó
    public Task<List<CampusDto>> List() => service.ListAsync();

    [HttpPost, Authorize(Policy = Policies.SchoolAdmin)]
    public Task<CampusDto> Create(CampusRequest r) => service.CreateAsync(r);

    [HttpPut("{id:int}"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> Update(int id, CampusRequest r) { await service.UpdateAsync(id, r); return NoContent(); }

    [HttpPut("{id:int}/active"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> SetActive(int id, ActiveRequest r) { await service.SetActiveAsync(id, r.IsActive); return NoContent(); }
}
