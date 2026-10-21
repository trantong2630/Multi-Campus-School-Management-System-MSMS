using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Scd.Controllers;

[ApiController, Route("api/scd/schedule")]
public class ScheduleController(ScheduleQueryService query) : ControllerBase
{
    [HttpGet("day"), Authorize(Policy = Policies.CampusReader)]
    public Task<DayScheduleDto> Day([FromQuery] int campusId, [FromQuery] DateOnly date) => query.GetDayAsync(campusId, date);

    [HttpGet("mine"), Authorize(Policy = Policies.Staff)]
    public Task<List<MySessionDto>> Mine([FromQuery] DateOnly from, [FromQuery] DateOnly to) => query.GetMineAsync(from, to);
}
