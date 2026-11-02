using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Lrn.Dtos;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Api.Modules.Lrn.Controllers;

/// <summary>Phía học sinh: xem bài được giao, bắt đầu, làm bài, tự lưu nháp, nộp bài. Chỉ Student, bằng tài khoản của mình (mục 2.4).</summary>
[ApiController, Route("api/lrn/my-assignments"), Authorize(Policy = Policies.Student)]
public class StudentAssignmentsController(AttemptService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<MyAssignmentItemDto>> List() => service.ListMineAsync();

    [HttpGet("{id:int}/attempt")]
    public Task<AttemptDto> Open(int id) => service.OpenAsync(id);

    [HttpPost("{id:int}/start")]
    public Task<AttemptDto> Start(int id) => service.StartAsync(id);

    [HttpPut("{id:int}/answers")]
    public async Task<IActionResult> SaveDraft(int id, SaveAnswersDto dto)
    {
        var savedAt = await service.SaveDraftAsync(id, dto);
        return Ok(new { savedAt });
    }

    [HttpPost("{id:int}/submit")]
    public Task<SubmitResultDto> Submit(int id, SaveAnswersDto dto) => service.SubmitAsync(id, dto);
}
