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
    public Task<List<StudentCandidateDto>> Candidates([FromQuery] int yearId, [FromQuery] string? search, [FromQuery] int? limit)
        => service.FindCandidatesAsync(yearId, search, limit ?? 30);

    [HttpPost("classes/{classId:int}/enrollments/bulk"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> EnrollMany(int classId, EnrollManyRequest r) { await service.EnrollManyAsync(classId, r); return NoContent(); }

    [HttpPost("enrollments/transfer-bulk"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> TransferMany(TransferManyRequest r) { await service.TransferManyAsync(r); return NoContent(); }

    [HttpPost("enrollments/close-bulk"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> CloseMany(CloseManyRequest r) { await service.CloseManyAsync(r); return NoContent(); }

    [HttpPost("classes/{classId:int}/enrollments"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> Enroll(int classId, EnrollRequest r) { await service.EnrollAsync(classId, r); return NoContent(); }

    [HttpPost("enrollments/{id:int}/transfer"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> Transfer(int id, TransferRequest r) { await service.TransferAsync(id, r); return NoContent(); }

    [HttpPost("classes/{classId:int}/graduate"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<int> Graduate(int classId, GraduateRequest r) => service.GraduateAsync(classId, r);

    [HttpPost("enrollments/{id:int}/close"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> Close(int id, CloseEnrollmentRequest r) { await service.CloseAsync(id, r); return NoContent(); }
}
