using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Adm.Services;
using MSMS.Api.Modules.Ppl.Services;

namespace MSMS.Api.Modules.Adm.Controllers;

/// <summary>
/// ADM-03 Xuất dữ liệu (spec 5.2). Tầng 1: Hiệu trưởng, Hiệu phó và giáo viên; tầng 2 (lớp chủ nhiệm, phân hiệu phụ trách) do service kiểm.
/// Hiệu trưởng xuất mọi lớp và mọi phân hiệu. School Admin không xuất Excel (spec 2.3, 2.4).
/// </summary>
[ApiController, Route("api/adm/exports"), Authorize(Policy = Policies.DataExporter)]
public class ExportController(ExportService exports, ResponsibilityService responsibilities) : ControllerBase
{
    /// <summary>4.9.12: chủ nhiệm xuất lớp mình, Hiệu phó xuất lớp thuộc phân hiệu mình, Hiệu trưởng xuất mọi lớp.</summary>
    [HttpGet("attendance")]
    public Task<FileContentResult> Attendance([FromQuery] int classId, [FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        exports.AttendanceAsync(classId, from, to);

    /// <summary>4.9.13: Hiệu phó xuất phân hiệu mình, Hiệu trưởng xuất mọi phân hiệu.</summary>
    [HttpGet("timetable")]
    public Task<FileContentResult> Timetable([FromQuery] int campusId, [FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        exports.TimetableAsync(campusId, from, to);

    /// <summary>Danh sách trách nhiệm đang xem (phụ trách phân hiệu, tổ trưởng, chủ nhiệm): Hiệu trưởng toàn trường, Hiệu phó trong phân hiệu mình phụ trách. Giáo viên và School Admin không xuất.</summary>
    [HttpGet("responsibilities"), Authorize(Policy = Policies.CampusReader)]
    public Task<FileContentResult> Responsibilities([FromQuery] string kind, [FromQuery] int? yearId) =>
        responsibilities.ExportAsync(kind, yearId);
}
