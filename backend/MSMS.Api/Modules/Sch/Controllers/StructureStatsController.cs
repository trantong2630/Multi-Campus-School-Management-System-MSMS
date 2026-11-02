using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Sch.Controllers;

/// <summary>Thống kê cấu trúc cho các màn Thiết lập: School Admin (sửa), Principal và Vice Principal (chỉ xem, theo phạm vi).</summary>
[ApiController, Route("api/sch/stats")]
public class StructureStatsController(StructureStatsService service) : ControllerBase
{
    [HttpGet("campuses"), Authorize(Policy = Policies.Management)]
    public Task<List<CampusStatDto>> Campuses() => service.CampusesAsync();

    [HttpGet("catalog"), Authorize(Policy = Policies.Management)]
    public Task<CatalogStatsDto> Catalog() => service.CatalogAsync();

    [HttpGet("classes"), Authorize(Policy = Policies.Management)]
    public Task<ClassStatsDto> Classes() => service.ClassesAsync();
}
