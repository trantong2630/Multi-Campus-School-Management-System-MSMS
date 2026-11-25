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
/// SCD-03: giáo viên vắng (BR-16). Tổ trưởng khai báo vắng cho giáo viên thuộc tổ mình: các tiết Đã xếp lịch chưa bị khóa của giáo viên trong khoảng,
/// ở mọi phân hiệu, chuyển sang Chờ xử lý (Chỉnh tay) và để trống giáo viên. Tổ trưởng quyết định từng tiết bằng một ĐỀ XUẤT (giáo viên dạy thay hoặc
/// hủy tiết); đề xuất chưa đổi lịch. Hiệu phó của phân hiệu có tiết chỉ DUYỆT: khi tổ trưởng đã quyết định hết mọi tiết của khai báo thì duyệt một lần,
/// mọi đề xuất ở phân hiệu mình được áp cùng lúc, tất cả hoặc không. Hiệu phó vẫn xử lý tay từng tiết (chọn người khác hoặc hủy) khi cần.
/// Đây không phải đơn xin nghỉ của cán bộ.
/// </summary>
public sealed class TeacherAbsenceService(
    MsmsDbContext db, CurrentUser user, ScopeResolver scope, IClock clock, AuditWriter audit, ScheduleSupport support,
    AssignmentEmailNotifier mailer)
{
    private const int MaxDays = 90;
    private const string ActionSubstitute = "Substitute", ActionCancel = "Cancel", ActionClear = "Clear";

    // ---------------------------------------------------------------- Tổ trưởng: phạm vi
    /// <summary>Các tổ người gọi đang làm tổ trưởng hôm nay (BR-09); rỗng nếu không phải tổ trưởng.</summary>
    private async Task<List<int>> HeadDepartmentIdsAsync() =>
        user.AccountType == AccountTypes.Teacher
            ? (await new AccountScopeResolver(db, clock).ResolveAsync(user.PersonId, user.AccountType)).HeadDepartmentIds.ToList()
            : [];

    private async Task<List<int>> RequireHeadAsync()
    {
        var departments = await HeadDepartmentIdsAsync();
        if (departments.Count == 0) throw ApiException.Forbidden("Chỉ tổ trưởng đang giữ trách nhiệm mới dùng được chức năng này.");
        return departments;
    }

    /// <summary>Giáo viên thuộc các tổ người gọi làm tổ trưởng — danh sách chọn khi tổ trưởng khai báo vắng.</summary>
    public async Task<List<TeacherOptionDto>> DepartmentTeachersAsync()
    {
        var departments = await RequireHeadAsync();
        return await db.StaffProfile.AsNoTracking()
            .Where(t => t.IsActive && t.DepartmentId != null && departments.Contains(t.DepartmentId.Value)
                        && t.Staff.Account != null && t.Staff.Account.AccountType == AccountTypes.Teacher
                        && t.Staff.Account.Status != AccountStatuses.Locked)
            .OrderBy(t => t.Staff.FullName).ThenBy(t => t.StaffId)
            .Select(t => new TeacherOptionDto(t.StaffId, t.Staff.FullName, t.TeacherAbbr)).ToListAsync();
    }

    // ---------------------------------------------------------------- Khai báo (tổ trưởng)
    /// <summary>
    /// Khai theo ngày (khoảng ngày, cả ngày) hoặc theo tiết (một ngày, các tiết đã chọn — danh sách tiết khác rỗng).
    /// Chỉ tổ trưởng khai, cho giáo viên thuộc tổ mình; tác động tiết ở mọi phân hiệu giáo viên đó dạy.
    /// </summary>
    public async Task<DeclareAbsenceResultDto> DeclareAsync(DeclareAbsenceRequest r)
    {
        var headDepartments = await RequireHeadAsync();
        var teacher = await db.StaffProfile.AsNoTracking().Include(t => t.Staff).ThenInclude(p => p.Account)
                          .FirstOrDefaultAsync(t => t.StaffId == r.TeacherId && t.Staff.Account != null && t.Staff.Account.AccountType == AccountTypes.Teacher)
                      ?? throw ApiException.BadRequest("Chọn một giáo viên.");
        var name = teacher.Staff.FullName;
        if (teacher.DepartmentId is not { } teacherDepartment || !headDepartments.Contains(teacherDepartment))
            throw ApiException.Forbidden("Tổ trưởng chỉ khai vắng cho giáo viên thuộc tổ mình.");
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
        await using var tx = await db.Database.BeginTransactionAsync();
        var sessions = await db.TeachingSession.Include(s => s.Class)
            .Where(support.NotEnded())
            .Where(s => s.TeacherId == r.TeacherId && s.Status == "Scheduled" && s.SessionDate >= start && s.SessionDate <= r.ToDate
                        && !s.AttendanceRecord.Any()
                        && (slotIds.Count == 0 || slotIds.Contains(s.PeriodSlotId)))
            .ToListAsync();
        if (sessions.Count == 0)
        {
            // Không có tiết nào để chuyển Chờ xử lý: nói đúng lý do. Tiết của giáo viên gồm cả tiết đã khai vắng (đang để trống giáo viên).
            var mine = await db.TeachingSession.AsNoTracking().Include(s => s.PeriodSlot)
                .Where(s => s.SessionDate >= r.FromDate && s.SessionDate <= r.ToDate
                            && (slotIds.Count == 0 || slotIds.Contains(s.PeriodSlotId))
                            && (s.TeacherId == r.TeacherId || (s.TeacherAbsence != null && s.TeacherAbsence.TeacherId == r.TeacherId)))
                .Select(s => new { s.Status, s.SessionDate, s.PeriodSlot, Marked = s.AttendanceRecord.Any() }).ToListAsync();
            if (mine.Any(m => m.Status == "Pending" && !support.HasEnded(m.SessionDate, m.PeriodSlot)))
                throw ApiException.BadRequest($"Các tiết của {name} trong phạm vi này đã được khai vắng và đang Chờ xử lý. Xử lý ở tab Tiết cần dạy thay.");
            if (mine.Any(m => m.Marked || support.HasEnded(m.SessionDate, m.PeriodSlot)))
                throw ApiException.BadRequest($"Các tiết của {name} trong phạm vi này đã kết thúc hoặc đã có điểm danh nên bị khóa, không khai vắng được.");
            if (mine.Count > 0)
                throw ApiException.BadRequest($"Các tiết của {name} trong phạm vi này đã được xử lý (hủy hoặc có người dạy thay).");
            throw ApiException.BadRequest($"{name} không có tiết nào trong phạm vi này. Chọn ngày hoặc tiết khác.");
        }

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
        audit.Add(AuditCategories.ScheduleAdjust, AuditActions.DeclareTeacherAbsence, [absence.AbsenceId],
            ("Giáo viên", r.TeacherId), ("Từ ngày", r.FromDate), ("Đến ngày", r.ToDate), ("Các tiết", slotIds),
            ("Số tiết chuyển Chờ xử lý", sessions.Count), ("Mã các tiết", sessions.Select(s => s.SessionId)));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new DeclareAbsenceResultDto(absence.AbsenceId, sessions.Count);
    }

    // ---------------------------------------------------------------- Danh sách và xóa khai báo
    /// <summary>Khai báo có tiết ở phân hiệu (Principal đọc, hiệu phó đọc và duyệt). Tiết đếm theo phân hiệu; "đã quyết định" đếm trên mọi phân hiệu của khai báo.</summary>
    public async Task<List<AbsenceDto>> ListAsync(int campusId)
    {
        await scope.EnsureCanReadCampusAsync(campusId);
        return await BuildAbsencesAsync(db.TeacherAbsence.AsNoTracking()
            .Where(a => a.TeachingSession.Any(s => s.Class.CampusId == campusId)), campusId, false);
    }

    /// <summary>Khai báo vắng của giáo viên thuộc tổ mình (mọi phân hiệu), kèm khai báo do chính tổ trưởng khai. Chỉ khai báo của chính mình mới xóa được.</summary>
    public async Task<List<AbsenceDto>> ListForDepartmentAsync()
    {
        var departments = await RequireHeadAsync();
        return await BuildAbsencesAsync(db.TeacherAbsence.AsNoTracking()
            .Where(a => a.DeclaredByPersonId == user.PersonId
                        || (a.Teacher.DepartmentId != null && departments.Contains(a.Teacher.DepartmentId.Value))), null, true);
    }

    /// <summary><paramref name="campusId"/> null: đếm tiết ở mọi phân hiệu (tổ trưởng).</summary>
    private async Task<List<AbsenceDto>> BuildAbsencesAsync(IQueryable<TeacherAbsence> source, int? campusId, bool deleteOwnOnly)
    {
        var rows = await source
            .OrderByDescending(a => a.FromDate).ThenByDescending(a => a.AbsenceId).Take(200)
            .Select(a => new
            {
                a.AbsenceId, a.TeacherId, TeacherName = a.Teacher.Staff.FullName, a.Teacher.TeacherAbbr, a.FromDate, a.ToDate, a.SlotIds, a.Reason,
                DeclaredBy = a.DeclaredByPerson == null ? null : a.DeclaredByPerson.FullName, a.CreatedAt, a.DeclaredByPersonId,
            }).ToListAsync();

        // Đếm trong bộ nhớ vì "đã kết thúc" cần so giờ kết thúc của khung giờ.
        var ids = rows.Select(a => a.AbsenceId).ToList();
        var sessions = (await db.TeachingSession.AsNoTracking()
                .Where(s => s.TeacherAbsenceId != null && ids.Contains(s.TeacherAbsenceId.Value))
                .Select(s => new { Id = s.TeacherAbsenceId!.Value, s.Class.CampusId, s.Status, s.SessionDate, s.PeriodSlot, s.ProposedAction })
                .ToListAsync())
            .ToLookup(s => s.Id);
        var isVice = user.AccountType == AccountTypes.VicePrincipal;

        return rows.Select(a =>
        {
            var all = sessions[a.AbsenceId].ToList();
            var here = campusId is null ? all : all.Where(s => s.CampusId == campusId).ToList();
            bool Ended(DateOnly d, PeriodSlot p) => support.HasEnded(d, p);
            var pending = here.Count(s => s.Status == "Pending" && !Ended(s.SessionDate, s.PeriodSlot));
            var missed = here.Count(s => s.Status == "Pending" && Ended(s.SessionDate, s.PeriodSlot));
            var handled = here.Count(s => s.Status != "Pending");
            var isPast = all.Count > 0 && all.All(s => Ended(s.SessionDate, s.PeriodSlot));
            var handledAnywhere = all.Count(s => s.Status != "Pending");   // DeleteAsync đếm mọi phân hiệu
            // Tiết còn chờ ở mọi phân hiệu: tổ trưởng phải quyết định hết trước khi hiệu phó được duyệt.
            var awaiting = all.Where(s => s.Status == "Pending" && !Ended(s.SessionDate, s.PeriodSlot)).ToList();
            var decided = awaiting.Count(s => s.ProposedAction != null);
            var canApprove = isVice && awaiting.Count > 0 && decided == awaiting.Count && pending > 0;
            return new AbsenceDto(
                a.AbsenceId, a.TeacherId, a.TeacherName, a.TeacherAbbr, a.FromDate, a.ToDate, AbsenceQueries.ParseSlots(a.SlotIds), a.Reason,
                a.DeclaredBy, a.CreatedAt, pending, handled,
                handledAnywhere == 0 && !isPast && (!deleteOwnOnly || a.DeclaredByPersonId == user.PersonId), missed, isPast,
                awaiting.Count, decided, canApprove);
        }).ToList();
    }

    /// <summary>
    /// Xóa khai báo theo kiểu tất cả hoặc không: chỉ tổ trưởng đã khai (cho giáo viên thuộc tổ mình) xóa được, khi chưa có tiết nào của khai báo bị xử lý
    /// (dạy thay, hủy) và giáo viên vẫn đủ điều kiện dạy (BR-06), không trùng lịch ở tiết nào (BR-13). Khi đó mọi tiết trở về giáo viên, trạng thái
    /// Đã xếp lịch, và các đề xuất bị bỏ.
    /// </summary>
    public async Task<DeleteAbsenceResultDto> DeleteAsync(int absenceId)
    {
        var absence = await db.TeacherAbsence.Include(a => a.Teacher).ThenInclude(t => t.Staff).ThenInclude(p => p.Account)
                          .FirstOrDefaultAsync(a => a.AbsenceId == absenceId)
                      ?? throw ApiException.NotFound("Không tìm thấy khai báo vắng.");
        var linked = await db.TeachingSession.Include(s => s.Class).Include(s => s.PeriodSlot)
            .Where(s => s.TeacherAbsenceId == absenceId).ToListAsync();
        var restorable = linked.Where(s => !support.HasEnded(s.SessionDate, s.PeriodSlot)).ToList();   // tiết đã qua bị khóa: giữ nguyên

        var departments = await HeadDepartmentIdsAsync();
        if (absence.DeclaredByPersonId != user.PersonId || absence.Teacher.DepartmentId is not { } d || !departments.Contains(d))
            throw ApiException.Forbidden("Chỉ tổ trưởng đã khai báo (cho giáo viên thuộc tổ mình) mới xóa được.");

        if (linked.Count > 0 && restorable.Count == 0)
            throw ApiException.BadRequest("Khai báo này đã qua: mọi tiết đã kết thúc nên bị khóa. Khai báo chỉ để tra cứu, không xóa được.");

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
            throw ApiException.BadRequest($"Không xóa được vì {teacherName} đã có tiết khác trùng lịch: {string.Join("; ", clashes.Take(5))}{(clashes.Count > 5 ? $"; và {clashes.Count - 5} tiết nữa" : "")}. Xử lý các tiết đó rồi xóa lại.");

        foreach (var s in restorable)
        {
            s.Status = "Scheduled";
            s.TeacherId = absence.TeacherId;
            s.TeacherAbsenceId = null;
            s.ProposedTeacherId = null;
            s.ProposedAction = null;
            support.MarkManual(s, $"Giáo viên {teacherName} trở lại: khai báo vắng đã xóa");
        }
        db.TeacherAbsence.Remove(absence);

        if (restorable.Count > 0)
        {
            await support.NotifyAsync(restorable.Select(s => s.ClassId), restorable.Min(s => s.SessionDate), [absence.TeacherId],
                "Tiết được trả lại giáo viên",
                $"{teacherName}: khai báo vắng {Range(absence.FromDate, absence.ToDate)} đã xóa, {restorable.Count} tiết trở lại lịch của giáo viên.",
                // Trỏ vào tiết sớm nhất được trả lại: khai báo vắng bị xóa ngay sau đây nên không trỏ vào nó được.
                "TeachingSession", restorable.OrderBy(s => s.SessionDate).ThenBy(s => s.PeriodSlot.StartTime).First().SessionId);
        }
        audit.Add(AuditCategories.ScheduleAdjust, AuditActions.DeleteTeacherAbsence, [absenceId],
            ("Giáo viên", absence.TeacherId), ("Từ ngày", absence.FromDate), ("Đến ngày", absence.ToDate),
            ("Mã các tiết trả lại", restorable.Select(s => s.SessionId)));
        await db.SaveChangesAsync();
        return new DeleteAbsenceResultDto(restorable.Count);
    }

    // ---------------------------------------------------------------- Tiết Chờ xử lý
    /// <summary>Tiết Chờ xử lý của phân hiệu từ hôm nay trở đi; tiết đã qua bị khóa nên không còn nằm trong danh sách.</summary>
    public async Task<List<PendingSessionDto>> ListPendingAsync(int campusId, DateOnly? from, DateOnly? to, int? classId)
    {
        await scope.EnsureCanReadCampusAsync(campusId);
        var query = db.TeachingSession.AsNoTracking().Where(support.NotEnded()).Where(s => s.Class.CampusId == campusId && s.Status == "Pending");
        if (from is { } f) query = query.Where(s => s.SessionDate >= f);
        if (to is { } t) query = query.Where(s => s.SessionDate <= t);
        if (classId is { } c) query = query.Where(s => s.ClassId == c);
        return await ProjectPending(query);
    }

    /// <summary>
    /// Tiết Chờ xử lý, chưa kết thúc, của khai báo vắng, thuộc các môn của tổ mình làm tổ trưởng, ở mọi phân hiệu — để tổ trưởng quyết định từng tiết.
    /// Tiết Chờ xử lý vì lý do khác (ví dụ tài khoản giáo viên bị khóa) không có khai báo để duyệt nên hiệu phó xử lý tay.
    /// </summary>
    public async Task<List<PendingSessionDto>> ListPendingForDepartmentAsync()
    {
        var departments = await RequireHeadAsync();
        return await ProjectPending(db.TeachingSession.AsNoTracking().Where(support.NotEnded())
            .Where(s => s.Status == "Pending" && s.TeacherAbsenceId != null && departments.Contains(s.Subject.DepartmentId)));
    }

    private static Task<List<PendingSessionDto>> ProjectPending(IQueryable<TeachingSession> query) =>
        query.OrderBy(s => s.SessionDate).ThenBy(s => s.PeriodSlot.StartTime).ThenBy(s => s.Class.Name).Take(500)
            .Select(s => new PendingSessionDto(
                s.SessionId, s.SessionDate, s.PeriodSlotId, s.PeriodSlot.SessionOfDay, s.PeriodSlot.PeriodNo, s.PeriodSlot.StartTime, s.PeriodSlot.EndTime,
                s.ClassId, s.Class.Name, s.SubjectId, s.Subject.Name, s.Subject.Abbreviation,
                s.TeacherAbsenceId, s.TeacherAbsence == null ? null : s.TeacherAbsence.Teacher.Staff.FullName, s.ChangeNote,
                s.Class.Campus.Name, s.ProposedAction, s.ProposedTeacherId, s.ProposedTeacher == null ? null : s.ProposedTeacher.Staff.FullName,
                s.TeacherAbsence == null ? null : s.TeacherAbsence.Reason,
                s.TeacherAbsence == null ? null : (DateOnly?)s.TeacherAbsence.FromDate,
                s.TeacherAbsence == null ? null : (DateOnly?)s.TeacherAbsence.ToDate))
            .ToListAsync();

    /// <summary>Gợi ý giáo viên rảnh cùng tổ chuyên môn với môn của tiết: đủ điều kiện dạy, không có tiết Đã xếp lịch cùng (ngày, khung giờ), không có khai báo vắng phủ ngày và tiết đó. Sắp theo tên, tối đa 10, không xếp hạng.</summary>
    public async Task<List<TeacherAvailabilityDto>> SuggestionsAsync(long sessionId) =>
        await SuggestForAsync(await support.LoadManagedSessionAsync(sessionId));

    /// <summary>Gợi ý cho tổ trưởng: cùng cách tính, nhưng tiết phải thuộc môn của tổ mình thay vì phân hiệu mình phụ trách.</summary>
    public async Task<List<TeacherAvailabilityDto>> DepartmentSuggestionsAsync(long sessionId) =>
        await SuggestForAsync(await LoadDepartmentSessionAsync(sessionId));

    private async Task<TeachingSession> LoadDepartmentSessionAsync(long sessionId)
    {
        var departments = await RequireHeadAsync();
        var s = await support.LoadSessionAsync(sessionId);
        if (!departments.Contains(s.Subject.DepartmentId))
            throw ApiException.Forbidden("Chỉ xử lý tiết của môn thuộc tổ mình làm tổ trưởng.");
        return s;
    }

    private async Task<List<TeacherAvailabilityDto>> SuggestForAsync(TeachingSession s)
    {
        EnsurePending(s);
        await support.EnsureNotLockedAsync(s);
        var departmentId = s.Subject.DepartmentId;
        var sameDepartment = (await db.StaffProfile.AsNoTracking().Where(t => t.DepartmentId == departmentId).Select(t => t.StaffId).ToListAsync()).ToHashSet();
        var all = await support.AvailabilityAsync(s.SessionDate, s.PeriodSlot, s.SessionId);
        return all.Where(t => !t.Busy && !t.Absent && sameDepartment.Contains(t.TeacherId)).Take(10).ToList();
    }

    // ---------------------------------------------------------------- Tổ trưởng quyết định từng tiết (đề xuất)
    /// <summary>
    /// Tổ trưởng quyết định một tiết Chờ xử lý của môn thuộc tổ mình bằng một đề xuất: Substitute (giáo viên cùng tổ dạy thay), Cancel (hủy tiết) hoặc
    /// Clear (bỏ đề xuất). Không đổi lịch: tiết vẫn Chờ xử lý cho tới khi hiệu phó duyệt. Giáo viên đề xuất phải cùng tổ, đủ điều kiện dạy, rảnh và không có
    /// khai báo vắng phủ tiết đó (BR-06, BR-13, BR-16). Khi mọi tiết còn chờ của khai báo đều đã có đề xuất, hiệu phó các phân hiệu có tiết được báo để duyệt.
    /// </summary>
    public async Task<AdjustResultDto> ProposeAsync(long sessionId, ProposeSessionRequest r)
    {
        var s = await LoadDepartmentSessionAsync(sessionId);
        EnsurePending(s);
        if (s.TeacherAbsenceId is null)
            throw ApiException.BadRequest("Tiết này không thuộc khai báo vắng nào nên hiệu phó xử lý tay, tổ trưởng không quyết định được.");
        await support.EnsureNotLockedAsync(s);
        var hadProposal = s.ProposedAction is not null;

        switch (r.Action)
        {
            case ActionClear:
                s.ProposedAction = null;
                s.ProposedTeacherId = null;
                await db.SaveChangesAsync();
                return new AdjustResultDto(s.SessionId, 0);

            case ActionCancel:
                s.ProposedAction = ActionCancel;
                s.ProposedTeacherId = null;
                audit.AddLong(AuditCategories.ScheduleAdjust, AuditActions.ProposeSession, [s.SessionId],
                    ("Lớp", s.ClassId), ("Ngày", s.SessionDate), ("Đề xuất", "Hủy tiết"));
                break;

            case ActionSubstitute:
            {
                var teacherId = r.TeacherId ?? throw ApiException.BadRequest("Chọn giáo viên dạy thay.");
                var teacher = await support.EnsureEligibleTeacherAsync(teacherId);
                if (teacher.DepartmentId != s.Subject.DepartmentId)
                    throw ApiException.BadRequest("Chỉ đề xuất giáo viên cùng tổ chuyên môn với môn của tiết.");
                await ValidateSubstituteAsync(s, teacherId, teacher.Staff.FullName);
                s.ProposedAction = ActionSubstitute;
                s.ProposedTeacherId = teacherId;
                audit.AddLong(AuditCategories.ScheduleAdjust, AuditActions.ProposeSession, [s.SessionId],
                    ("Lớp", s.ClassId), ("Ngày", s.SessionDate), ("Đề xuất", "Dạy thay"), ("Giáo viên", teacherId));
                break;
            }

            default:
                throw ApiException.BadRequest("Chọn một trong các cách xử lý: giáo viên dạy thay, hủy tiết hoặc bỏ đề xuất.");
        }

        await db.SaveChangesAsync();
        if (!hadProposal && s.TeacherAbsenceId is { } absenceId) await NotifyIfReadyAsync(absenceId);
        return new AdjustResultDto(s.SessionId, 1);
    }

    /// <summary>Tổ trưởng vừa quyết định xong tiết cuối của khai báo: báo hiệu phó từng phân hiệu có tiết chờ duyệt (loại SessionPending, BR-26).</summary>
    private async Task NotifyIfReadyAsync(int absenceId)
    {
        var awaiting = (await db.TeachingSession.AsNoTracking().Include(s => s.PeriodSlot).Include(s => s.Class)
                .Where(s => s.TeacherAbsenceId == absenceId && s.Status == "Pending").ToListAsync())
            .Where(s => !support.HasEnded(s.SessionDate, s.PeriodSlot)).ToList();
        if (awaiting.Count == 0 || awaiting.Any(s => s.ProposedAction is null)) return;

        var absence = await db.TeacherAbsence.AsNoTracking().Include(a => a.Teacher).ThenInclude(t => t.Staff).FirstAsync(a => a.AbsenceId == absenceId);
        foreach (var campus in awaiting.GroupBy(s => s.Class.CampusId))
            await support.NotifyVicesAsync(campus.Key, "Khai báo vắng chờ duyệt",
                $"Tổ trưởng đã xử lý xong khai báo vắng của {absence.Teacher.Staff.FullName} {Range(absence.FromDate, absence.ToDate)}: {campus.Count()} tiết của phân hiệu chờ bạn duyệt.");
        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------- Hiệu phó duyệt
    /// <summary>
    /// Hiệu phó duyệt khai báo: chỉ khi tổ trưởng đã quyết định MỌI tiết còn chờ của khai báo (ở mọi phân hiệu). Áp các đề xuất cho tiết ở phân hiệu mình
    /// phụ trách (dạy thay hoặc hủy), gửi thông báo và email như xử lý thường. Tất cả hoặc không: một đề xuất không còn hợp lệ (giáo viên đã bận hoặc vắng)
    /// thì không áp tiết nào, hiệu phó xử lý tay tiết đó rồi duyệt lại. Tiết ở phân hiệu khác do hiệu phó phân hiệu đó duyệt.
    /// </summary>
    public async Task<ApproveAbsenceResultDto> ApproveAsync(int absenceId)
    {
        var absence = await db.TeacherAbsence.Include(a => a.Teacher).ThenInclude(t => t.Staff)
                          .FirstOrDefaultAsync(a => a.AbsenceId == absenceId)
                      ?? throw ApiException.NotFound("Không tìm thấy khai báo vắng.");
        var led = (await scope.LedCampusIdsAsync()).ToList();
        var awaiting = (await db.TeachingSession.Include(s => s.Class).ThenInclude(c => c.Campus).Include(s => s.PeriodSlot).Include(s => s.Subject)
                .Where(s => s.TeacherAbsenceId == absenceId && s.Status == "Pending").ToListAsync())
            .Where(s => !support.HasEnded(s.SessionDate, s.PeriodSlot)).ToList();
        if (awaiting.Count == 0) throw ApiException.BadRequest("Khai báo này không còn tiết nào chờ duyệt.");
        var mine = awaiting.Where(s => led.Contains(s.Class.CampusId)).ToList();
        if (mine.Count == 0) throw ApiException.Forbidden("Khai báo này không có tiết ở phân hiệu bạn phụ trách.");

        var undecided = awaiting.Count(s => s.ProposedAction is null);
        if (undecided > 0)
            throw ApiException.BadRequest($"Tổ trưởng mới quyết định {awaiting.Count - undecided}/{awaiting.Count} tiết. Chờ tổ trưởng quyết định hết các tiết rồi duyệt.");

        // Kiểm trước mọi đề xuất (kể cả hai đề xuất cùng giáo viên, cùng khung giờ) để áp tất cả hoặc không.
        var problems = new List<string>();
        var taken = new HashSet<(int Teacher, DateOnly Date, int Slot)>();
        foreach (var s in mine.OrderBy(x => x.SessionDate).ThenBy(x => x.PeriodSlot.StartTime))
        {
            await support.EnsureNotLockedAsync(s);
            if (s.ProposedAction != ActionSubstitute) continue;
            var teacherId = s.ProposedTeacherId!.Value;
            var label = $"{ScheduleSupport.Describe(s.Subject.Name, s.Class.Name, s.SessionDate, s.PeriodSlot)}";
            try
            {
                var teacher = await support.EnsureEligibleTeacherAsync(teacherId);
                await ValidateSubstituteAsync(s, teacherId, teacher.Staff.FullName);
                if (!taken.Add((teacherId, s.SessionDate, s.PeriodSlotId)))
                    problems.Add($"{label}: {teacher.Staff.FullName} được đề xuất dạy hai lớp cùng khung giờ.");
            }
            catch (ApiException e)
            {
                problems.Add($"{label}: {e.Message}");
            }
        }
        if (problems.Count > 0)
            throw ApiException.BadRequest($"Chưa duyệt được vì {problems.Count} tiết không còn hợp lệ: {string.Join(" | ", problems.Take(3))}{(problems.Count > 3 ? $" | và {problems.Count - 3} tiết nữa" : "")}. Xử lý tay các tiết đó (chọn người khác hoặc hủy) rồi duyệt lại.");

        int substituted = 0, cancelled = 0;
        foreach (var s in mine)
        {
            if (s.ProposedAction == ActionSubstitute)
            {
                var teacherId = s.ProposedTeacherId!.Value;
                var teacher = await support.EnsureEligibleTeacherAsync(teacherId);
                await ApplySubstituteAsync(s, teacherId, teacher.Staff.FullName);
                substituted++;
            }
            else
            {
                await ApplyCancelAsync(s);
                cancelled++;
            }
        }
        audit.Add(AuditCategories.ScheduleAdjust, AuditActions.ApproveAbsence, [absenceId],
            ("Giáo viên", absence.TeacherId), ("Từ ngày", absence.FromDate), ("Đến ngày", absence.ToDate),
            ("Số tiết dạy thay", substituted), ("Số tiết hủy", cancelled), ("Mã các tiết", mine.Select(s => s.SessionId)));
        await db.SaveChangesAsync();
        _ = mailer.Dispatch();
        return new ApproveAbsenceResultDto(absenceId, substituted, cancelled, awaiting.Count - mine.Count);
    }

    // ---------------------------------------------------------------- Hiệu phó xử lý tay từng tiết
    /// <summary>Hai lựa chọn cho tiết Chờ xử lý chưa bị khóa: chọn giáo viên dạy thay, hoặc hủy. Muốn bù thì thêm tiết bù sau khi hủy.</summary>
    public async Task<AdjustResultDto> ResolveAsync(long sessionId, ResolvePendingRequest r)
    {
        var s = await support.LoadManagedSessionAsync(sessionId);
        EnsurePending(s);
        await support.EnsureNotLockedAsync(s);

        if (r.Action == "Substitute")
        {
            var teacherId = r.TeacherId ?? throw ApiException.BadRequest("Chọn giáo viên dạy thay.");
            var teacher = await support.EnsureEligibleTeacherAsync(teacherId);
            var name = teacher.Staff.FullName;
            await ValidateSubstituteAsync(s, teacherId, name);
            audit.AddLong(AuditCategories.ScheduleAdjust, AuditActions.ResolvePendingSubstitute, [s.SessionId],
                ("Ngày", s.SessionDate), ("Giáo viên", teacherId), ("Mã khai báo vắng", s.TeacherAbsenceId));
            var notified = await ApplySubstituteAsync(s, teacherId, name);
            await db.SaveChangesAsync();
            _ = mailer.Dispatch();
            return new AdjustResultDto(s.SessionId, notified);
        }
        if (r.Action == "Cancel")
        {
            audit.AddLong(AuditCategories.ScheduleAdjust, AuditActions.ResolvePendingCancel, [s.SessionId],
                ("Ngày", s.SessionDate), ("Mã khai báo vắng", s.TeacherAbsenceId));
            var notified = await ApplyCancelAsync(s);
            await db.SaveChangesAsync();
            return new AdjustResultDto(s.SessionId, notified);
        }
        throw ApiException.BadRequest("Chọn một trong hai cách xử lý: giáo viên dạy thay hoặc hủy tiết.");
    }

    /// <summary>Giáo viên được chọn dạy thay phải rảnh và không có khai báo vắng phủ tiết (BR-13, BR-16); điều kiện dạy đã kiểm ở người gọi.</summary>
    private async Task ValidateSubstituteAsync(TeachingSession s, int teacherId, string name)
    {
        await support.EnsureTeacherFreeAsync(teacherId, name, s.SessionDate, s.PeriodSlotId, s.SessionId);
        await support.EnsureTeacherNotAbsentAsync(teacherId, name, s.SessionDate, s.PeriodSlot);
    }

    /// <summary>Đổi tiết sang Đã xếp lịch với giáo viên dạy thay, xóa đề xuất, thông báo và xếp email; người gọi SaveChanges. Trả về số người được báo.</summary>
    private async Task<int> ApplySubstituteAsync(TeachingSession s, int teacherId, string name)
    {
        var what = ScheduleSupport.Describe(s.Subject.Name, s.Class.Name, s.SessionDate, s.PeriodSlot);
        s.Status = "Scheduled";
        s.TeacherId = teacherId;
        s.ProposedTeacherId = null;
        s.ProposedAction = null;
        support.MarkManual(s, $"Dạy thay: {name}");
        var notified = await support.NotifyAsync([s.ClassId], s.SessionDate, [teacherId],
            "Tiết có giáo viên dạy thay", $"{what}: {name} dạy thay.", "TeachingSession", s.SessionId);
        await mailer.QueueAsync(teacherId, $"[MSMS] Phân công dạy thay: {what}",
            AssignmentEmailNotifier.Body("Bạn được phân công dạy thay tiết sau:", what, s.PeriodSlot));
        return notified;
    }

    /// <summary>Hủy tiết Chờ xử lý, xóa đề xuất và thông báo học sinh của lớp; người gọi SaveChanges.</summary>
    private async Task<int> ApplyCancelAsync(TeachingSession s)
    {
        var what = ScheduleSupport.Describe(s.Subject.Name, s.Class.Name, s.SessionDate, s.PeriodSlot);
        s.Status = "Cancelled";
        s.ProposedTeacherId = null;
        s.ProposedAction = null;
        support.MarkManual(s, "Hủy tiết Chờ xử lý");
        return await support.NotifyAsync([s.ClassId], s.SessionDate, [], "Tiết đã hủy", $"{what} đã hủy.", "TeachingSession", s.SessionId);
    }

    private static void EnsurePending(TeachingSession s)
    {
        if (s.Status != "Pending")
            throw ApiException.BadRequest("Tiết này không còn ở trạng thái Chờ xử lý. Tải lại danh sách.");
    }

    private static string Range(DateOnly from, DateOnly to) => from == to ? $"ngày {from:dd/MM/yyyy}" : $"{from:dd/MM/yyyy} – {to:dd/MM/yyyy}";
}
