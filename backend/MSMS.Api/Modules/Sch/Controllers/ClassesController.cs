using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Sch.Controllers;

[ApiController, Route("api/sch/classes")]
public class ClassesController(ClassService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.Staff)]
    public Task<List<ClassDto>> List([FromQuery] int? campusId, [FromQuery] int? yearId, [FromQuery] bool includeInactive = false)
        => service.ListAsync(campusId, yearId, includeInactive);

    [HttpPost, Authorize(Policy = Policies.SchoolAdmin)]
    public Task<ClassDto> Create(ClassRequest r) => service.CreateAsync(r);

    [HttpPut("{id:int}"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> Update(int id, ClassUpdateRequest r) { await service.UpdateAsync(id, r); return NoContent(); }

    [HttpPut("{id:int}/active"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> SetActive(int id, ActiveRequest r) { await service.SetActiveAsync(id, r.IsActive); return NoContent(); }
}
