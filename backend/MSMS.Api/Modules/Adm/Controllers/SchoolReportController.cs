using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Adm.Dtos;
using MSMS.Api.Modules.Adm.Services;

namespace MSMS.Api.Modules.Adm.Controllers;

/// <summary>Báo cáo toàn trường (ADM-01): chỉ Hiệu trưởng, so sánh phân hiệu và lớp kèm cảnh báo. Không có số liệu từng học sinh.</summary>
[ApiController, Route("api/adm/report"), Authorize(Policy = Policies.SchoolReport)]
public class SchoolReportController(SchoolReportService report) : ControllerBase
{
    [HttpGet]
    public Task<SchoolReportDto> Get([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? campusId, [FromQuery] int? gradeLevel) =>
        report.GetAsync(from, to, campusId, gradeLevel);
}
