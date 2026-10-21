using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;

namespace MSMS.Api.Modules.Sch.Services;

/// <summary>SCH-03: năm học, hai học kỳ, ngày nghỉ theo kế hoạch.</summary>
public sealed class CalendarService(MsmsDbContext db, CurrentUser user, IClock clock)
{
    // ---- Năm học ----
    public async Task<List<AcademicYearDto>> ListYearsAsync()
    {
        var years = await db.AcademicYear.AsNoTracking().Include(y => y.Semester)
            .OrderByDescending(y => y.StartDate).ToListAsync();
        return years.Select(ToDto).ToList();
    }

    public async Task<AcademicYearDto> CreateYearAsync(AcademicYearRequest r)
    {
        var name = Guard.Required(r.Name, "Tên năm học", 20);
        if (r.EndDate <= r.StartDate) throw ApiException.BadRequest("Ngày kết thúc phải sau ngày bắt đầu năm học.");
        if (await db.AcademicYear.AnyAsync(y => y.Name == name))
            throw ApiException.BadRequest($"Năm học \"{name}\" đã có. Chọn tên khác.");

        var year = new AcademicYear { Name = name, StartDate = r.StartDate, EndDate = r.EndDate, IsCurrent = false };
        // Năm học đầu tiên tự thành hiện hành để các màn hình vận hành có năm mặc định.
        if (!await db.AcademicYear.AnyAsync()) year.IsCurrent = true;
        db.AcademicYear.Add(year);
        await db.SaveChangesAsync();
        return ToDto(year);
    }

    public async Task UpdateYearAsync(int id, AcademicYearRequest r)
    {
        var year = await db.AcademicYear.Include(y => y.Semester).FirstOrDefaultAsync(y => y.AcademicYearId == id)
            ?? throw ApiException.NotFound("Không tìm thấy năm học.");
        var name = Guard.Required(r.Name, "Tên năm học", 20);
        if (r.EndDate <= r.StartDate) throw ApiException.BadRequest("Ngày kết thúc phải sau ngày bắt đầu năm học.");
        if (await db.AcademicYear.AnyAsync(y => y.AcademicYearId != id && y.Name == name))
            throw ApiException.BadRequest($"Năm học \"{name}\" đã có. Chọn tên khác.");
        foreach (var s in year.Semester)
        {
            if (s.StartDate < r.StartDate || s.EndDate > r.EndDate)
                throw ApiException.BadRequest($"{s.Name} đang nằm ngoài khoảng năm học mới. Sửa học kỳ trước hoặc chọn khoảng rộng hơn.");
        }
        year.Name = name;
        year.StartDate = r.StartDate;
        year.EndDate = r.EndDate;
        await db.SaveChangesAsync();
    }

    /// <summary>Đúng một năm học hiện hành tại một thời điểm (BR-08).</summary>
    public async Task SetCurrentYearAsync(int id)
    {
        var years = await db.AcademicYear.ToListAsync();
        if (years.All(y => y.AcademicYearId != id)) throw ApiException.NotFound("Không tìm thấy năm học.");
        foreach (var y in years) y.IsCurrent = y.AcademicYearId == id;
        await db.SaveChangesAsync();
    }

    // ---- Học kỳ ----
    /// <summary>Mỗi năm đúng hai học kỳ, nằm trong năm học, không chồng nhau, kỳ 1 trước kỳ 2 (BR-08).</summary>
    public async Task<AcademicYearDto> ConfigureSemestersAsync(int yearId, SemestersRequest r)
    {
        var year = await db.AcademicYear.Include(y => y.Semester).FirstOrDefaultAsync(y => y.AcademicYearId == yearId)
            ?? throw ApiException.NotFound("Không tìm thấy năm học.");

        foreach (var (no, range) in new[] { (1, r.Semester1), (2, r.Semester2) })
        {
            if (range.EndDate <= range.StartDate)
                throw ApiException.BadRequest($"Học kỳ {no}: ngày kết thúc phải sau ngày bắt đầu.");
            if (range.StartDate < year.StartDate || range.EndDate > year.EndDate)
                throw ApiException.BadRequest(
                    $"Học kỳ {no} phải nằm trong năm học {year.Name} ({year.StartDate:dd/MM/yyyy} – {year.EndDate:dd/MM/yyyy}).");
        }
        if (r.Semester1.EndDate >= r.Semester2.StartDate)
            throw ApiException.BadRequest("Học kỳ 1 phải kết thúc trước ngày bắt đầu học kỳ 2, hai học kỳ không được chồng nhau.");

        Upsert(year, 1, r.Semester1);
        Upsert(year, 2, r.Semester2);
        await db.SaveChangesAsync();
        return ToDto(year);
    }

    private void Upsert(AcademicYear year, byte no, SemesterRange range)
    {
        var sem = year.Semester.FirstOrDefault(s => s.SemesterNo == no);
        if (sem is null)
        {
            sem = new Semester { AcademicYearId = year.AcademicYearId, SemesterNo = no, Name = $"Học kỳ {no}" };
            year.Semester.Add(sem);
        }
        sem.StartDate = range.StartDate;
        sem.EndDate = range.EndDate;
    }

    // ---- Ngày nghỉ ----
    public async Task<List<HolidayDto>> ListHolidaysAsync(int? yearId, int? campusId) =>
        await db.Holiday.AsNoTracking()
            .Where(h => (yearId == null || h.AcademicYearId == yearId)
                        && (campusId == null || h.CampusId == campusId || h.CampusId == null))
            .OrderBy(h => h.HolidayDate)
            .Select(h => new HolidayDto(h.HolidayId, h.AcademicYearId, h.CampusId, h.Campus == null ? null : h.Campus.Name,
                h.HolidayDate, h.Reason, h.IsAdHoc))
            .ToListAsync();

    public async Task<HolidayDto> CreateHolidayAsync(HolidayRequest r)
    {
        var year = await db.AcademicYear.FindAsync(r.AcademicYearId) ?? throw ApiException.BadRequest("Chọn năm học cho ngày nghỉ.");
        if (r.HolidayDate < year.StartDate || r.HolidayDate > year.EndDate)
            throw ApiException.BadRequest($"Ngày nghỉ phải nằm trong năm học {year.Name}.");
        string? campusName = null;
        if (r.CampusId is { } cid)
        {
            var campus = await db.Campus.FindAsync(cid) ?? throw ApiException.BadRequest("Phân hiệu không tồn tại.");
            campusName = campus.Name;
        }
        if (await db.Holiday.AnyAsync(h => h.AcademicYearId == r.AcademicYearId && h.HolidayDate == r.HolidayDate && h.CampusId == r.CampusId))
            throw ApiException.BadRequest("Ngày này đã khai là ngày nghỉ cho phạm vi đã chọn.");

        var holiday = new Holiday
        {
            AcademicYearId = r.AcademicYearId, CampusId = r.CampusId, HolidayDate = r.HolidayDate,
            Reason = Guard.Optional(r.Reason, "Lý do", 255), IsAdHoc = false,
            CreatedByPersonId = user.PersonId, CreatedAt = clock.Now,
        };
        db.Holiday.Add(holiday);
        await db.SaveChangesAsync();
        return new HolidayDto(holiday.HolidayId, holiday.AcademicYearId, holiday.CampusId, campusName, holiday.HolidayDate, holiday.Reason, false);
    }

    /// <summary>Xóa ngày nghỉ KHÔNG khôi phục tiết đã bị hủy trước đó; muốn dạy lại thì thêm tiết bù (BR-15).</summary>
    public async Task DeleteHolidayAsync(int id)
    {
        var holiday = await db.Holiday.FindAsync(id) ?? throw ApiException.NotFound("Không tìm thấy ngày nghỉ.");
        db.Holiday.Remove(holiday);
        await db.SaveChangesAsync();
    }

    private static AcademicYearDto ToDto(AcademicYear y) => new(
        y.AcademicYearId, y.Name, y.StartDate, y.EndDate, y.IsCurrent,
        y.Semester.OrderBy(s => s.SemesterNo)
            .Select(s => new SemesterDto(s.SemesterId, s.SemesterNo, s.Name, s.StartDate, s.EndDate)).ToList());
}
