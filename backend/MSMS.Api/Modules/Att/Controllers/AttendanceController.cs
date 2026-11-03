using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Att.Dtos;
using MSMS.Api.Modules.Att.Services;

namespace MSMS.Api.Modules.Att.Controllers;

/// <summary>
/// ATT-01 Điểm danh theo tiết. Policy chặn loại tài khoản (tầng 1); phạm vi từng tiết, từng lớp, từng học sinh do service kiểm (tầng 2).
/// </summary>
[ApiController, Route("api/att")]
public class AttendanceController(AttendanceService service) : ControllerBase
{
    /// <summary>4.5.1: tiết giáo viên dạy hôm nay.</summary>
    [HttpGet("today"), Authorize(Policy = Policies.Teacher)]
    public Task<List<AttendanceSessionDto>> Today() => service.TodayAsync();

    /// <summary>Lớp xem được điểm danh: SA, HT, HP (phân hiệu mình), GV (lớp chủ nhiệm).</summary>
    [HttpGet("classes"), Authorize(Policy = Policies.Staff)]
    public Task<List<AttendanceClassOptionDto>> Classes() => service.ClassesAsync();

    /// <summary>4.5.6: tiết của lớp trong khoảng ngày; missing=true chỉ lấy tiết đã qua chưa điểm danh.</summary>
    [HttpGet("sessions"), Authorize(Policy = Policies.Management)]
    public Task<List<AttendanceSessionDto>> Sessions([FromQuery] int classId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] bool missing = false) =>
        service.ClassSessionsAsync(classId, from, to, missing);

    /// <summary>4.5.2: bảng điểm danh của tiết.</summary>
    [HttpGet("sessions/{sessionId:long}"), Authorize(Policy = Policies.Staff)]
    public Task<AttendanceSheetDto> Sheet(long sessionId) => service.GetSheetAsync(sessionId);

    /// <summary>4.5.3, 4.5.4, 4.5.7, 4.5.8: ghi hoặc sửa điểm danh của tiết.</summary>
    [HttpPut("sessions/{sessionId:long}"), Authorize(Policy = Policies.AttendanceWriter)]
    public Task<AttendanceSheetDto> Save(long sessionId, SaveAttendanceRequest r) => service.SaveAsync(sessionId, r);

    /// <summary>4.5.13: chuyên cần của lớp theo ngày hoặc khoảng ngày.</summary>
    [HttpGet("class-summary"), Authorize(Policy = Policies.Staff)]
    public Task<ClassAttendanceDto> ClassSummary([FromQuery] int classId, [FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        service.ClassSummaryAsync(classId, from, to);

    /// <summary>4.5.11: học sinh xem chuyên cần của mình.</summary>
    [HttpGet("me"), Authorize(Policy = Policies.Student)]
    public Task<StudentAttendanceDto> Mine([FromQuery] DateOnly from, [FromQuery] DateOnly to) => service.MineAsync(from, to);

    /// <summary>4.5.12: phụ huynh xem chuyên cần của con.</summary>
    [HttpGet("child"), Authorize(Policy = Policies.Parent)]
    public Task<StudentAttendanceDto> Child([FromQuery] int studentId, [FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        service.ChildAsync(studentId, from, to);
}
