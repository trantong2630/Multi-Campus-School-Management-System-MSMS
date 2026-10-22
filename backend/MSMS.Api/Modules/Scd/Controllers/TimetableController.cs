using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Scd.Controllers;

[ApiController, Route("api/scd/timetable")]
public class TimetableController(
    TimetableTemplateService template, TimetableImportService import, TimetablePublishService publish, ScheduleQueryService query)
    : ControllerBase
{
    [HttpGet("campuses"), Authorize(Policy = Policies.TimetableReader)]
    public Task<List<CampusOptionDto>> MyCampuses() => query.MyCampusesAsync();

    [HttpGet("template"), Authorize(Policy = Policies.TimetableManager)]
    public async Task<IActionResult> Template([FromQuery] int campusId, [FromQuery] int semesterId) => await template.BuildAsync(campusId, semesterId);

    [HttpPost("import"), Authorize(Policy = Policies.TimetableManager)]
    [RequestSizeLimit(10_000_000)]
    public async Task<TimetableImportResult> Import(IFormFile file)
    {
        if (file is null || file.Length == 0) throw ApiException.BadRequest("Chưa chọn file. Chọn file .xlsx rồi tải lên.");
        await using var stream = file.OpenReadStream();
        return await import.ImportAsync(stream, file.FileName);
    }

    [HttpGet("versions"), Authorize(Policy = Policies.TimetableReader)]
    public Task<List<TimetableVersionDto>> Versions([FromQuery] int campusId, [FromQuery] int semesterId) => query.ListVersionsAsync(campusId, semesterId);

    [HttpGet("versions/{id:int}"), Authorize(Policy = Policies.TimetableReader)]
    public Task<VersionGridDto> VersionGrid(int id) => query.GetVersionGridAsync(id);

    [HttpGet("versions/{id:int}/preview"), Authorize(Policy = Policies.TimetableManager)]
    public Task<PublishPreviewDto> Preview(int id) => publish.PreviewAsync(id);

    [HttpPost("versions/{id:int}/publish"), Authorize(Policy = Policies.TimetableManager)]
    public Task<PublishResultDto> Publish(int id, PublishRequest request) => publish.PublishAsync(id, request);

}
