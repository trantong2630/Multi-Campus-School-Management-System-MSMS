using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSMS.Api.Common.Auth;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Modules.Sch.Controllers;

[ApiController, Route("api/sch")]
public class CalendarController(CalendarService service) : ControllerBase
{
    [HttpGet("academic-years"), Authorize(Policy = Policies.Staff)]
    public Task<List<AcademicYearDto>> ListYears() => service.ListYearsAsync();

    [HttpPost("academic-years"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<AcademicYearDto> CreateYear(AcademicYearRequest r) => service.CreateYearAsync(r);

    [HttpPut("academic-years/{id:int}"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> UpdateYear(int id, AcademicYearRequest r) { await service.UpdateYearAsync(id, r); return NoContent(); }

    [HttpPut("academic-years/{id:int}/current"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> SetCurrent(int id) { await service.SetCurrentYearAsync(id); return NoContent(); }

    [HttpPut("academic-years/{id:int}/semesters"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<AcademicYearDto> ConfigureSemesters(int id, SemestersRequest r) => service.ConfigureSemestersAsync(id, r);

    [HttpDelete("academic-years/{id:int}"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> DeleteYear(int id) { await service.DeleteYearAsync(id); return NoContent(); }

    [HttpGet("holidays"), Authorize(Policy = Policies.Staff)]
    public Task<List<HolidayDto>> ListHolidays([FromQuery] int? yearId, [FromQuery] int? campusId) => service.ListHolidaysAsync(yearId, campusId);

    [HttpPost("holidays"), Authorize(Policy = Policies.SchoolAdmin)]
    public Task<HolidayDto> CreateHoliday(HolidayRequest r) => service.CreateHolidayAsync(r);

    [HttpDelete("holidays/{id:int}"), Authorize(Policy = Policies.SchoolAdmin)]
    public async Task<IActionResult> DeleteHoliday(int id) { await service.DeleteHolidayAsync(id); return NoContent(); }
}
