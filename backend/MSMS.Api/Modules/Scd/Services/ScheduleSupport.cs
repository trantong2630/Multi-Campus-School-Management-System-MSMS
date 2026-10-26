using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Services;
using MSMS.Api.Modules.Scd.Dtos;

namespace MSMS.Api.Modules.Scd.Services;

/// <summary>
/// Quy tắc dùng chung cho SCD-02 (điều chỉnh lịch) và SCD-03 (giáo viên vắng): tiết bị khóa (mục 6.2), xung đột lịch toàn trường (BR-13),
/// điều kiện dạy (BR-06), khai báo vắng phủ ngày và buổi (BR-16), và thông báo (BR-26). Chỉ thêm vào context; người gọi SaveChanges.
/// </summary>
public sealed class ScheduleSupport(MsmsDbContext db, ScopeResolver scope, CurrentUser user, IClock clock, NotificationSender notifier)
{
    public DateOnly Today => clock.Today;

    /// <summary>Tải tiết kèm lớp, khung giờ, môn, giáo viên; người gọi phải là hiệu phó đang phụ trách phân hiệu của lớp.</summary>
    public async Task<TeachingSession> LoadManagedSessionAsync(long sessionId)
    {
        var session = await db.TeachingSession
            .Include(s => s.Class).ThenInclude(c => c.Campus)
            .Include(s => s.PeriodSlot).Include(s => s.Subject)
            .Include(s => s.Teacher!).ThenInclude(t => t.Staff)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId) ?? throw ApiException.NotFound("Không tìm thấy tiết.");
        await scope.EnsureCanManageCampusAsync(session.Class.CampusId);
        return session;
    }

    /// <summary>Tiết bị khóa (mục 6.2): ngày của tiết đã qua, hoặc đã có điểm danh. Tiết bị khóa không đổi giáo viên, không hủy.</summary>
    public async Task EnsureNotLockedAsync(TeachingSession s)
    {
        if (s.SessionDate < clock.Today)
            throw ApiException.BadRequest("Tiết đã qua ngày nên bị khóa: không đổi giáo viên, không hủy. School Admin sửa điểm danh của tiết đã qua.");
        if (await db.TeachingSession.AnyAsync(x => x.SessionId == s.SessionId && x.AttendanceRecord.Any()))
            throw ApiException.BadRequest("Tiết đã có điểm danh nên bị khóa: không đổi giáo viên, không hủy.");
    }

    public void MarkManual(TeachingSession s, string? note)
    {
        s.Source = "Manual";
        s.UpdatedByPersonId = user.PersonId;
        s.UpdatedAt = clock.Now;
        s.ChangeNote = note is { Length: > 255 } ? note[..255] : note;
    }

    /// <summary>Giáo viên đủ điều kiện dạy (BR-06): hồ sơ đang hoạt động, tài khoản loại Teacher không bị khóa.</summary>
    public async Task<StaffProfile> EnsureEligibleTeacherAsync(int teacherId)
    {
        var teacher = await db.StaffProfile.AsNoTracking().Include(t => t.Staff).ThenInclude(p => p.Account)
                          .FirstOrDefaultAsync(t => t.StaffId == teacherId)
                      ?? throw ApiException.BadRequest("Không tìm thấy giáo viên.");
        if (TimetableImportService.TeacherProblem(teacher) is { } problem)
            throw ApiException.BadRequest($"Giáo viên {teacher.Staff.FullName} không đủ điều kiện dạy: {problem}");
        return teacher;
    }

    /// <summary>BR-13: mỗi giáo viên tối đa một tiết Đã xếp lịch tại một (ngày, khung giờ), kiểm trên toàn trường.</summary>
    public async Task EnsureTeacherFreeAsync(int teacherId, string teacherName, DateOnly date, int slotId, long excludeSessionId = 0)
    {
        var busy = await db.TeachingSession.AsNoTracking()
            .Where(s => s.TeacherId == teacherId && s.SessionDate == date && s.PeriodSlotId == slotId
                        && s.Status == "Scheduled" && s.SessionId != excludeSessionId)
            .Select(s => new { Class = s.Class.Name, Campus = s.Class.Campus.Name }).FirstOrDefaultAsync();
        if (busy is not null)
            throw ApiException.BadRequest($"{teacherName} đã có tiết ở lớp {busy.Class} ({busy.Campus}) cùng ngày {date:dd/MM/yyyy} và cùng khung giờ. Chọn giáo viên khác.");
    }

    /// <summary>BR-16: giáo viên đang có khai báo vắng phủ ngày và tiết của tiết học thì không được xếp dạy.</summary>
    public async Task EnsureTeacherNotAbsentAsync(int teacherId, string teacherName, DateOnly date, PeriodSlot slot)
    {
        if (await db.TeacherAbsence.Where(a => a.TeacherId == teacherId).Covering(date, slot.PeriodSlotId).AnyAsync())
            throw ApiException.BadRequest($"{teacherName} có khai báo vắng phủ ngày {date:dd/MM/yyyy}, {SessionOfDay.Label(slot.SessionOfDay).ToLowerInvariant()} tiết {slot.PeriodNo}. Chọn giáo viên khác.");
    }

    /// <summary>BR-13: mỗi lớp tối đa một tiết Đã xếp lịch hoặc Chờ xử lý tại một (ngày, khung giờ).</summary>
    public async Task EnsureClassFreeAsync(int classId, DateOnly date, int slotId, long excludeSessionId = 0)
    {
        if (await db.TeachingSession.AnyAsync(s => s.ClassId == classId && s.SessionDate == date && s.PeriodSlotId == slotId
                                                   && s.Status != "Cancelled" && s.SessionId != excludeSessionId))
            throw ApiException.BadRequest($"Lớp đã có tiết cùng ngày {date:dd/MM/yyyy} và cùng khung giờ này. Chọn khung giờ trống.");
    }

    /// <summary>
    /// Mọi giáo viên đủ điều kiện dạy của TOÀN TRƯỜNG kèm trạng thái tại (ngày, khung giờ) — hiệu phó chọn giáo viên cho tiết
    /// hoặc dạy thay nhìn thấy cả giáo viên phân hiệu khác (mục 2.3, BR-13). Sắp theo tên.
    /// </summary>
    public async Task<List<TeacherAvailabilityDto>> AvailabilityAsync(DateOnly date, PeriodSlot slot, long excludeSessionId = 0)
    {
        var teachers = await db.StaffProfile.AsNoTracking()
            .Where(t => t.IsActive && t.Staff.Account != null && t.Staff.Account.AccountType == AccountTypes.Teacher
                        && t.Staff.Account.Status != AccountStatuses.Locked)
            .OrderBy(t => t.Staff.FullName).ThenBy(t => t.StaffId)
            .Select(t => new { t.StaffId, Name = t.Staff.FullName, t.TeacherAbbr, Campus = t.PrimaryCampus.Name }).ToListAsync();

        var busy = (await db.TeachingSession.AsNoTracking()
                .Where(s => s.TeacherId != null && s.SessionDate == date && s.PeriodSlotId == slot.PeriodSlotId
                            && s.Status == "Scheduled" && s.SessionId != excludeSessionId)
                .Select(s => new { TeacherId = s.TeacherId!.Value, Class = s.Class.Name, Campus = s.Class.Campus.Name }).ToListAsync())
            .GroupBy(x => x.TeacherId).ToDictionary(g => g.Key, g => $"{g.First().Class} · {g.First().Campus}");
        var absent = (await db.TeacherAbsence.AsNoTracking().Covering(date, slot.PeriodSlotId)
                .Select(a => a.TeacherId).ToListAsync()).ToHashSet();

        return teachers.Select(t => new TeacherAvailabilityDto(
            t.StaffId, t.Name, t.TeacherAbbr, t.Campus, busy.ContainsKey(t.StaffId), busy.GetValueOrDefault(t.StaffId), absent.Contains(t.StaffId))).ToList();
    }

    public Task<List<int>> ViceIdsAsync(int campusId)
    {
        var today = clock.Today;
        return db.CampusLeadership.AsNoTracking().Where(EffectivePeriod.EffectiveAt<CampusLeadership>(today))
            .Where(l => l.CampusId == campusId).Select(l => l.StaffId).ToListAsync();
    }

    /// <summary>Học sinh có ghi danh hiệu lực của các lớp tại một ngày (mục 6.2); ngày đã qua thì tính theo hôm nay.</summary>
    private async Task<List<int>> StudentIdsAsync(IReadOnlyCollection<int> classIds, DateOnly date)
    {
        var on = date < clock.Today ? clock.Today : date;
        return await db.Enrollment.AsNoTracking()
            .Where(e => classIds.Contains(e.ClassId) && e.StartDate <= on && (e.EndDate == null || e.EndDate >= on))
            .Select(e => e.StudentId).ToListAsync();
    }

    /// <summary>
    /// BR-15, BR-26: mỗi người nhận một thông báo — giáo viên liên quan và học sinh của các lớp.
    /// Trả về số người nhận.
    /// </summary>
    public async Task<int> NotifyAsync(IEnumerable<int> classIds, DateOnly refDate, IEnumerable<int?> teacherIds,
        string title, string body, string refType, long refId)
    {
        var classes = classIds.Distinct().ToList();
        var recipients = (await StudentIdsAsync(classes, refDate)).Concat(teacherIds.Where(t => t.HasValue).Select(t => t!.Value)).Distinct().ToList();
        notifier.Add(recipients, NotificationTypes.ScheduleChanged, title, body, refType, refId);
        return recipients.Count;
    }

    public static string WeekdayLabel(DateOnly date) =>
        date.DayOfWeek == DayOfWeek.Sunday ? "CN" : $"Thứ {SchoolCalendarRules.ToVnDay(date.DayOfWeek)}";

    /// <summary>Mô tả tiết ngắn gọn cho thông báo và ghi chú: môn, lớp, ngày, khung giờ.</summary>
    public static string Describe(string subjectName, string className, DateOnly date, PeriodSlot slot) =>
        $"{subjectName} lớp {className}, {WeekdayLabel(date)} {date:dd/MM/yyyy}, {SessionOfDay.Label(slot.SessionOfDay).ToLowerInvariant()} tiết {slot.PeriodNo}";
}

/// <summary>Khai báo vắng phủ một (ngày, tiết): ngày nằm trong khoảng, và danh sách tiết trống (cả ngày) hoặc có chứa tiết đó.</summary>
public static class AbsenceQueries
{
    public static IQueryable<TeacherAbsence> Covering(this IQueryable<TeacherAbsence> q, DateOnly date, int slotId) =>
        q.Where(a => a.FromDate <= date && a.ToDate >= date && (a.SlotIds == null || ("," + a.SlotIds + ",").Contains("," + slotId + ",")));

    public static List<int> ParseSlots(string? slotIds) =>
        string.IsNullOrEmpty(slotIds) ? [] : slotIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

    public static string? JoinSlots(IEnumerable<int> slotIds) =>
        slotIds.Distinct().OrderBy(i => i).ToList() is { Count: > 0 } list ? string.Join(',', list) : null;
}
