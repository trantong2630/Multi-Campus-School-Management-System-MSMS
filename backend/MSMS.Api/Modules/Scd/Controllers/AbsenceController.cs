using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Scd.Controllers;

/// <summary>SCD-03: giáo viên vắng và tiết Chờ xử lý (BR-16). Hai danh sách đọc được cho Principal (toàn trường) và Vice Principal (phân hiệu mình); mọi thao tác ghi chỉ Vice Principal phụ trách phân hiệu.</summary>
[ApiController, Route("api/scd/absences")]
public class AbsenceController(TeacherAbsenceService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.TimetableReader)]
    public Task<List<AbsenceDto>> List([FromQuery] int campusId) => service.ListAsync(campusId);

    [HttpGet("teachers"), Authorize(Policy = Policies.TimetableManager)]
    public Task<List<TeacherOptionDto>> Teachers([FromQuery] int campusId) => service.TeachersAsync(campusId);

    [HttpPost, Authorize(Policy = Policies.TimetableManager)]
    public Task<DeclareAbsenceResultDto> Declare(DeclareAbsenceRequest request) => service.DeclareAsync(request);

    [HttpDelete("{id:int}"), Authorize(Policy = Policies.TimetableManager)]
    public Task<DeleteAbsenceResultDto> Delete(int id) => service.DeleteAsync(id);

    [HttpGet("pending"), Authorize(Policy = Policies.TimetableReader)]
    public Task<List<PendingSessionDto>> Pending([FromQuery] int campusId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? classId) =>
        service.ListPendingAsync(campusId, from, to, classId);

    [HttpGet("pending/{id:long}/suggestions"), Authorize(Policy = Policies.TimetableManager)]
    public Task<List<TeacherAvailabilityDto>> Suggestions(long id) => service.SuggestionsAsync(id);

    [HttpPost("pending/{id:long}/resolve"), Authorize(Policy = Policies.TimetableManager)]
    public Task<AdjustResultDto> Resolve(long id, ResolvePendingRequest request) => service.ResolveAsync(id, request);
}
