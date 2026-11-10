using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Sch.Controllers;

/// <summary>Thống kê cấu trúc cho các màn Cơ cấu nhà trường: School Admin (sửa) và Principal (chỉ xem, toàn trường).</summary>
[ApiController, Route("api/sch/stats")]
public class StructureStatsController(StructureStatsService service) : ControllerBase
{
    [HttpGet("campuses"), Authorize(Policy = Policies.SchoolStructureReader)]
    public Task<List<CampusStatDto>> Campuses() => service.CampusesAsync();

    [HttpGet("catalog"), Authorize(Policy = Policies.SchoolStructureReader)]
    public Task<CatalogStatsDto> Catalog() => service.CatalogAsync();

    [HttpGet("classes"), Authorize(Policy = Policies.SchoolStructureReader)]
    public Task<ClassStatsDto> Classes() => service.ClassesAsync();
}
