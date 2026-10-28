using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Scd.Controllers;

/// <summary>SCD-03: giáo viên vắng và tiết Chờ xử lý (BR-16). Chỉ Vice Principal phụ trách phân hiệu.</summary>
[ApiController, Route("api/scd/absences"), Authorize(Policy = Policies.TimetableManager)]
public class AbsenceController(TeacherAbsenceService service) : ControllerBase
{
    [HttpGet]
    public Task<List<AbsenceDto>> List([FromQuery] int campusId) => service.ListAsync(campusId);

    [HttpGet("teachers")]
    public Task<List<TeacherOptionDto>> Teachers([FromQuery] int campusId) => service.TeachersAsync(campusId);

    [HttpPost]
    public Task<DeclareAbsenceResultDto> Declare(DeclareAbsenceRequest request) => service.DeclareAsync(request);

    [HttpDelete("{id:int}")]
    public Task<DeleteAbsenceResultDto> Delete(int id) => service.DeleteAsync(id);

    [HttpGet("pending")]
    public Task<List<PendingSessionDto>> Pending([FromQuery] int campusId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? classId) =>
        service.ListPendingAsync(campusId, from, to, classId);

    [HttpGet("pending/{id:long}/suggestions")]
    public Task<List<TeacherAvailabilityDto>> Suggestions(long id) => service.SuggestionsAsync(id);

    [HttpPost("pending/{id:long}/resolve")]
    public Task<AdjustResultDto> Resolve(long id, ResolvePendingRequest request) => service.ResolveAsync(id, request);
}
