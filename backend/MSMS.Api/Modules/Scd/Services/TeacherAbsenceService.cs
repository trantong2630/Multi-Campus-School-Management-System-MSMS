using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Scd.Dtos;

namespace MSMS.Api.Modules.Scd.Services;

/// <summary>
/// SCD-03: giáo viên vắng (BR-16). Khai báo KHÔNG tự đổi lịch: các tiết Đã xếp lịch chưa bị khóa của giáo viên trong khoảng, ở phân hiệu
/// của người khai, chuyển sang Chờ xử lý (Chỉnh tay) và để trống giáo viên; hiệu phó quyết định từng tiết: dạy thay hoặc hủy.
/// Giáo viên dạy nhiều phân hiệu thì hiệu phó mỗi phân hiệu khai phần của mình. Đây không phải đơn xin nghỉ của cán bộ.
/// </summary>
public sealed class TeacherAbsenceService(
    MsmsDbContext db, CurrentUser user, ScopeResolver scope, IClock clock, AuditWriter audit, ScheduleSupport support)
{
    private const int MaxDays = 90;

    // ---------------------------------------------------------------- Khai báo
    /// <summary>Giáo viên còn tiết Đã xếp lịch từ hôm nay ở phân hiệu này — danh sách chọn khi khai báo.</summary>
    public async Task<List<TeacherOptionDto>> TeachersAsync(int campusId)
    {
        await scope.EnsureCanManageCampusAsync(campusId);
        var today = clock.Today;
        return await db.TeachingSession.AsNoTracking()
            .Where(s => s.TeacherId != null && s.Class.CampusId == campusId && s.Status == "Scheduled" && s.SessionDate >= today)
            .Select(s => new { Id = s.TeacherId!.Value, Name = s.Teacher!.Staff.FullName, Abbr = s.Teacher.TeacherAbbr }).Distinct()
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .Select(t => new TeacherOptionDto(t.Id, t.Name, t.Abbr)).ToListAsync();
    }

    /// <summary>
    /// Khai theo ngày (khoảng ngày, cả ngày) hoặc theo tiết (một ngày, các tiết đã chọn — danh sách tiết khác rỗng).
    /// Chỉ tác động tiết ở phân hiệu hiệu phó đang phụ trách; hiệu phó phân hiệu khác tự khai phần của mình.
    /// </summary>
    public async Task<DeclareAbsenceResultDto> DeclareAsync(DeclareAbsenceRequest r)
    {
        if (user.AccountType != AccountTypes.VicePrincipal) throw ApiException.Forbidden("Chỉ hiệu phó khai báo giáo viên vắng.");
        var teacher = await db.StaffProfile.AsNoTracking().Include(t => t.Staff).ThenInclude(p => p.Account)
                          .FirstOrDefaultAsync(t => t.StaffId == r.TeacherId && t.Staff.Account != null && t.Staff.Account.AccountType == AccountTypes.Teacher)
                      ?? throw ApiException.BadRequest("Chọn một giáo viên.");
        var name = teacher.Staff.FullName;
        var reason = Guard.Optional(r.Reason, "Lý do", 255);

        var slotIds = (r.SlotIds ?? []).Distinct().ToList();
        if (r.ToDate < r.FromDate) throw ApiException.BadRequest("Ngày kết thúc phải từ ngày bắt đầu trở đi.");
        if (r.ToDate < clock.Today) throw ApiException.BadRequest("Chỉ khai vắng từ hôm nay trở đi: tiết đã qua bị khóa, không đổi được.");
        if (r.ToDate.DayNumber - r.FromDate.DayNumber + 1 > MaxDays) throw ApiException.BadRequest($"Một khai báo tối đa {MaxDays} ngày. Chia thành nhiều khai báo.");
        if (slotIds.Count > 0)
        {
            if (r.FromDate != r.ToDate) throw ApiException.BadRequest("Khai theo tiết chỉ chọn một ngày. Vắng nhiều ngày thì khai theo ngày.");
            if (await db.PeriodSlot.CountAsync(p => slotIds.Contains(p.PeriodSlotId) && p.IsActive) != slotIds.Count)
                throw ApiException.BadRequest("Có tiết không tồn tại hoặc đã ngừng dùng.");
        }

        var start = r.FromDate < clock.Today ? clock.Today : r.FromDate;   // tiết đã qua là tiết bị khóa
        var led = await scope.LedCampusIdsAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var sessions = await db.TeachingSession.Include(s => s.Class)
            .Where(s => s.TeacherId == r.TeacherId && s.Status == "Scheduled" && s.SessionDate >= start && s.SessionDate <= r.ToDate
                        && led.Contains(s.Class.CampusId) && !s.AttendanceRecord.Any()
                        && (slotIds.Count == 0 || slotIds.Contains(s.PeriodSlotId)))
            .ToListAsync();
        if (sessions.Count == 0)
            throw ApiException.BadRequest($"{name} không có tiết nào cần xử lý ở phân hiệu bạn phụ trách trong phạm vi này. Chỉ khai được khi giáo viên có tiết ở phân hiệu của bạn.");

        var absence = new TeacherAbsence
        {
            TeacherId = r.TeacherId, FromDate = r.FromDate, ToDate = r.ToDate, SlotIds = AbsenceQueries.JoinSlots(slotIds), Reason = reason,
            DeclaredByPersonId = user.PersonId, CreatedAt = clock.Now,
        };
        db.TeacherAbsence.Add(absence);
        await db.SaveChangesAsync();

        foreach (var s in sessions)
        {
            s.Status = "Pending";
            s.TeacherId = null;
            s.TeacherAbsenceId = absence.AbsenceId;
            support.MarkManual(s, $"Giáo viên {name} vắng {Range(r.FromDate, r.ToDate)}");
        }
        audit.Add(AuditCategories.ScheduleAdjust, "DeclareTeacherAbsence", [absence.AbsenceId],
            new { teacherId = r.TeacherId, from = r.FromDate, to = r.ToDate, slotIds, pending = sessions.Count, sessionIds = sessions.Select(s => s.SessionId) });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new DeclareAbsenceResultDto(absence.AbsenceId, sessions.Count);
    }

    // ---------------------------------------------------------------- Danh sách và xóa khai báo
    public async Task<List<AbsenceDto>> ListAsync(int campusId)
    {
        await scope.EnsureCanManageCampusAsync(campusId);
        var rows = await db.TeacherAbsence.AsNoTracking()
            .Where(a => a.DeclaredByPersonId == user.PersonId || a.TeachingSession.Any(s => s.Class.CampusId == campusId))
            .OrderByDescending(a => a.FromDate).ThenByDescending(a => a.AbsenceId).Take(200)
            .Select(a => new
            {
                a.AbsenceId, a.TeacherId, TeacherName = a.Teacher.Staff.FullName, a.Teacher.TeacherAbbr, a.FromDate, a.ToDate, a.SlotIds, a.Reason,
                DeclaredBy = a.DeclaredByPerson == null ? null : a.DeclaredByPerson.FullName, a.CreatedAt,
                Pending = a.TeachingSession.Count(s => s.Class.CampusId == campusId && s.Status == "Pending"),
                Handled = a.TeachingSession.Count(s => s.Class.CampusId == campusId && s.Status != "Pending"),
            }).ToListAsync();
        return rows.Select(a => new AbsenceDto(
            a.AbsenceId, a.TeacherId, a.TeacherName, a.TeacherAbbr, a.FromDate, a.ToDate, AbsenceQueries.ParseSlots(a.SlotIds), a.Reason,
            a.DeclaredBy, a.CreatedAt, a.Pending, a.Handled, a.Handled == 0)).ToList();
    }

    /// <summary>
    /// Xóa khai báo theo kiểu tất cả hoặc không: chỉ xóa khi chưa có tiết nào của khai báo bị xử lý (dạy thay, hủy) và giáo viên
    /// vẫn đủ điều kiện dạy (BR-06), không trùng lịch ở tiết nào (BR-13). Khi đó mọi tiết trở về giáo viên, trạng thái Đã xếp lịch.
    /// </summary>
    public async Task<DeleteAbsenceResultDto> DeleteAsync(int absenceId)
    {
        var absence = await db.TeacherAbsence.Include(a => a.Teacher).ThenInclude(t => t.Staff).ThenInclude(p => p.Account)
                          .FirstOrDefaultAsync(a => a.AbsenceId == absenceId)
                      ?? throw ApiException.NotFound("Không tìm thấy khai báo vắng.");
        var linked = await db.TeachingSession.Include(s => s.Class)
            .Where(s => s.TeacherAbsenceId == absenceId).ToListAsync();
        var restorable = linked.Where(s => s.SessionDate >= clock.Today).ToList();   // tiết đã qua bị khóa: giữ nguyên

        var led = await scope.LedCampusIdsAsync();
        if (user.AccountType != AccountTypes.VicePrincipal
            || (absence.DeclaredByPersonId != user.PersonId && !linked.Any(s => led.Contains(s.Class.CampusId))))
            throw ApiException.Forbidden("Chỉ hiệu phó khai báo, hoặc hiệu phó có tiết thuộc khai báo này, mới xóa được.");

        var handled = linked.Count(s => s.Status != "Pending");
        if (handled > 0)
            throw ApiException.BadRequest($"Đã xử lý {handled} tiết của khai báo này (dạy thay hoặc hủy) nên không xóa được. Xử lý nốt các tiết còn lại ở lưới Điều chỉnh.");

        var teacher = absence.Teacher;
        var teacherName = teacher.Staff.FullName;
        if (restorable.Count > 0 && TimetableImportService.TeacherProblem(teacher) is { } problem)
            throw ApiException.BadRequest($"Không trả tiết về {teacherName} được: {problem} Dạy thay hoặc hủy từng tiết.");

        var clashes = new List<string>();
        foreach (var s in restorable.OrderBy(x => x.SessionDate))
        {
            var busy = await db.TeachingSession.AsNoTracking()
                .Where(x => x.TeacherId == absence.TeacherId && x.SessionDate == s.SessionDate && x.PeriodSlotId == s.PeriodSlotId
                            && x.Status == "Scheduled" && x.SessionId != s.SessionId)
                .Select(x => x.Class.Name).FirstOrDefaultAsync();
            if (busy is not null) clashes.Add($"{s.SessionDate:dd/MM/yyyy} lớp {s.Class.Name} (giáo viên đang dạy lớp {busy})");
        }
        if (clashes.Count > 0)
            throw ApiException.BadRequest($"Không xóa được vì {teacherName} đã có tiết khác trùng lịch: {string.Join("; ", clashes.Take(5))}{(clashes.Count > 5 ? $"; và {clashes.Count - 5} tiết nữa" : "")}. Dạy thay hoặc hủy các tiết đó rồi xóa lại.");

        foreach (var s in restorable)
        {
            s.Status = "Scheduled";
            s.TeacherId = absence.TeacherId;
            s.TeacherAbsenceId = null;
            support.MarkManual(s, $"Giáo viên {teacherName} trở lại: khai báo vắng đã xóa");
        }
        db.TeacherAbsence.Remove(absence);

        if (restorable.Count > 0)
        {
            await support.NotifyAsync(restorable.Select(s => s.ClassId), restorable.Min(s => s.SessionDate), [absence.TeacherId],
                "Tiết được trả lại giáo viên",
                $"{teacherName}: khai báo vắng {Range(absence.FromDate, absence.ToDate)} đã xóa, {restorable.Count} tiết trở lại lịch của giáo viên.",
                "TeacherAbsence", absenceId);
        }
        audit.Add(AuditCategories.ScheduleAdjust, "DeleteTeacherAbsence", [absenceId],
            new { teacherId = absence.TeacherId, from = absence.FromDate, to = absence.ToDate, restored = restorable.Select(s => s.SessionId) });
        await db.SaveChangesAsync();
        return new DeleteAbsenceResultDto(restorable.Count);
    }

    // ---------------------------------------------------------------- Tiết Chờ xử lý
    /// <summary>Tiết Chờ xử lý của phân hiệu từ hôm nay trở đi; tiết đã qua bị khóa nên không còn nằm trong danh sách.</summary>
    public async Task<List<PendingSessionDto>> ListPendingAsync(int campusId, DateOnly? from, DateOnly? to, int? classId)
    {
        await scope.EnsureCanManageCampusAsync(campusId);
        var today = clock.Today;
        var query = db.TeachingSession.AsNoTracking().Where(s => s.Class.CampusId == campusId && s.Status == "Pending" && s.SessionDate >= today);
        if (from is { } f) query = query.Where(s => s.SessionDate >= f);
        if (to is { } t) query = query.Where(s => s.SessionDate <= t);
        if (classId is { } c) query = query.Where(s => s.ClassId == c);
        return await query.OrderBy(s => s.SessionDate).ThenBy(s => s.PeriodSlot.StartTime).ThenBy(s => s.Class.Name).Take(500)
            .Select(s => new PendingSessionDto(
                s.SessionId, s.SessionDate, s.PeriodSlotId, s.PeriodSlot.SessionOfDay, s.PeriodSlot.PeriodNo, s.PeriodSlot.StartTime, s.PeriodSlot.EndTime,
                s.ClassId, s.Class.Name, s.SubjectId, s.Subject.Name, s.Subject.Abbreviation,
                s.TeacherAbsenceId, s.TeacherAbsence == null ? null : s.TeacherAbsence.Teacher.Staff.FullName, s.ChangeNote))
            .ToListAsync();
    }

    /// <summary>Gợi ý giáo viên rảnh: đủ điều kiện dạy, không có tiết Đã xếp lịch cùng (ngày, khung giờ), không có khai báo vắng phủ ngày và tiết đó. Sắp theo tên, tối đa 10, không xếp hạng.</summary>
    public async Task<List<TeacherAvailabilityDto>> SuggestionsAsync(long sessionId)
    {
        var s = await support.LoadManagedSessionAsync(sessionId);
        EnsurePending(s);
        await support.EnsureNotLockedAsync(s);
        var all = await support.AvailabilityAsync(s.SessionDate, s.PeriodSlot, s.SessionId);
        return all.Where(t => !t.Busy && !t.Absent).Take(10).ToList();
    }

    /// <summary>Hai lựa chọn cho tiết Chờ xử lý chưa bị khóa: chọn giáo viên dạy thay, hoặc hủy. Muốn bù thì thêm tiết bù sau khi hủy.</summary>
    public async Task<AdjustResultDto> ResolveAsync(long sessionId, ResolvePendingRequest r)
    {
        var s = await support.LoadManagedSessionAsync(sessionId);
        EnsurePending(s);
        await support.EnsureNotLockedAsync(s);
        var what = ScheduleSupport.Describe(s.Subject.Name, s.Class.Name, s.SessionDate, s.PeriodSlot);

        if (r.Action == "Substitute")
        {
            var teacherId = r.TeacherId ?? throw ApiException.BadRequest("Chọn giáo viên dạy thay.");
            var teacher = await support.EnsureEligibleTeacherAsync(teacherId);
            var name = teacher.Staff.FullName;
            await support.EnsureTeacherFreeAsync(teacherId, name, s.SessionDate, s.PeriodSlotId, s.SessionId);
            await support.EnsureTeacherNotAbsentAsync(teacherId, name, s.SessionDate, s.PeriodSlot);
            s.Status = "Scheduled";
            s.TeacherId = teacherId;
            support.MarkManual(s, $"Dạy thay: {name}");
            audit.AddLong(AuditCategories.ScheduleAdjust, "ResolvePendingSubstitute", [s.SessionId],
                new { sessionId = s.SessionId, date = s.SessionDate, teacherId, absenceId = s.TeacherAbsenceId });
            var notified = await support.NotifyAsync([s.ClassId], s.SessionDate, [teacherId],
                "Tiết có giáo viên dạy thay", $"{what}: {name} dạy thay.", "TeachingSession", s.SessionId);
            await db.SaveChangesAsync();
            return new AdjustResultDto(s.SessionId, notified);
        }
        if (r.Action == "Cancel")
        {
            s.Status = "Cancelled";
            support.MarkManual(s, "Hủy tiết Chờ xử lý");
            audit.AddLong(AuditCategories.ScheduleAdjust, "ResolvePendingCancel", [s.SessionId],
                new { sessionId = s.SessionId, date = s.SessionDate, absenceId = s.TeacherAbsenceId });
            var notified = await support.NotifyAsync([s.ClassId], s.SessionDate, [], "Tiết đã hủy", $"{what} đã hủy.", "TeachingSession", s.SessionId);
            await db.SaveChangesAsync();
            return new AdjustResultDto(s.SessionId, notified);
        }
        throw ApiException.BadRequest("Chọn một trong hai cách xử lý: giáo viên dạy thay hoặc hủy tiết.");
    }

    private static void EnsurePending(TeachingSession s)
    {
        if (s.Status != "Pending")
            throw ApiException.BadRequest("Tiết này không còn ở trạng thái Chờ xử lý. Tải lại danh sách.");
    }

    private static string Range(DateOnly from, DateOnly to) => from == to ? $"ngày {from:dd/MM/yyyy}" : $"{from:dd/MM/yyyy} – {to:dd/MM/yyyy}";
}
