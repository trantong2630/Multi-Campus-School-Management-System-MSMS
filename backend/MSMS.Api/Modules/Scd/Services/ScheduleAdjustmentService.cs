using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Scd.Dtos;

namespace MSMS.Api.Modules.Scd.Services;

/// <summary>
/// SCD-02: điều chỉnh lịch dạy (BR-15). Hiệu phó phân hiệu thao tác trên tiết chưa bị khóa: đổi giáo viên, hủy, thêm tiết bù.
/// Mọi thao tác đặt nguồn tiết thành Chỉnh tay, kiểm xung đột toàn trường (BR-13), thông báo giáo viên liên quan và học sinh của lớp, ghi Audit.
/// Chỉ đổi lịch dạy, không đổi phụ trách chuyên môn (BR-14).
/// </summary>
public sealed class ScheduleAdjustmentService(
    MsmsDbContext db, CurrentUser user, ScopeResolver scope, IClock clock, AuditWriter audit, ScheduleSupport support)
{
    // ---------------------------------------------------------------- Xem lịch tuần để điều chỉnh
    /// <summary>Lưới tuần của một lớp (Thứ 2–7) kèm cờ khóa của từng tiết và các ngày nghỉ trong tuần. Principal xem được nhưng không thao tác.</summary>
    public async Task<AdjustWeekDto> WeekAsync(int classId, DateOnly weekStart)
    {
        var cls = await db.Class.AsNoTracking().FirstOrDefaultAsync(c => c.ClassId == classId) ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        await scope.EnsureCanReadCampusAsync(cls.CampusId);
        var from = weekStart.AddDays(-(((int)weekStart.DayOfWeek + 6) % 7));
        var to = from.AddDays(5);
        var today = clock.Today;

        var sessions = await db.TeachingSession.AsNoTracking()
            .Where(s => s.ClassId == classId && s.SessionDate >= from && s.SessionDate <= to)
            .OrderBy(s => s.SessionDate).ThenBy(s => s.PeriodSlot.StartTime).ThenBy(s => s.Status == "Cancelled" ? 1 : 0)
            .Select(s => new AdjustSessionDto(
                s.SessionId, s.SessionDate, s.PeriodSlotId, s.SubjectId, s.Subject.Name, s.Subject.Abbreviation,
                s.TeacherId, s.Teacher == null ? null : s.Teacher.Staff.FullName, s.Teacher == null ? null : s.Teacher.TeacherAbbr,
                s.Status, s.Source, s.SessionDate < today || s.AttendanceRecord.Any(), s.VersionId == null, s.ChangeNote))
            .ToListAsync();
        var holidays = await db.Holiday.AsNoTracking()
            .Where(h => h.AcademicYearId == cls.AcademicYearId && (h.CampusId == null || h.CampusId == cls.CampusId)
                        && h.HolidayDate >= from && h.HolidayDate <= to)
            .Select(h => new AdjustHolidayDto(h.HolidayDate, h.Reason)).ToListAsync();

        return new AdjustWeekDto(cls.ClassId, cls.Name, cls.CampusId, from, to, await scope.CanManageCampusAsync(cls.CampusId), sessions, holidays);
    }

    // ---------------------------------------------------------------- Chọn giáo viên toàn trường
    /// <summary>Giáo viên toàn trường kèm trạng thái rảnh/bận tại (ngày, khung giờ) — để chọn giáo viên cho tiết hoặc tiết bù (BR-13).</summary>
    public async Task<List<TeacherAvailabilityDto>> AvailableTeachersAsync(DateOnly date, int periodSlotId, long? excludeSessionId)
    {
        var slot = await db.PeriodSlot.AsNoTracking().FirstOrDefaultAsync(p => p.PeriodSlotId == periodSlotId)
                   ?? throw ApiException.BadRequest("Khung giờ tiết không tồn tại.");
        return await support.AvailabilityAsync(date, slot, excludeSessionId ?? 0);
    }

    // ---------------------------------------------------------------- Đổi giáo viên
    public async Task<AdjustResultDto> ChangeTeacherAsync(long sessionId, ChangeSessionTeacherRequest r)
    {
        var s = await support.LoadManagedSessionAsync(sessionId);
        if (s.Status == "Pending")
            throw ApiException.BadRequest("Tiết đang Chờ xử lý. Chọn giáo viên dạy thay hoặc hủy tiết.");
        if (s.Status == "Cancelled") throw ApiException.BadRequest("Tiết đã hủy nên không đổi giáo viên.");
        await support.EnsureNotLockedAsync(s);
        if (s.TeacherId == r.TeacherId) throw ApiException.BadRequest("Giáo viên này đang dạy tiết này. Chọn giáo viên khác.");

        var teacher = await support.EnsureEligibleTeacherAsync(r.TeacherId);
        var name = teacher.Staff.FullName;
        await support.EnsureTeacherFreeAsync(teacher.StaffId, name, s.SessionDate, s.PeriodSlotId, s.SessionId);
        await support.EnsureTeacherNotAbsentAsync(teacher.StaffId, name, s.SessionDate, s.PeriodSlot);

        var oldTeacherId = s.TeacherId;
        var oldName = s.Teacher?.Staff.FullName ?? "chưa có giáo viên";
        var what = ScheduleSupport.Describe(s.Subject.Name, s.Class.Name, s.SessionDate, s.PeriodSlot);
        s.TeacherId = teacher.StaffId;
        support.MarkManual(s, $"Đổi giáo viên: {oldName} → {name}");

        audit.AddLong(AuditCategories.ScheduleAdjust, "ChangeSessionTeacher", [s.SessionId],
            new { sessionId = s.SessionId, classId = s.ClassId, date = s.SessionDate, fromTeacherId = oldTeacherId, toTeacherId = teacher.StaffId });
        var notified = await support.NotifyAsync([s.ClassId], s.SessionDate, [oldTeacherId, teacher.StaffId],
            "Tiết đổi giáo viên", $"{what}: {oldName} → {name}.", "TeachingSession", s.SessionId);
        await db.SaveChangesAsync();
        return new AdjustResultDto(s.SessionId, notified);
    }

    // ---------------------------------------------------------------- Hủy tiết
    /// <summary>Tiết Đã hủy không dạy, không điểm danh, không tính chuyên cần (BR-15). Giáo viên được giữ lại trên tiết để truy vết.</summary>
    public async Task<AdjustResultDto> CancelAsync(long sessionId)
    {
        var s = await support.LoadManagedSessionAsync(sessionId);
        if (s.Status == "Pending") throw ApiException.BadRequest("Tiết đang Chờ xử lý. Chọn giáo viên dạy thay hoặc hủy tiết.");
        if (s.Status == "Cancelled") throw ApiException.BadRequest("Tiết này đã hủy rồi.");
        await support.EnsureNotLockedAsync(s);

        s.Status = "Cancelled";
        support.MarkManual(s, "Hủy tiết");
        var what = ScheduleSupport.Describe(s.Subject.Name, s.Class.Name, s.SessionDate, s.PeriodSlot);
        audit.AddLong(AuditCategories.ScheduleAdjust, "CancelSession", [s.SessionId],
            new { sessionId = s.SessionId, classId = s.ClassId, date = s.SessionDate, teacherId = s.TeacherId });
        var notified = await support.NotifyAsync([s.ClassId], s.SessionDate, [s.TeacherId],
            "Tiết đã hủy", $"{what} đã hủy.", "TeachingSession", s.SessionId);
        await db.SaveChangesAsync();
        return new AdjustResultDto(s.SessionId, notified);
    }

    // ---------------------------------------------------------------- Tiết bù
    /// <summary>
    /// Thêm tiết bù = tạo một tiết mới. Ngày ≥ hôm nay, là ngày học của phân hiệu (không Chủ nhật, không ngày nghỉ), trong một học kỳ của năm học;
    /// (ngày, khung giờ) trống với cả lớp và giáo viên (BR-13).
    /// </summary>
    public async Task<AdjustResultDto> AddMakeUpAsync(MakeUpRequest r)
    {
        var cls = await db.Class.Include(c => c.Campus).FirstOrDefaultAsync(c => c.ClassId == r.ClassId)
                  ?? throw ApiException.BadRequest("Không tìm thấy lớp.");
        await scope.EnsureCanManageCampusAsync(cls.CampusId);
        if (!cls.IsActive) throw ApiException.BadRequest("Lớp đã ngừng hoạt động nên không thêm tiết bù được.");
        if (r.Date < clock.Today) throw ApiException.BadRequest("Ngày tiết bù phải từ hôm nay trở đi.");
        if (!SchoolCalendarRules.IsTeachingWeekday(r.Date)) throw ApiException.BadRequest("Chủ nhật không có tiết. Chọn ngày học của phân hiệu.");
        if (!await db.Semester.AnyAsync(s => s.AcademicYearId == cls.AcademicYearId && s.StartDate <= r.Date && s.EndDate >= r.Date))
            throw ApiException.BadRequest("Ngày này nằm ngoài học kỳ của năm học. Tiết bù phải nằm trong học kỳ.");
        if (await db.Holiday.AnyAsync(h => h.AcademicYearId == cls.AcademicYearId && h.HolidayDate == r.Date && (h.CampusId == null || h.CampusId == cls.CampusId)))
            throw ApiException.BadRequest("Ngày này là ngày nghỉ của phân hiệu nên không có tiết. Chọn ngày học khác.");

        var subject = await db.Subject.FirstOrDefaultAsync(s => s.SubjectId == r.SubjectId && s.IsActive)
                      ?? throw ApiException.BadRequest("Môn không tồn tại hoặc đã ngừng dùng.");
        var slot = await db.PeriodSlot.FirstOrDefaultAsync(p => p.PeriodSlotId == r.PeriodSlotId && p.IsActive)
                   ?? throw ApiException.BadRequest("Khung giờ tiết không tồn tại hoặc đã ngừng dùng.");
        await support.EnsureClassFreeAsync(r.ClassId, r.Date, r.PeriodSlotId);
        var teacher = await support.EnsureEligibleTeacherAsync(r.TeacherId);
        var name = teacher.Staff.FullName;
        await support.EnsureTeacherFreeAsync(teacher.StaffId, name, r.Date, r.PeriodSlotId);
        await support.EnsureTeacherNotAbsentAsync(teacher.StaffId, name, r.Date, slot);

        var makeUp = new TeachingSession
        {
            ClassId = r.ClassId, SubjectId = r.SubjectId, TeacherId = r.TeacherId, SessionDate = r.Date, PeriodSlotId = r.PeriodSlotId,
            Status = "Scheduled", Source = "Manual", VersionId = null, ChangeNote = "Tiết bù",
            CreatedByPersonId = user.PersonId, CreatedAt = clock.Now,
        };
        db.TeachingSession.Add(makeUp);

        await using var tx = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();   // cần mã tiết mới để audit và thông báo trỏ tới
        audit.AddLong(AuditCategories.ScheduleAdjust, "AddMakeUpSession", [makeUp.SessionId],
            new { classId = r.ClassId, date = r.Date, periodSlotId = r.PeriodSlotId, subjectId = r.SubjectId, teacherId = r.TeacherId });
        var notified = await support.NotifyAsync([r.ClassId], r.Date, [r.TeacherId], "Có tiết bù mới",
            $"Tiết bù: {ScheduleSupport.Describe(subject.Name, cls.Name, r.Date, slot)}, giáo viên {name}.", "TeachingSession", makeUp.SessionId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new AdjustResultDto(makeUp.SessionId, notified);
    }
}
