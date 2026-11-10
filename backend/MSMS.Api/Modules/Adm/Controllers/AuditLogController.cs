using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Adm.Dtos;
using MSMS.Api.Modules.Adm.Services;

namespace MSMS.Api.Modules.Adm.Controllers;

/// <summary>Nhật ký của School Admin (ADM-02, BR-28). Chỉ có GET: nhật ký chỉ ghi thêm, không có API sửa hay xóa.</summary>
[ApiController, Route("api/adm/audit-log"), Authorize(Policy = Policies.SchoolAdmin)]
public class AuditLogController(AuditLogService auditLog) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditLogItemDto>> List([FromQuery] string? category, [FromQuery] int? actorPersonId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        auditLog.ListAsync(category, actorPersonId, from, to, page, pageSize);

    [HttpGet("actors")]
    public Task<List<AuditActorDto>> Actors() => auditLog.ActorsAsync();

    [HttpGet("{id:long}")]
    public Task<AuditLogDetailDto> Get(long id) => auditLog.GetAsync(id);
}
