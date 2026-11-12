using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Lrn.Dtos;
using MSMS.Api.Modules.Lrn.Services;

namespace MSMS.Api.Modules.Lrn.Controllers;

/// <summary>Bản in đề từ đề đang soạn (chưa giao): PDF hoặc Word.</summary>
[ApiController, Route("api/lrn/papers"), Authorize(Policy = Policies.Teacher)]
public class PapersController(ExamPaperService paper) : ControllerBase
{
    [HttpPost("export")]
    public async Task<IActionResult> Export(ExportPaperDto dto)
    {
        var file = await paper.ExportAsync(dto);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
