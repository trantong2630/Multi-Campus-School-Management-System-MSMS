using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Audit;
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
/// SCD-01: xem trước, kiểm xung đột toàn trường và Phát hành (BR-13, BR-14).
/// Phát hành chạy trong MỘT transaction, hai bước: xóa mọi tiết của phân hiệu trong học kỳ từ ngày mai trở đi
/// (ghi đè sạch, kể cả tiết chỉnh tay), rồi sinh lại từ file tới hết học kỳ, bỏ qua ngày nghỉ.
/// Tiết đã qua và tiết đã có điểm danh không bao giờ bị đụng tới.
/// </summary>
public sealed class TimetablePublishService(
    MsmsDbContext db, CurrentUser user, ScopeResolver scope, IClock clock, AuditWriter audit, NotificationSender notifier)
{
    private sealed record NewSession(int ClassId, int SubjectId, int TeacherId, DateOnly Date, int SlotId);
    private sealed record SessionKey(int ClassId, DateOnly Date, int SlotId, int SubjectId, int TeacherId);

    private sealed class Plan
    {
        public required TimetableVersion Version { get; init; }
        public required Semester Semester { get; init; }
        public required string CampusName { get; init; }
        public required DateOnly From { get; init; }
        public required DateOnly To { get; init; }
        public required List<NewSession> NewSessions { get; init; }
        public required List<TimetableEntry> Entries { get; init; }
        public List<string> BlockingIssues { get; } = [];
        public List<TeacherConflictDto> Conflicts { get; } = [];
    }

    // ---------------------------------------------------------------- Xem trước
    /// <summary>Xem trước không được lưu lại: lúc bấm Phát hành hệ thống tính lại, nên con số cuối mới là con số ghi vào phiên bản.</summary>
    public async Task<PublishPreviewDto> PreviewAsync(int versionId)
    {
        var plan = await BuildPlanAsync(versionId);
        var campusId = plan.Version.CampusId;
        var deletable = DeletableSessions(campusId, plan.From, plan.To);

        var deleteByClass = await deletable.GroupBy(s => s.Class.Name).Select(g => new { Name = g.Key, Count = g.Count() }).ToListAsync();
        var createByClass = plan.NewSessions
            .GroupBy(n => plan.Entries.First(e => e.ClassId == n.ClassId).Class.Name)
            .ToDictionary(g => g.Key, g => g.Count());
        var classNames = deleteByClass.Select(x => x.Name).Union(createByClass.Keys).OrderBy(x => x).ToList();
        var byClass = classNames.Select(n => new PreviewClassRow(
            n, deleteByClass.FirstOrDefault(x => x.Name == n)?.Count ?? 0, createByClass.GetValueOrDefault(n))).ToList();

        var warnings = new PublishWarnings(
            await deletable.CountAsync(s => s.Source == "Manual" && s.VersionId != null),
            await deletable.CountAsync(s => s.VersionId == null),
            await deletable.CountAsync(s => s.Status == "Pending"));

        return new PublishPreviewDto(plan.Version.VersionId, plan.CampusName, plan.Semester.Name, plan.From, plan.To,
            byClass.Sum(x => x.DeleteCount), plan.NewSessions.Count, byClass, warnings, plan.Conflicts, plan.BlockingIssues,
            plan.Conflicts.Count == 0 && plan.BlockingIssues.Count == 0);
    }

    // ---------------------------------------------------------------- Phát hành
    public async Task<PublishResultDto> PublishAsync(int versionId, PublishRequest request)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var plan = await BuildPlanAsync(versionId);
        if (plan.BlockingIssues.Count > 0)
            throw ApiException.BadRequest(string.Join(" ", plan.BlockingIssues));
        if (plan.Conflicts.Count > 0)
            throw ApiException.BadRequest($"Còn {plan.Conflicts.Count} xung đột giáo viên với phân hiệu khác. Xung đột chặn Phát hành: phía kia sửa lịch hoặc quản trị hệ thống gỡ, rồi phát hành lại.");

        var campusId = plan.Version.CampusId;
        var deletable = DeletableSessions(campusId, plan.From, plan.To);

        var needsConfirm = await deletable.AnyAsync(s => (s.Source == "Manual" && s.VersionId != null) || s.VersionId == null || s.Status == "Pending");
        if (needsConfirm && !request.ConfirmWarnings)
            throw ApiException.BadRequest("Phát hành sẽ xóa tiết Chỉnh tay, tiết bù hoặc tiết Chờ xử lý. Xem số lượng ở màn hình xem trước và xác nhận để tiếp tục.");

        // Khóa tiết cũ trước khi xóa, dùng để biết giáo viên nào thực sự có lịch thay đổi.
        var oldKeys = (await deletable.Where(s => s.Status != "Cancelled")
                .Select(s => new { s.ClassId, s.SessionDate, s.PeriodSlotId, s.SubjectId, s.TeacherId }).ToListAsync())
            .Select(s => new SessionKey(s.ClassId, s.SessionDate, s.PeriodSlotId, s.SubjectId, s.TeacherId)).ToHashSet();

        var deleteIds = deletable.Select(s => s.SessionId);
        // Tiết được giữ lại (đã qua) có thể trỏ tới tiết gốc sắp bị xóa; gỡ liên kết trước để không vướng khóa ngoại.
        await db.TeachingSession
            .Where(s => s.OriginalSessionId != null && deleteIds.Contains(s.OriginalSessionId.Value) && !deleteIds.Contains(s.SessionId))
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.OriginalSessionId, (long?)null));
        var deleted = await deletable.ExecuteDeleteAsync();

        var now = clock.Now;
        var created = plan.NewSessions.Select(n => new TeachingSession
        {
            ClassId = n.ClassId, SubjectId = n.SubjectId, TeacherId = n.TeacherId, SessionDate = n.Date, PeriodSlotId = n.SlotId,
            Status = "Scheduled", Source = "Generated", VersionId = versionId, CreatedByPersonId = user.PersonId, CreatedAt = now,
        }).ToList();
        db.TeachingSession.AddRange(created);

        var version = await db.TimetableVersion.FirstAsync(v => v.VersionId == versionId);
        version.Status = "Published";   // phiên bản trước không bị đổi trạng thái: mỗi phiên bản là hồ sơ của một lần import
        version.PublishedByPersonId = user.PersonId;
        version.PublishedAt = now;
        version.DeletedSessionCount = deleted;
        version.CreatedSessionCount = created.Count;

        audit.Add(AuditCategories.TimetablePublish, "PublishTimetable", [versionId],
            new { campusId, semesterId = plan.Semester.SemesterId, deleted, created = created.Count });

        var newKeys = plan.NewSessions.Select(n => new SessionKey(n.ClassId, n.Date, n.SlotId, n.SubjectId, n.TeacherId)).ToHashSet();
        var changedTeachers = oldKeys.Except(newKeys).Union(newKeys.Except(oldKeys)).Select(k => k.TeacherId).Distinct().ToList();
        notifier.Add(changedTeachers, NotificationTypes.TimetablePublished, "Thời khóa biểu đã được phát hành",
            $"{plan.CampusName}, {plan.Semester.Name}: lịch dạy từ {plan.From:dd/MM/yyyy} có thay đổi. Xem ở mục Lịch dạy.",
            "TimetableVersion", versionId);

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new PublishResultDto(versionId, deleted, created.Count, changedTeachers.Count);
    }

    // ---------------------------------------------------------------- Dựng kế hoạch
    private IQueryable<TeachingSession> DeletableSessions(int campusId, DateOnly from, DateOnly to) =>
        db.TeachingSession.Where(s => s.Class.CampusId == campusId && s.SessionDate >= from && s.SessionDate <= to
                                      && !s.AttendanceRecord.Any());

    private async Task<Plan> BuildPlanAsync(int versionId)
    {
        var version = await db.TimetableVersion.AsNoTracking()
            .Include(v => v.Campus).Include(v => v.Semester).ThenInclude(s => s.AcademicYear)
            .FirstOrDefaultAsync(v => v.VersionId == versionId) ?? throw ApiException.NotFound("Không tìm thấy phiên bản thời khóa biểu.");
        await scope.EnsureCanManageCampusAsync(version.CampusId);
        if (version.Status != "Draft")
            throw ApiException.BadRequest("Phiên bản này đã phát hành. Muốn đổi lịch thì tải mẫu điền sẵn, sửa rồi import thành phiên bản mới.");

        var semester = version.Semester;
        var today = clock.Today;
        var from = today.AddDays(1) > semester.StartDate ? today.AddDays(1) : semester.StartDate;
        var to = semester.EndDate;
        if (from > to) throw ApiException.BadRequest($"{semester.Name} đã kết thúc, không còn ngày nào để sinh tiết.");

        var entries = await db.TimetableEntry.AsNoTracking()
            .Include(e => e.Class).Include(e => e.PeriodSlot).Include(e => e.Subject)
            .Include(e => e.Teacher).ThenInclude(t => t.Staff).ThenInclude(p => p.Account)
            .Where(e => e.VersionId == versionId).ToListAsync();

        // Ngày học = trong học kỳ, không phải Chủ nhật, không trùng ngày nghỉ toàn trường hay của phân hiệu (SCH-03).
        var holidays = (await db.Holiday.AsNoTracking()
                .Where(h => h.AcademicYearId == semester.AcademicYearId && (h.CampusId == null || h.CampusId == version.CampusId)
                            && h.HolidayDate >= from && h.HolidayDate <= to)
                .Select(h => h.HolidayDate).ToListAsync()).ToHashSet();
        var datesByDow = SchoolCalendarRules.SchoolDays(from, to, holidays).GroupBy(d => d.DayOfWeek).ToDictionary(g => g.Key, g => g.ToList());

        var newSessions = new List<NewSession>();
        foreach (var e in entries)
        {
            if (!datesByDow.TryGetValue(SchoolCalendarRules.ToDayOfWeek(e.DayOfWeek), out var dates)) continue;
            newSessions.AddRange(dates.Select(d => new NewSession(e.ClassId, e.SubjectId, e.TeacherId, d, e.PeriodSlotId)));
        }

        var plan = new Plan
        {
            Version = version, Semester = semester, CampusName = version.Campus.Name, From = from, To = to,
            NewSessions = newSessions, Entries = entries,
        };

        // Giáo viên có thể đã bị khóa hoặc ngừng hoạt động sau lúc import.
        foreach (var teacher in entries.Select(e => e.Teacher).DistinctBy(t => t.StaffId))
        {
            if (TimetableImportService.TeacherProblem(teacher) is { } problem)
                plan.BlockingIssues.Add($"Giáo viên {teacher.TeacherAbbr} ({teacher.Staff.FullName}) không còn đủ điều kiện dạy: {problem}");
        }

        await DetectConflictsAsync(plan);
        return plan;
    }

    /// <summary>
    /// BR-13: mỗi giáo viên tối đa một tiết tại một (ngày, khung giờ) trên toàn trường.
    /// Tiết cùng phân hiệu trong khoảng sinh lại sẽ bị xóa nên không chiếm chỗ; chỉ tiết ở phân hiệu khác
    /// (hoặc tiết đã có điểm danh) mới gây xung đột.
    /// </summary>
    private async Task DetectConflictsAsync(Plan plan)
    {
        var campusId = plan.Version.CampusId;
        var teacherIds = plan.Entries.Select(e => e.TeacherId).Distinct().ToList();
        if (teacherIds.Count == 0) return;

        var occupied = await db.TeachingSession.AsNoTracking()
            .Where(s => teacherIds.Contains(s.TeacherId) && s.SessionDate >= plan.From && s.SessionDate <= plan.To && s.Status != "Cancelled")
            .Where(s => s.Class.CampusId != campusId || s.AttendanceRecord.Any())
            .Select(s => new
            {
                s.TeacherId, s.SessionDate, s.PeriodSlotId, CampusId = s.Class.CampusId, CampusName = s.Class.Campus.Name,
                ClassName = s.Class.Name, Subject = s.Subject.Abbreviation,
            }).ToListAsync();
        if (occupied.Count == 0) return;

        var index = occupied.ToDictionary(o => (o.TeacherId, o.SessionDate, o.PeriodSlotId));
        var entryByKey = plan.Entries.ToDictionary(e => (e.ClassId, Day: (int)e.DayOfWeek, e.PeriodSlotId));
        var hits = new Dictionary<(int Teacher, int Day, int Slot, int OccCampus, string OccClass, string OccSubject), (List<DateOnly> Dates, string NewClass)>();

        foreach (var n in plan.NewSessions)
        {
            if (!index.TryGetValue((n.TeacherId, n.Date, n.SlotId), out var occ)) continue;
            var vnDay = SchoolCalendarRules.ToVnDay(n.Date.DayOfWeek);
            var key = (n.TeacherId, vnDay, n.SlotId, occ.CampusId, occ.ClassName, occ.Subject);
            if (!hits.TryGetValue(key, out var hit))
                hits[key] = hit = ([], entryByKey[(n.ClassId, vnDay, n.SlotId)].Class.Name);
            hit.Dates.Add(n.Date);
        }
        if (hits.Count == 0) return;

        // Mỗi dòng xung đột ghi rõ phân hiệu đang chiếm chỗ và ai là hiệu phó phụ trách phân hiệu đó (BR-14).
        var occCampusIds = hits.Keys.Select(k => k.OccCampus).Distinct().ToList();
        var today = clock.Today;
        var vices = await db.CampusLeadership.AsNoTracking().Where(EffectivePeriod.EffectiveAt<CampusLeadership>(today))
            .Where(l => occCampusIds.Contains(l.CampusId))
            .Select(l => new { l.CampusId, Name = l.Staff.Staff.FullName }).ToListAsync();
        var campusNames = occupied.GroupBy(o => o.CampusId).ToDictionary(g => g.Key, g => g.First().CampusName);
        var teachers = plan.Entries.Select(e => e.Teacher).DistinctBy(t => t.StaffId).ToDictionary(t => t.StaffId);
        var slots = plan.Entries.Select(e => e.PeriodSlot).DistinctBy(p => p.PeriodSlotId).ToDictionary(p => p.PeriodSlotId);

        foreach (var (key, hit) in hits.OrderBy(h => h.Key.Day).ThenBy(h => h.Key.Slot))
        {
            var t = teachers[key.Teacher];
            var slot = slots[key.Slot];
            var dates = hit.Dates.OrderBy(d => d).ToList();
            plan.Conflicts.Add(new TeacherConflictDto(
                t.Staff.FullName, t.TeacherAbbr ?? "", key.Day, slot.SessionOfDay, slot.PeriodNo, hit.NewClass,
                campusNames[key.OccCampus], key.OccClass, key.OccSubject,
                vices.FirstOrDefault(v => v.CampusId == key.OccCampus)?.Name, dates.Count, dates[0], dates[^1]));
        }
    }
}
