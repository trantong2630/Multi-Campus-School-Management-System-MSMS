using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Adm.Services;

namespace MSMS.Api.Modules.Adm.Controllers;

/// <summary>
/// ADM-03 Xuất dữ liệu (spec 5.2). Tầng 1: Hiệu phó và giáo viên; tầng 2 (lớp chủ nhiệm, phân hiệu phụ trách) do service kiểm.
/// School Admin và Hiệu trưởng không xuất Excel (spec 2.3, 2.4).
/// </summary>
[ApiController, Route("api/adm/exports"), Authorize(Policy = Policies.DataExporter)]
public class ExportController(ExportService exports) : ControllerBase
{
    /// <summary>4.9.10: chủ nhiệm xuất lớp mình, Hiệu phó xuất lớp thuộc phân hiệu mình.</summary>
    [HttpGet("attendance")]
    public Task<FileContentResult> Attendance([FromQuery] int classId, [FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        exports.AttendanceAsync(classId, from, to);

    /// <summary>4.9.11: Hiệu phó xuất phân hiệu mình.</summary>
    [HttpGet("timetable")]
    public Task<FileContentResult> Timetable([FromQuery] int campusId, [FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        exports.TimetableAsync(campusId, from, to);
}
