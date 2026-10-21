using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Sch.Controllers;

[ApiController, Route("api/school")]
public class SchoolController(SchoolProfileService service) : ControllerBase
{
    // Công khai: màn hình đăng nhập cũng cần tên và logo trường để nhận diện.
    [HttpGet, AllowAnonymous]
    public Task<SchoolProfileDto> Get() => service.GetAsync();

    [HttpPut, Authorize(Policy = Policies.SchoolProfileManager)]
    public Task<SchoolProfileDto> Update(SchoolProfileRequest r) => service.UpdateAsync(r);

    [HttpGet("logo"), AllowAnonymous]
    public async Task<IActionResult> Logo()
    {
        var logo = await service.FindLogoAsync();
        if (logo is null) return NotFound();
        // Giao diện thêm ?v=<phiên bản> vào đường dẫn nên có thể cho trình duyệt cache lâu.
        Response.Headers.CacheControl = "public, max-age=604800";
        return PhysicalFile(logo.Value.Path, logo.Value.ContentType);
    }

    [HttpPost("logo"), Authorize(Policy = Policies.SchoolProfileManager)]
    [RequestSizeLimit(3_000_000)]
    public Task<SchoolProfileDto> UploadLogo(IFormFile file)
    {
        if (file is null) throw ApiException.BadRequest("Chưa chọn ảnh. Chọn file JPG hoặc PNG rồi tải lên.");
        return service.SetLogoAsync(file);
    }

    [HttpDelete("logo"), Authorize(Policy = Policies.SchoolProfileManager)]
    public Task<SchoolProfileDto> RemoveLogo() => service.RemoveLogoAsync();
}
