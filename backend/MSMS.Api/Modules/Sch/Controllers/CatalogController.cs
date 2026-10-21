using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Sch.Controllers;

[ApiController, Route("api/sch")]
public class CatalogController(CatalogService service) : ControllerBase
{
    [HttpGet("departments"), Authorize(Policy = Policies.Staff)]
    public Task<List<DepartmentDto>> ListDepartments() => service.ListDepartmentsAsync();

    [HttpPost("departments"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<DepartmentDto> CreateDepartment(DepartmentRequest r) => service.CreateDepartmentAsync(r);

    [HttpPut("departments/{id:int}"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> UpdateDepartment(int id, DepartmentRequest r) { await service.UpdateDepartmentAsync(id, r); return NoContent(); }

    [HttpPut("departments/{id:int}/active"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> SetDepartmentActive(int id, ActiveRequest r) { await service.SetDepartmentActiveAsync(id, r.IsActive); return NoContent(); }

    [HttpGet("subjects"), Authorize(Policy = Policies.Staff)]
    public Task<List<SubjectDto>> ListSubjects([FromQuery] int? departmentId) => service.ListSubjectsAsync(departmentId);

    [HttpPost("subjects"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<SubjectDto> CreateSubject(SubjectRequest r) => service.CreateSubjectAsync(r);

    [HttpPut("subjects/{id:int}"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> UpdateSubject(int id, SubjectRequest r) { await service.UpdateSubjectAsync(id, r); return NoContent(); }

    [HttpPut("subjects/{id:int}/active"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> SetSubjectActive(int id, ActiveRequest r) { await service.SetSubjectActiveAsync(id, r.IsActive); return NoContent(); }
}
