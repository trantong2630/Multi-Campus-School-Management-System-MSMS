using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Modules.Sch.Services;
using MSMS.Api.Modules.Scd.Dtos;

namespace MSMS.Api.Modules.Scd.Services;

/// <summary>Phần đọc của SCD-01: danh sách phiên bản, lưới phiên bản, lịch dạy theo ngày, lịch dạy của giáo viên.</summary>
public sealed class ScheduleQueryService(MsmsDbContext db, CurrentUser user, ScopeResolver scope)
{
    /// <summary>Phân hiệu người dùng được thao tác: School Admin và Principal thấy tất cả, Vice Principal chỉ phân hiệu đang phụ trách.</summary>
    public async Task<List<CampusOptionDto>> MyCampusesAsync()
    {
        var query = db.Campus.AsNoTracking().Where(c => c.IsActive);
        if (user.AccountType == AccountTypes.VicePrincipal)
        {
            var led = await scope.LedCampusIdsAsync();
            query = query.Where(c => led.Contains(c.CampusId));
        }
        return await query.OrderBy(c => c.Name).Select(c => new CampusOptionDto(c.CampusId, c.Name)).ToListAsync();
    }

    public async Task<List<TimetableVersionDto>> ListVersionsAsync(int campusId, int semesterId)
    {
        await scope.EnsureCanReadCampusAsync(campusId);
        return await db.TimetableVersion.AsNoTracking()
            .Where(v => v.CampusId == campusId && v.SemesterId == semesterId)
            .OrderByDescending(v => v.VersionId)
            .Select(v => new TimetableVersionDto(v.VersionId, v.CampusId, v.Campus.Name, v.SemesterId, v.Semester.Name,
                v.Semester.AcademicYear.Name, v.Status, v.SourceFileName, v.ImportedAt,
                v.ImportedByPerson == null ? null : v.ImportedByPerson.FullName, v.PublishedAt,
                v.DeletedSessionCount, v.CreatedSessionCount, v.TimetableEntry.Count))
            .ToListAsync();
    }

    public async Task<VersionGridDto> GetVersionGridAsync(int versionId)
    {
        var version = await db.TimetableVersion.AsNoTracking().Include(v => v.Campus).Include(v => v.Semester)
            .FirstOrDefaultAsync(v => v.VersionId == versionId) ?? throw ApiException.NotFound("Không tìm thấy phiên bản thời khóa biểu.");
        await scope.EnsureCanReadCampusAsync(version.CampusId);

        var entries = await db.TimetableEntry.AsNoTracking().Where(e => e.VersionId == versionId)
            .Select(e => new
            {
                e.ClassId, ClassName = e.Class.Name, e.Class.GradeLevel, e.DayOfWeek, e.PeriodSlotId,
                e.PeriodSlot.SessionOfDay, e.PeriodSlot.PeriodNo,
                SubjectAbbr = e.Subject.Abbreviation, SubjectName = e.Subject.Name,
                TeacherAbbr = e.Teacher.TeacherAbbr, TeacherName = e.Teacher.Staff.FullName,
            }).ToListAsync();

        var classes = await db.Class.AsNoTracking()
            .Where(c => c.CampusId == version.CampusId && c.AcademicYearId == version.Semester.AcademicYearId && c.IsActive)
            .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).Select(c => new GridClassDto(c.ClassId, c.Name)).ToListAsync();
        var slots = await db.PeriodSlot.AsNoTracking().Where(p => p.IsActive)
            .OrderBy(p => p.SessionOfDay == SessionOfDay.Morning ? 0 : 1).ThenBy(p => p.PeriodNo).ToListAsync();

        var rows = new List<GridRowDto>();
        for (var day = 2; day <= 7; day++)
        {
            foreach (var slot in slots)
            {
                var cells = classes.Select(c =>
                {
                    var e = entries.FirstOrDefault(x => x.ClassId == c.ClassId && x.DayOfWeek == day && x.PeriodSlotId == slot.PeriodSlotId);
                    return e is null ? null : new GridCellDto(e.SubjectAbbr, e.SubjectName, e.TeacherAbbr ?? "", e.TeacherName);
                }).ToList();
                rows.Add(new GridRowDto(day, slot.SessionOfDay, slot.PeriodNo, cells));
            }
        }
        return new VersionGridDto(versionId, version.Status, version.Campus.Name, version.Semester.Name, classes, rows);
    }

    /// <summary>Lịch dạy một ngày của phân hiệu — đọc từ lịch dạy (TeachingSession), nguồn sự thật của thời khóa biểu hiện hành (BR-14).</summary>
    public async Task<DayScheduleDto> GetDayAsync(int campusId, DateOnly date)
    {
        await scope.EnsureCanReadCampusAsync(campusId);
        var year = await db.AcademicYear.AsNoTracking().Where(y => y.StartDate <= date && y.EndDate >= date).FirstOrDefaultAsync()
                   ?? await db.AcademicYear.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);

        var classes = year is null ? [] : await db.Class.AsNoTracking()
            .Where(c => c.CampusId == campusId && c.AcademicYearId == year.AcademicYearId && c.IsActive)
            .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).Select(c => new GridClassDto(c.ClassId, c.Name)).ToListAsync();
        var slots = await db.PeriodSlot.AsNoTracking().Where(p => p.IsActive)
            .OrderBy(p => p.SessionOfDay == SessionOfDay.Morning ? 0 : 1).ThenBy(p => p.PeriodNo).ToListAsync();

        var holiday = year is null ? null : await db.Holiday.AsNoTracking()
            .Where(h => h.AcademicYearId == year.AcademicYearId && h.HolidayDate == date && (h.CampusId == null || h.CampusId == campusId))
            .Select(h => h.Reason ?? "Ngày nghỉ").FirstOrDefaultAsync();

        var sessions = await db.TeachingSession.AsNoTracking()
            .Where(s => s.Class.CampusId == campusId && s.SessionDate == date)
            .Select(s => new
            {
                s.SessionId, s.ClassId, s.PeriodSlotId, s.Status, s.Source,
                SubjectName = s.Subject.Name, SubjectAbbr = s.Subject.Abbreviation,
                TeacherName = s.Teacher == null ? null : s.Teacher.Staff.FullName, TeacherAbbr = s.Teacher == null ? null : s.Teacher.TeacherAbbr,
            }).ToListAsync();

        var rows = slots.Select(slot => new DaySlotRow(slot.PeriodSlotId, slot.SessionOfDay, slot.PeriodNo, slot.StartTime, slot.EndTime,
            classes.Select(c =>
            {
                // Một khung có thể có tiết Đã hủy kèm tiết khác: ưu tiên tiết còn hiệu lực.
                var s = sessions.Where(x => x.ClassId == c.ClassId && x.PeriodSlotId == slot.PeriodSlotId)
                    .OrderBy(x => x.Status == "Cancelled" ? 1 : 0).FirstOrDefault();
                return s is null ? null : new SessionCellDto(s.SessionId, s.SubjectName, s.SubjectAbbr, s.TeacherName, s.TeacherAbbr, s.Status, s.Source);
            }).ToList())).ToList();

        return new DayScheduleDto(date, holiday, holiday is not null, classes, rows);
    }

    /// <summary>Giáo viên xem lịch dạy của mình: gồm tiết bù và tiết dạy thay; tiết Chờ xử lý và Đã hủy hiện rõ trạng thái.</summary>
    public async Task<List<MySessionDto>> GetMineAsync(DateOnly from, DateOnly to)
    {
        if (to < from) throw ApiException.BadRequest("Ngày kết thúc phải sau ngày bắt đầu.");
        if (to.DayNumber - from.DayNumber > 62) throw ApiException.BadRequest("Chỉ xem tối đa 62 ngày mỗi lần. Chọn khoảng ngắn hơn.");
        return await db.TeachingSession.AsNoTracking()
            .Where(s => s.TeacherId == user.PersonId && s.SessionDate >= from && s.SessionDate <= to)
            .OrderBy(s => s.SessionDate).ThenBy(s => s.PeriodSlot.StartTime)
            .Select(s => new MySessionDto(s.SessionId, s.SessionDate, s.PeriodSlotId, s.PeriodSlot.SessionOfDay, s.PeriodSlot.PeriodNo,
                s.PeriodSlot.StartTime, s.PeriodSlot.EndTime, s.Class.Name, s.Class.Campus.Name, s.Subject.Name, s.Subject.Abbreviation, s.Status, s.Source))
            .ToListAsync();
    }

    /// <summary>Lớp đang hoạt động của phân hiệu trong năm học hiện hành, dùng cho bộ chọn lớp ở màn xem lịch.</summary>
    public async Task<List<GridClassDto>> ListClassesAsync(int campusId)
    {
        await scope.EnsureCanReadCampusAsync(campusId);
        var year = await db.AcademicYear.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent);
        if (year is null) return [];
        return await db.Class.AsNoTracking()
            .Where(c => c.CampusId == campusId && c.AcademicYearId == year.AcademicYearId && c.IsActive)
            .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).Select(c => new GridClassDto(c.ClassId, c.Name)).ToListAsync();
    }

    /// <summary>Giáo viên đang hoạt động có phân hiệu chính là phân hiệu này.</summary>
    public async Task<List<TeacherOptionDto>> ListTeachersAsync(int campusId)
    {
        await scope.EnsureCanReadCampusAsync(campusId);
        return await db.StaffProfile.AsNoTracking()
            .Where(t => t.PrimaryCampusId == campusId && t.IsActive && t.TeacherAbbr != null)
            .OrderBy(t => t.Staff.FullName).Select(t => new TeacherOptionDto(t.StaffId, t.Staff.FullName, t.TeacherAbbr)).ToListAsync();
    }

    /// <summary>Lịch dạy của một lớp trong khoảng ngày.</summary>
    public async Task<List<ViewSessionDto>> GetClassSessionsAsync(int classId, DateOnly from, DateOnly to)
    {
        CheckRange(from, to);
        var campusId = await db.Class.AsNoTracking().Where(c => c.ClassId == classId).Select(c => (int?)c.CampusId).FirstOrDefaultAsync()
                       ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        await scope.EnsureCanReadCampusAsync(campusId);
        return await ProjectSessions(db.TeachingSession.AsNoTracking().Where(s => s.ClassId == classId && s.SessionDate >= from && s.SessionDate <= to));
    }

    /// <summary>Lịch dạy của một giáo viên trong khoảng ngày; chỉ gồm các tiết ở phân hiệu người gọi đọc được.</summary>
    public async Task<List<ViewSessionDto>> GetTeacherSessionsAsync(int teacherId, DateOnly from, DateOnly to)
    {
        CheckRange(from, to);
        var query = db.TeachingSession.AsNoTracking().Where(s => s.TeacherId == teacherId && s.SessionDate >= from && s.SessionDate <= to);
        if (user.AccountType == AccountTypes.VicePrincipal)
        {
            var led = await scope.LedCampusIdsAsync();
            query = query.Where(s => led.Contains(s.Class.CampusId));
        }
        return await ProjectSessions(query);
    }

    private static void CheckRange(DateOnly from, DateOnly to)
    {
        if (to < from) throw ApiException.BadRequest("Ngày kết thúc phải sau ngày bắt đầu.");
        if (to.DayNumber - from.DayNumber > 62) throw ApiException.BadRequest("Chỉ xem tối đa 62 ngày mỗi lần. Chọn khoảng ngắn hơn.");
    }

    private static Task<List<ViewSessionDto>> ProjectSessions(IQueryable<Data.Entities.TeachingSession> query) =>
        query.OrderBy(s => s.SessionDate).ThenBy(s => s.PeriodSlot.StartTime)
            .Select(s => new ViewSessionDto(s.SessionId, s.SessionDate, s.PeriodSlotId, s.PeriodSlot.SessionOfDay, s.PeriodSlot.PeriodNo,
                s.PeriodSlot.StartTime, s.PeriodSlot.EndTime, s.Class.Name, s.Class.Campus.Name, s.Subject.Name, s.Subject.Abbreviation,
                s.Teacher == null ? null : s.Teacher.Staff.FullName, s.Status, s.Source))
            .ToListAsync();
}
