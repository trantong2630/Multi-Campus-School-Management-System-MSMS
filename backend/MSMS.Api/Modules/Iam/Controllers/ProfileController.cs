using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Iam.Dtos;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Modules.Iam.Controllers;

/// <summary>
/// IAM-04: hồ sơ cá nhân của người dùng đang đăng nhập.
/// GET    /api/iam/profile       — xem hồ sơ và trạng thái tài khoản (4.1.27, 4.1.30)
/// PUT    /api/iam/profile       — cập nhật trường cá nhân (4.1.28)
/// POST   /api/iam/profile/avatar — tải ảnh đại diện (4.1.29)
/// DELETE /api/iam/profile/avatar — gỡ ảnh đại diện (4.1.29)
/// GET    /api/iam/profile/avatar/{personId} — phục vụ file ảnh (public, dùng cho mọi nơi hiển thị avatar)
/// </summary>
[ApiController]
[Route("api/iam/profile")]
[Authorize]
public class ProfileController(ProfileService profile) : ControllerBase
{
    [HttpGet]
    public Task<OwnProfileDto> Get() => profile.GetAsync();

    [HttpPut]
    public Task<OwnProfileDto> Update(UpdateProfileRequest r) => profile.UpdateAsync(r);

    [HttpPost("avatar")]
    [RequestSizeLimit(3_000_000)]
    public Task<OwnProfileDto> UploadAvatar(IFormFile file)
    {
        if (file is null) throw ApiException.BadRequest("Chưa chọn ảnh. Chọn file JPG hoặc PNG rồi tải lên.");
        return profile.SetAvatarAsync(file);
    }

    [HttpDelete("avatar")]
    public Task<OwnProfileDto> RemoveAvatar() => profile.RemoveAvatarAsync();

    /// <summary>
    /// Phục vụ ảnh đại diện theo personId. Yêu cầu đăng nhập nhưng không yêu cầu đúng chủ sở hữu
    /// vì ảnh đại diện xuất hiện ở danh sách người (Design_Web §8.2b) mà người khác cũng nhìn thấy.
    /// Giao diện gắn ?v=&lt;avatarVersion&gt; nên cho trình duyệt cache được.
    /// </summary>
    [HttpGet("avatar/{personId:int}")]
    public async Task<IActionResult> GetAvatar(int personId)
    {
        var avatar = await profile.FindAvatarAsync(personId);
        if (avatar is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=86400";
        return File(avatar.Content, avatar.ContentType);
    }
}

