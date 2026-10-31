using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;

namespace MSMS.Api.Modules.Ppl.Controllers;

/// <summary>PPL-02: import học sinh kèm ghi danh và phụ huynh (4.3.16–4.3.18). Chỉ School Admin.</summary>
[ApiController, Route("api/ppl/students")]
public class StudentsController(StudentImportService import) : ControllerBase
{
    /// <summary>Tải mẫu; có thể điền sẵn hai ô meta (năm học, phân hiệu) khi mở từ màn hình Lớp & Ghi danh.</summary>
    [HttpGet("import-template"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<FileContentResult> Template([FromQuery] int? yearId, [FromQuery] int? campusId) => import.BuildTemplateAsync(yearId, campusId);

    [HttpPost("import"), Authorize(Policy = Policies.SchoolAdmin)]
    [RequestSizeLimit(10_000_000)]
    public async Task<StudentImportResult> Import(IFormFile file)
    {
        if (file is null || file.Length == 0) throw ApiException.BadRequest("Chưa chọn file. Chọn file .xlsx rồi tải lên.");
        await using var stream = file.OpenReadStream();
        return await import.ImportAsync(stream);
    }
}
