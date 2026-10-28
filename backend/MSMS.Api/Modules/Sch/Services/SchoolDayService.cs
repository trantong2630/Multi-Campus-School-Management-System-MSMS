using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data;

namespace MSMS.Api.Modules.Sch.Services;

/// <summary>
/// SCH-03 (4.2.20): trả lời "ngày D có phải ngày dạy của phân hiệu P không". Chỉ đọc dữ liệu học kỳ và ngày nghỉ rồi giao cho
/// <see cref="SchoolCalendarRules"/>. Module khác (SCD sinh tiết, thêm tiết bù, khai ngày nghỉ đột xuất) gọi service này,
/// không tự query học kỳ hay ngày nghỉ.
/// </summary>
public sealed class SchoolDayService(MsmsDbContext db)
{
    public async Task<SchoolDayStatus> StatusAsync(int campusId, DateOnly date)
    {
        var (semesters, holidays) = await LoadAsync(campusId, date, date);
        return SchoolCalendarRules.Classify(date, semesters, holidays);
    }

    public async Task<bool> IsSchoolDayAsync(int campusId, DateOnly date) =>
        await StatusAsync(campusId, date) == SchoolDayStatus.SchoolDay;

    /// <summary>Các ngày dạy của phân hiệu trong [from, to], theo thứ tự tăng dần.</summary>
    public async Task<List<DateOnly>> SchoolDaysAsync(int campusId, DateOnly from, DateOnly to)
    {
        if (from > to) return [];
        var (semesters, holidays) = await LoadAsync(campusId, from, to);
        return SchoolCalendarRules.SchoolDays(from, to, semesters, holidays).ToList();
    }

    private async Task<(List<(DateOnly Start, DateOnly End)> Semesters, HashSet<DateOnly> Holidays)> LoadAsync(
        int campusId, DateOnly from, DateOnly to)
    {
        if (!await db.Campus.AnyAsync(c => c.CampusId == campusId))
            throw ApiException.NotFound("Không tìm thấy phân hiệu.");

        var semesters = (await db.Semester.AsNoTracking()
                .Where(s => s.StartDate <= to && s.EndDate >= from)
                .Select(s => new { s.StartDate, s.EndDate }).ToListAsync())
            .Select(s => (s.StartDate, s.EndDate)).ToList();
        var holidays = (await db.Holiday.AsNoTracking()
                .Where(h => (h.CampusId == null || h.CampusId == campusId) && h.HolidayDate >= from && h.HolidayDate <= to)
                .Select(h => h.HolidayDate).ToListAsync())
            .ToHashSet();
        return (semesters, holidays);
    }
}
