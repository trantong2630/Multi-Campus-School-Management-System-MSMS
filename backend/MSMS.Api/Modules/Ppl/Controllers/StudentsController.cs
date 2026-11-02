using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Ppl.Dtos;
using MSMS.Api.Modules.Ppl.Services;

namespace MSMS.Api.Modules.Ppl.Controllers;

/// <summary>PPL-02: import học sinh kèm ghi danh và phụ huynh (4.3.16–4.3.18). Chỉ School Admin.</summary>
[ApiController, Route("api/ppl/students")]
public class StudentsController(StudentImportService import, StudentService students) : ControllerBase
{
    // 4.3.12–4.3.13: danh bạ + lọc. Hiệu phó chỉ thấy học sinh của phân hiệu mình phụ trách.
    [HttpGet, Authorize(Policy = Policies.CampusReader)]
    public Task<PagedResult<StudentDto>> List([FromQuery] int? yearId, [FromQuery] int? campusId, [FromQuery] int? classId, [FromQuery] string? status,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => students.ListAsync(yearId, campusId, classId, status, search, page, pageSize);

    // 4.3.14–4.3.15: tạo và sửa hồ sơ; số định danh không sửa được.
    [HttpPost, Authorize(Policy = Policies.SchoolAdmin)]
    public Task<StudentDto> Create(StudentRequest r) => students.CreateAsync(r);

    [HttpPut("{id:int}"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<StudentDto> Update(int id, StudentRequest r) => students.UpdateAsync(id, r);
    // 4.3.19–4.3.20: chuyển trường / tốt nghiệp, một hoặc nhiều học sinh một lượt.
    [HttpPost("leave"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> Leave(StudentLeaveRequest r) { await students.LeaveAsync(r); return NoContent(); }

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
