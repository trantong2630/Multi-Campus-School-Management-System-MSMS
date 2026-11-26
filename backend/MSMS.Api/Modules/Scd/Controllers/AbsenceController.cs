using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Scd.Controllers;

/// <summary>
/// SCD-03: giáo viên vắng và tiết Chờ xử lý (BR-16). Hai danh sách đọc được cho Principal (toàn trường) và Vice Principal (phân hiệu mình).
/// Nhánh `department` dành cho tổ trưởng: khai vắng cho giáo viên của tổ và quyết định từng tiết bằng đề xuất (dạy thay hoặc hủy); service kiểm người gọi
/// đang giữ trách nhiệm tổ trưởng, nên tầng 1 chỉ cần loại Teacher. Vice Principal phụ trách phân hiệu duyệt khai báo (`approve`) hoặc xử lý tay từng tiết.
/// </summary>
[ApiController, Route("api/scd/absences")]
public class AbsenceController(TeacherAbsenceService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.TimetableReader)]
    public Task<List<AbsenceDto>> List([FromQuery] int campusId) => service.ListAsync(campusId);

    [HttpPost("{id:int}/approve"), Authorize(Policy = Policies.TimetableManager)]
    public Task<ApproveAbsenceResultDto> Approve(int id) => service.ApproveAsync(id);

    [HttpGet("pending"), Authorize(Policy = Policies.TimetableReader)]
    public Task<List<PendingSessionDto>> Pending([FromQuery] int campusId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? classId) =>
        service.ListPendingAsync(campusId, from, to, classId);

    [HttpGet("pending/{id:long}/suggestions"), Authorize(Policy = Policies.TimetableManager)]
    public Task<List<TeacherAvailabilityDto>> Suggestions(long id) => service.SuggestionsAsync(id);

    [HttpPost("pending/{id:long}/resolve"), Authorize(Policy = Policies.TimetableManager)]
    public Task<AdjustResultDto> Resolve(long id, ResolvePendingRequest request) => service.ResolveAsync(id, request);

    // ---- Tổ trưởng
    [HttpGet("department"), Authorize(Policy = Policies.Teacher)]
    public Task<List<AbsenceDto>> DepartmentList() => service.ListForDepartmentAsync();

    [HttpGet("department/teachers"), Authorize(Policy = Policies.Teacher)]
    public Task<List<TeacherOptionDto>> DepartmentTeachers() => service.DepartmentTeachersAsync();

    [HttpPost("department"), Authorize(Policy = Policies.Teacher)]
    public Task<DeclareAbsenceResultDto> DepartmentDeclare(DeclareAbsenceRequest request) => service.DeclareAsync(request);

    [HttpDelete("department/{id:int}"), Authorize(Policy = Policies.Teacher)]
    public Task<DeleteAbsenceResultDto> DepartmentDelete(int id) => service.DeleteAsync(id);

    [HttpGet("department/pending"), Authorize(Policy = Policies.Teacher)]
    public Task<List<PendingSessionDto>> DepartmentPending() => service.ListPendingForDepartmentAsync();

    [HttpGet("department/pending/{id:long}/suggestions"), Authorize(Policy = Policies.Teacher)]
    public Task<List<TeacherAvailabilityDto>> DepartmentSuggestions(long id) => service.DepartmentSuggestionsAsync(id);

    [HttpPost("department/pending/{id:long}/propose"), Authorize(Policy = Policies.Teacher)]
    public Task<AdjustResultDto> DepartmentPropose(long id, ProposeSessionRequest request) => service.ProposeAsync(id, request);
}
