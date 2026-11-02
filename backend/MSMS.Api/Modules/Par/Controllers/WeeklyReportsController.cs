using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Modules.Par.Dtos;
using MSMS.Api.Modules.Par.Services;

namespace MSMS.Api.Modules.Par.Controllers;

[ApiController, Route("api/par/weekly-reports"), Authorize]
public class WeeklyReportsController(
    WeeklyReportService reportService,
    MsmsDbContext db,
    IClock clock,
    CurrentUser user) : ControllerBase
{
    private void EnsureNotSchoolAdmin()
    {
        if (user.AccountType == AccountTypes.SchoolAdmin)
            throw ApiException.Forbidden("SchoolAdmin không có quyền xem sổ liên lạc.");
    }

    private async Task<bool> CanViewStudentReportAsync(int studentId)
    {
        // Check nếu người dùng chính là học sinh
        if (user.AccountType == AccountTypes.Student)
        {
            var student = await db.StudentProfile
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentId == studentId && s.Student.PersonId == user.PersonId);
            if (student != null)
                return true;
        }

        // Check nếu người dùng là phụ huynh của học sinh
        if (user.AccountType == AccountTypes.Parent)
        {
            var link = await db.ParentStudentLink
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.StudentId == studentId && l.ParentId == user.PersonId);
            if (link != null)
                return true;
        }

        // Check nếu người dùng là giáo viên chủ nhiệm của lớp học sinh đang theo học
        if (user.AccountType == AccountTypes.Teacher)
        {
            var today = clock.Today;

            var isHomeroom = await db.Enrollment
                .AsNoTracking()
                .Where(e => e.StudentId == studentId)
                .Where(EnrollmentRules.ActiveAt(today))
                .AnyAsync(e => e.Class.HomeroomAssignment
                    .Any(ha => ha.StaffId == user.PersonId && ha.StartDate <= today && (ha.EndDate == null || ha.EndDate > today)));
            if (isHomeroom)
                return true;
        }

        return false;
    }

    /// <summary>Danh sách tuần (Thứ 2 - CN) hợp lệ để xem sổ của học sinh.</summary>
    [HttpGet("weeks")]
    public async Task<IActionResult> GetWeeks([FromQuery] int studentId)
    {
        EnsureNotSchoolAdmin();

        if (!await CanViewStudentReportAsync(studentId))
            return Forbid();

        var weeks = await reportService.GetValidWeeksAsync(studentId);
        return Ok(weeks);
    }

    /// <summary>Sổ liên lạc của học sinh cho một tuần (mặc định tuần chứa hôm nay).</summary>
    [HttpGet("student/{studentId:int}")]
    public async Task<IActionResult> GetReport(int studentId, [FromQuery] DateOnly? weekStart)
    {
        EnsureNotSchoolAdmin();

        if (!await CanViewStudentReportAsync(studentId))
            return Forbid();

        var report = await reportService.ComputeWeeklyReportAsync(studentId, weekStart);
        return Ok(report);
    }

    /// <summary>Danh sách học sinh lớp GVCN đang chủ nhiệm (dành cho giáo viên chọn học sinh xem sổ).</summary>
    [HttpGet("homeroom-students"), Authorize(Policy = Policies.Teacher)]
    public async Task<IActionResult> GetHomeroomStudents()
    {
        var students = await reportService.GetHomeroomStudentsAsync();
        return Ok(students);
    }
}
