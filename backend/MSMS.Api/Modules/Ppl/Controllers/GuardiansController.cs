using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;

namespace MSMS.Api.Modules.Ppl.Controllers;

/// <summary>PPL-03: danh bạ phụ huynh và liên kết (4.3.22–4.3.26). Xem: Management; ghi: School Admin.</summary>
[ApiController, Route("api/ppl")]
public class GuardiansController(GuardianService guardians) : ControllerBase
{
    [HttpGet("guardians"), Authorize(Policy = Policies.CampusReader)]
    public Task<PagedResult<GuardianDto>> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => guardians.ListAsync(search, page, pageSize);

    [HttpGet("guardians/{id:int}/students"), Authorize(Policy = Policies.CampusReader)]
    public Task<List<GuardianChildDto>> Children(int id) => guardians.ChildrenAsync(id);

    [HttpPost("guardians"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<GuardianDto> Create(GuardianRequest r) => guardians.CreateAsync(r);

    [HttpPut("guardians/{id:int}"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<GuardianDto> Update(int id, GuardianRequest r) => guardians.UpdateAsync(id, r);

    [HttpPut("students/{studentId:int}/guardian"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> ChangeRepresentative(int studentId, ChangeGuardianRequest r)
    {
        await guardians.ChangeRepresentativeAsync(studentId, r);
        return NoContent();
    }
}