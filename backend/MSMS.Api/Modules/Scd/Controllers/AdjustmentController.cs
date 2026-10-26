using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Scd.Controllers;

/// <summary>SCD-02: điều chỉnh lịch dạy (BR-15). Xem tuần cho Principal và Vice Principal; mọi thao tác ghi chỉ Vice Principal phụ trách phân hiệu.</summary>
[ApiController, Route("api/scd/adjust")]
public class AdjustmentController(ScheduleAdjustmentService service) : ControllerBase
{
    [HttpGet("week"), Authorize(Policy = Policies.TimetableReader)]
    public Task<AdjustWeekDto> Week([FromQuery] int classId, [FromQuery] DateOnly weekStart) => service.WeekAsync(classId, weekStart);

    [HttpGet("teachers"), Authorize(Policy = Policies.TimetableManager)]
    public Task<List<TeacherAvailabilityDto>> Teachers([FromQuery] DateOnly date, [FromQuery] int periodSlotId, [FromQuery] long? excludeSessionId) =>
        service.AvailableTeachersAsync(date, periodSlotId, excludeSessionId);

    [HttpPut("sessions/{id:long}/teacher"), Authorize(Policy = Policies.TimetableManager)]
    public Task<AdjustResultDto> ChangeTeacher(long id, ChangeSessionTeacherRequest request) => service.ChangeTeacherAsync(id, request);

    [HttpPost("sessions/{id:long}/cancel"), Authorize(Policy = Policies.TimetableManager)]
    public Task<AdjustResultDto> Cancel(long id) => service.CancelAsync(id);

    [HttpPost("makeup"), Authorize(Policy = Policies.TimetableManager)]
    public Task<AdjustResultDto> AddMakeUp(MakeUpRequest request) => service.AddMakeUpAsync(request);
}
