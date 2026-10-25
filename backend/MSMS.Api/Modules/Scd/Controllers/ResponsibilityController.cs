using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Scd.Controllers;

[ApiController, Route("api/scd/responsibilities")]
public class ResponsibilityController(SubjectResponsibilityService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.TimetableManager)]
    public Task<ResponsibilityDto> List([FromQuery] int? campusId, [FromQuery] int? subjectId) => service.ListAsync(campusId, subjectId);
}
