using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Lrn.Dtos;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Api.Modules.Lrn.Controllers;

[ApiController, Route("api/lrn/textbooks"), Authorize(Policy = Policies.Teacher)]
public class TextbooksController(TextbookService service) : ControllerBase
{
    [HttpGet("lessons")]
    public Task<IReadOnlyList<TextbookLessonDto>> Lessons([FromQuery] int subjectId, [FromQuery] byte gradeLevel) => service.LessonsAsync(subjectId, gradeLevel);

    [HttpGet("lessons/{lessonId:int}/pages/{page:int}")]
    public async Task<IActionResult> Page(int lessonId, int page) => File(await service.GetPageImageAsync(lessonId, page), "image/jpeg");
}
