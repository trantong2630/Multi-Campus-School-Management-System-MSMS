using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Scd.Controllers;

[ApiController, Route("api/scd/timetable")]
public class TimetableController(
    TimetableTemplateService template, TimetableImportService import, TimetablePublishService publish, TimetableDraftService draft,
    ScheduleQueryService query)
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

    // ---- Sửa bản Nháp (BR-12) ----
    [HttpGet("draft-options"), Authorize(Policy = Policies.TimetableManager)]
    public Task<DraftOptionsDto> DraftOptions([FromQuery] int campusId) => draft.OptionsAsync(campusId);

    [HttpPost("versions/{id:int}/entries/move"), Authorize(Policy = Policies.TimetableManager)]
    public Task<VersionGridDto> MoveEntry(int id, MoveEntryRequest request) => draft.MoveAsync(id, request);

    [HttpPost("versions/{id:int}/entries"), Authorize(Policy = Policies.TimetableManager)]
    public Task<VersionGridDto> AddEntry(int id, AddEntryRequest request) => draft.AddAsync(id, request);

    [HttpPut("versions/{id:int}/entries/teacher"), Authorize(Policy = Policies.TimetableManager)]
    public Task<VersionGridDto> ChangeEntryTeacher(int id, ChangeTeacherRequest request) => draft.ChangeTeacherAsync(id, request);

    [HttpDelete("versions/{id:int}/entries"), Authorize(Policy = Policies.TimetableManager)]
    public Task<VersionGridDto> RemoveEntry(int id, [FromQuery] int classId, [FromQuery] int dayOfWeek, [FromQuery] int periodSlotId)
        => draft.RemoveAsync(id, classId, dayOfWeek, periodSlotId);

    [HttpPost("versions/{id:int}/reset"), Authorize(Policy = Policies.TimetableManager)]
    public Task<VersionGridDto> ResetDraft(int id) => draft.ResetAsync(id);

}
