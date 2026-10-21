using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;
using MSMS.Api.Modules.Sch.Dtos;

namespace MSMS.Api.Modules.Ppl.Controllers;

[ApiController, Route("api/ppl/staff")]
public class StaffController(StaffService staff, StaffImportService import) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.CampusReader)]
    public Task<PagedResult<StaffDto>> List([FromQuery] int? campusId, [FromQuery] int? departmentId, [FromQuery] string? accountType,
        [FromQuery] bool? hasAbbr, [FromQuery] string? search, [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => staff.ListAsync(campusId, departmentId, accountType, hasAbbr, search, includeInactive, page, pageSize);

    [HttpPost, Authorize(Policy = Policies.SchoolAdmin)]
    public Task<StaffDto> Create(StaffRequest r) => staff.CreateAsync(r);

    [HttpPut("{id:int}"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<StaffDto> Update(int id, StaffRequest r) => staff.UpdateAsync(id, r);

    [HttpPut("{id:int}/active"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> SetActive(int id, ActiveRequest r) { await staff.SetActiveAsync(id, r.IsActive); return NoContent(); }

    [HttpGet("import-template"), Authorize(Policy = Policies.SchoolAdmin)]
    public IActionResult Template() => import.BuildTemplate();

    [HttpPost("import"), Authorize(Policy = Policies.SchoolAdmin)]
    [RequestSizeLimit(10_000_000)]
    public async Task<StaffImportResult> Import(IFormFile file)
    {
        if (file is null || file.Length == 0) throw ApiException.BadRequest("Chưa chọn file. Chọn file .xlsx rồi tải lên.");
        await using var stream = file.OpenReadStream();
        return await import.ImportAsync(stream);
    }
}
