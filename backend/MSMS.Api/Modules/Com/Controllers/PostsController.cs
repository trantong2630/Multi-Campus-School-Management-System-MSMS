using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Com.Dtos;
using MSMS.Api.Modules.Com.Services;

namespace MSMS.Api.Modules.Com.Controllers;

/// <summary>COM-02: bảng tin (BR-27). Đọc: mọi loại tài khoản (service lọc theo phạm vi). Ghi: chỉ cán bộ.</summary>
[ApiController, Route("api/com/posts")]
public class PostsController(PostService service) : ControllerBase
{
    private const int UploadLimit = 12_000_000;   // 5 ảnh × 2 MB cộng phần form

    [HttpGet, Authorize(Policy = Policies.StaffOrFamily)]
    public Task<PagedResult<PostListItemDto>> List([FromQuery] PostQueryDto query) => service.ListAsync(query);

    [HttpGet("{id:int}"), Authorize(Policy = Policies.StaffOrFamily)]
    public Task<PostDetailDto> Get(int id) => service.GetAsync(id);

    [HttpGet("{id:int}/images/{imageId:int}"), Authorize(Policy = Policies.StaffOrFamily)]
    public async Task<IActionResult> Image(int id, int imageId)
    {
        var image = await service.FindImageAsync(id, imageId);
        Response.Headers.CacheControl = "private, max-age=3600";
        return PhysicalFile(image.Path, image.ContentType);
    }

    [HttpPost, Authorize(Policy = Policies.Staff), RequestSizeLimit(UploadLimit)]
    public Task<PostDetailDto> Create([FromForm] PostForm form) => service.CreateAsync(form);

    [HttpPut("{id:int}"), Authorize(Policy = Policies.Staff)]
    public Task<PostDetailDto> Update(int id, PostUpdateRequest r) => service.UpdateAsync(id, r);

    [HttpPost("{id:int}/images"), Authorize(Policy = Policies.Staff), RequestSizeLimit(UploadLimit)]
    public Task<PostDetailDto> AddImages(int id, [FromForm] PostImagesForm form) => service.AddImagesAsync(id, form.Files);

    [HttpDelete("{id:int}/images/{imageId:int}"), Authorize(Policy = Policies.Staff)]
    public Task<PostDetailDto> RemoveImage(int id, int imageId) => service.RemoveImageAsync(id, imageId);

    [HttpPost("{id:int}/hide"), Authorize(Policy = Policies.Staff)]
    public Task<PostDetailDto> Hide(int id, HidePostRequest r) => service.HideAsync(id, r);

    [HttpPost("{id:int}/unhide"), Authorize(Policy = Policies.Staff)]
    public Task<PostDetailDto> Unhide(int id) => service.UnhideAsync(id);

    [HttpDelete("{id:int}"), Authorize(Policy = Policies.Staff)]
    public async Task<IActionResult> Delete(int id) { await service.DeleteAsync(id); return NoContent(); }
}
