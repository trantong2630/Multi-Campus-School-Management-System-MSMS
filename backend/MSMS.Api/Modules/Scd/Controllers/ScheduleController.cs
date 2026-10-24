using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Scd.Dtos;
using MSMS.Api.Modules.Scd.Services;

namespace MSMS.Api.Modules.Scd.Controllers;

[ApiController, Route("api/scd/schedule")]
public class ScheduleController(ScheduleQueryService query) : ControllerBase
{
    [HttpGet("day"), Authorize(Policy = Policies.TimetableReader)]
    public Task<DayScheduleDto> Day([FromQuery] int campusId, [FromQuery] DateOnly date) => query.GetDayAsync(campusId, date);

    [HttpGet("mine"), Authorize(Policy = Policies.Staff)]
    public Task<List<MySessionDto>> Mine([FromQuery] DateOnly from, [FromQuery] DateOnly to) => query.GetMineAsync(from, to);

    [HttpGet("classes"), Authorize(Policy = Policies.TimetableReader)]
    public Task<List<GridClassDto>> Classes([FromQuery] int campusId) => query.ListClassesAsync(campusId);

    [HttpGet("teachers"), Authorize(Policy = Policies.TimetableReader)]
    public Task<List<TeacherOptionDto>> Teachers([FromQuery] int campusId) => query.ListTeachersAsync(campusId);

    [HttpGet("class"), Authorize(Policy = Policies.TimetableReader)]
    public Task<List<ViewSessionDto>> ByClass([FromQuery] int classId, [FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        query.GetClassSessionsAsync(classId, from, to);

    [HttpGet("teacher"), Authorize(Policy = Policies.TimetableReader)]
    public Task<List<ViewSessionDto>> ByTeacher([FromQuery] int teacherId, [FromQuery] DateOnly from, [FromQuery] DateOnly to) =>
        query.GetTeacherSessionsAsync(teacherId, from, to);
}
