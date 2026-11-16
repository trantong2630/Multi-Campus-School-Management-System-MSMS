using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Iam.Dtos;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Modules.Iam.Controllers;

/// <summary>Phân quyền động: bảng chức năng × loại tài khoản, chỉ School Admin xem và sửa.</summary>
[ApiController, Route("api/iam/permissions"), Authorize(Policy = Policies.SchoolAdmin)]
public class PermissionsController(FeaturePermissionService service) : ControllerBase
{
    [HttpGet]
    public Task<FeatureMatrix> Get() => service.GetMatrixAsync();

    /// <summary>Đếm việc đang dở của các chức năng sắp tắt; không lưu gì.</summary>
    [HttpPost("impact")]
    public Task<List<FeatureImpact>> Impact(UpdateFeaturePermissionsRequest r) => service.ImpactAsync(r);

    [HttpPut]
    public async Task<FeatureMatrix> Update(UpdateFeaturePermissionsRequest r)
    {
        await service.UpdateAsync(r);
        return await service.GetMatrixAsync();
    }
}
