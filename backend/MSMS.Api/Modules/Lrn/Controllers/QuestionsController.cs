using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Lrn.Dtos;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Api.Modules.Lrn.Controllers;

[ApiController, Route("api/lrn/questions"), Authorize(Policy = Policies.Teacher)]
public class QuestionsController(QuestionService service, QuestionDraftService drafts) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<QuestionItemDto>> List(
        [FromQuery] int? subjectId, [FromQuery] byte? gradeLevel, [FromQuery] string? topic, [FromQuery] string? creator,
        [FromQuery] string? status, [FromQuery] bool mine = false, [FromQuery] bool includeHidden = false, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => service.ListAsync(subjectId, gradeLevel, topic, creator, status, mine, includeHidden, search, page, pageSize);

    [HttpGet("my-subjects")]
    public Task<IReadOnlyList<SubjectOptionDto>> MySubjects() => service.MySubjectsAsync();

    [HttpGet("{id:int}")]
    public Task<QuestionDetailDto> Get(int id) => service.GetAsync(id);

    [HttpPost]
    public Task<QuestionDetailDto> Create(SaveQuestionDto dto) => service.CreateAsync(dto);

    [HttpPut("{id:int}")]
    public Task<QuestionDetailDto> Update(int id, SaveQuestionDto dto) => service.UpdateAsync(id, dto);

    [HttpPut("{id:int}/hide")]
    public async Task<IActionResult> Hide(int id) { await service.HideAsync(id); return NoContent(); }

    [HttpPut("{id:int}/unhide")]
    public async Task<IActionResult> Unhide(int id) { await service.UnhideAsync(id); return NoContent(); }

    [HttpPut("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id) { await service.ConfirmAsync(id); return NoContent(); }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDraft(int id) { await service.DeleteDraftAsync(id); return NoContent(); }

    [HttpPost("ai-drafts")]
    public Task<GenerateQuestionsResultDto> GenerateDrafts(GenerateQuestionsDto dto, CancellationToken ct) => drafts.GenerateAsync(dto, ct);
}
