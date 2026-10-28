using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Sch.Controllers;

[ApiController, Route("api/sch")]
public class EnrollmentsController(EnrollmentService service) : ControllerBase
{
    [HttpGet("classes/{classId:int}/roster"), Authorize(Policy = Policies.Staff)]
    public Task<RosterDto> Roster(int classId, [FromQuery] DateOnly? date) => service.GetRosterAsync(classId, date);

    [HttpGet("enrollments/candidates"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<List<StudentCandidateDto>> Candidates([FromQuery] int yearId, [FromQuery] string? search)
        => service.FindCandidatesAsync(yearId, search);

    [HttpPost("classes/{classId:int}/enrollments"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> Enroll(int classId, EnrollRequest r) { await service.EnrollAsync(classId, r); return NoContent(); }

    [HttpPost("enrollments/{id:int}/transfer"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> Transfer(int id, TransferRequest r) { await service.TransferAsync(id, r); return NoContent(); }

    [HttpPost("enrollments/{id:int}/close"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> Close(int id, CloseEnrollmentRequest r) { await service.CloseAsync(id, r); return NoContent(); }
}
