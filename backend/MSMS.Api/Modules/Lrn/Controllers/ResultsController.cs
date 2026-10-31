using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Lrn.Dtos;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Api.Modules.Lrn.Controllers;

/// <summary>Kết quả và phân tích bài làm: học sinh xem của mình, phụ huynh xem của con được liên kết. Không có bước công bố (BR-22).</summary>
[ApiController, Route("api/lrn/results")]
public class ResultsController(ResultService service) : ControllerBase
{
    [HttpGet("children"), Authorize(Policy = Policies.Parent)]
    public Task<IReadOnlyList<ChildDto>> Children() => service.MyChildrenAsync();

    [HttpGet("children/{studentId:int}"), Authorize(Policy = Policies.Parent)]
    public Task<IReadOnlyList<ChildResultItemDto>> ChildResults(int studentId) => service.ChildResultsAsync(studentId);

    [HttpGet("{assignmentId:int}"), Authorize(Policy = Policies.StudentOrParent)]
    public Task<PracticeResultDto> Detail(int assignmentId, [FromQuery] int? studentId) => service.ResultAsync(assignmentId, studentId);
}
