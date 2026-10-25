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
/// Phát hành chạy trong MỘT transaction, hai bước: xóa mọi tiết của các lớp có trong phiên bản, trong học kỳ, từ ngày mai
/// trở đi (ghi đè sạch, kể cả tiết chỉnh tay), rồi sinh lại từ phiên bản tới hết học kỳ, bỏ qua ngày nghỉ.
/// Lớp không có trong phiên bản không bị đụng tới. Tiết đã qua và tiết đã có điểm danh không bao giờ bị đụng tới.
/// </summary>
public sealed class TimetablePublishService(
    MsmsDbContext db, CurrentUser user, ScopeResolver scope, IClock clock, AuditWriter audit, NotificationSender notifier)
{
    private sealed record NewSession(int ClassId, int SubjectId, int TeacherId, DateOnly Date, int SlotId);

    private sealed class Plan
    {
        public required TimetableVersion Version { get; init; }
        public required Semester Semester { get; init; }
        public required string CampusName { get; init; }
        public required DateOnly From { get; init; }
        public required DateOnly To { get; init; }
        public required List<NewSession> NewSessions { get; init; }
        public required List<TimetableEntry> Entries { get; init; }
        public required List<int> ClassIds { get; init; }
        /// <summary>Ô của bản Nháp (lớp, thứ, khung giờ) đang trùng giáo viên.</summary>
        public HashSet<(int ClassId, int Day, int SlotId)> ConflictCells { get; } = [];
        public List<string> BlockingIssues { get; } = [];
        public List<TeacherConflictDto> Conflicts { get; } = [];
    }

    // ---------------------------------------------------------------- Xem trước
    /// <summary>Xem trước không được lưu lại: lúc bấm Phát hành hệ thống tính lại, nên con số cuối mới là con số ghi vào phiên bản.</summary>
    public async Task<PublishPreviewDto> PreviewAsync(int versionId)
    {
        var plan = await BuildPlanAsync(versionId);
        var deletable = DeletableSessions(plan.ClassIds, plan.From, plan.To);

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
            await ClassesWithoutLessonsAsync(plan),
            plan.Conflicts.Count == 0 && plan.BlockingIssues.Count == 0);
    }

    /// <summary>
    /// Lớp của (phân hiệu, năm học) vẫn không có tiết nào trong học kỳ sau Phát hành: cảnh báo, không chặn.
    /// Tiết còn lại sau Phát hành = tiết sẽ sinh + tiết không bị xóa (lớp ngoài phiên bản, tiết đã qua, tiết đã điểm danh).
    /// </summary>
    private async Task<List<string>> ClassesWithoutLessonsAsync(Plan plan)
    {
        var classes = await db.Class.AsNoTracking()
            .Where(c => c.CampusId == plan.Version.CampusId && c.AcademicYearId == plan.Semester.AcademicYearId && c.IsActive)
            .OrderBy(c => c.GradeLevel).ThenBy(c => c.Name).Select(c => new { c.ClassId, c.Name }).ToListAsync();
        var classIds = classes.Select(c => c.ClassId).ToList();
        var versionClassIds = plan.ClassIds;
        var keptClassIds = (await db.TeachingSession.AsNoTracking()
            .Where(s => classIds.Contains(s.ClassId) && s.SessionDate >= plan.Semester.StartDate && s.SessionDate <= plan.Semester.EndDate)
            .Where(s => !(versionClassIds.Contains(s.ClassId) && s.SessionDate >= plan.From && s.SessionDate <= plan.To && !s.AttendanceRecord.Any()))
            .Select(s => s.ClassId).Distinct().ToListAsync()).ToHashSet();
        var newClassIds = plan.NewSessions.Select(n => n.ClassId).ToHashSet();
        return classes.Where(c => !keptClassIds.Contains(c.ClassId) && !newClassIds.Contains(c.ClassId)).Select(c => c.Name).ToList();
    }

    // ---------------------------------------------------------------- Phát hành
    public async Task<PublishResultDto> PublishAsync(int versionId, PublishRequest request)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var plan = await BuildPlanAsync(versionId);
        if (plan.BlockingIssues.Count > 0)
            throw ApiException.BadRequest(string.Join(" ", plan.BlockingIssues));
        if (plan.Conflicts.Count > 0)
            throw ApiException.BadRequest($"Còn {plan.Conflicts.Count} xung đột giáo viên. Xung đột chặn Phát hành: sửa bản Nháp, hoặc nhờ Hiệu phó phân hiệu kia sửa lịch, rồi phát hành lại.");

        var campusId = plan.Version.CampusId;
        var deletable = DeletableSessions(plan.ClassIds, plan.From, plan.To);

        var needsConfirm = await deletable.AnyAsync(s => (s.Source == "Manual" && s.VersionId != null) || s.VersionId == null || s.Status == "Pending");
        if (needsConfirm && !request.ConfirmWarnings)
            throw ApiException.BadRequest("Phát hành sẽ xóa tiết Chỉnh tay, tiết bù hoặc tiết Chờ xử lý. Xem số lượng ở màn hình xem trước và xác nhận để tiếp tục.");

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
            new { campusId, semesterId = plan.Semester.SemesterId, classes = plan.ClassIds.Count, deleted, created = created.Count });

        // Một thông báo chung cho giáo viên có tiết trong phiên bản và học sinh của các lớp trong phiên bản (BR-26).
        var today = clock.Today;
        var students = await db.Enrollment.AsNoTracking()
            .Where(e => plan.ClassIds.Contains(e.ClassId) && e.StartDate <= today && (e.EndDate == null || e.EndDate >= today))
            .Select(e => e.StudentId).ToListAsync();
        var recipients = plan.Entries.Select(e => e.TeacherId).Concat(students).Distinct().ToList();
        notifier.Add(recipients, NotificationTypes.TimetablePublished, "Đã có thời khóa biểu mới",
            $"{plan.CampusName}, {plan.Semester.Name}: thời khóa biểu mới có hiệu lực từ {plan.From:dd/MM/yyyy}. Xem ở mục Lịch dạy.",
            "TimetableVersion", versionId);

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new PublishResultDto(versionId, deleted, created.Count, recipients.Count);
    }

    // ---------------------------------------------------------------- Dựng kế hoạch
    private IQueryable<TeachingSession> DeletableSessions(List<int> classIds, DateOnly from, DateOnly to) =>
        db.TeachingSession.Where(s => classIds.Contains(s.ClassId) && s.SessionDate >= from && s.SessionDate <= to
                                      && !s.AttendanceRecord.Any());

    private async Task<Plan> BuildPlanAsync(int versionId)
    {
        var version = await db.TimetableVersion.AsNoTracking()
            .Include(v => v.Campus).Include(v => v.Semester).ThenInclude(s => s.AcademicYear)
            .FirstOrDefaultAsync(v => v.VersionId == versionId) ?? throw ApiException.NotFound("Không tìm thấy phiên bản thời khóa biểu.");
        await scope.EnsureCanManageCampusAsync(version.CampusId);
        if (version.Status != "Draft")
            throw ApiException.BadRequest("Phiên bản này đã phát hành. Muốn đổi lịch thì tải mẫu điền sẵn, sửa rồi import thành phiên bản mới.");
        var classIds = await db.TimetableVersionClass.AsNoTracking().Where(c => c.VersionId == versionId).Select(c => c.ClassId).ToListAsync();

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
            NewSessions = newSessions, Entries = entries, ClassIds = classIds,
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
    /// BR-13: mỗi giáo viên tối đa một tiết Đã xếp lịch tại một (ngày, khung giờ) trên toàn trường; tiết Chờ xử lý để trống giáo viên nên không chiếm chỗ.
    /// Hai nguồn xung đột: (1) hai ô của chính bản Nháp cùng giáo viên, cùng (thứ, tiết); (2) tiết còn nguyên sau Phát hành —
    /// lớp ngoài phiên bản (mọi phân hiệu) và tiết đã có điểm danh. Tiết của lớp trong phiên bản sẽ bị xóa nên không chiếm chỗ.
    /// </summary>
    private async Task DetectConflictsAsync(Plan plan)
    {
        var campusId = plan.Version.CampusId;
        var teacherIds = plan.Entries.Select(e => e.TeacherId).Distinct().ToList();
        if (teacherIds.Count == 0) return;

        var occupied = await db.TeachingSession.AsNoTracking()
            .Where(s => s.TeacherId != null && teacherIds.Contains(s.TeacherId.Value) && s.SessionDate >= plan.From && s.SessionDate <= plan.To && s.Status == "Scheduled")
            .Where(s => !plan.ClassIds.Contains(s.ClassId) || s.AttendanceRecord.Any())
            .Select(s => new
            {
                TeacherId = s.TeacherId!.Value, s.SessionDate, s.PeriodSlotId, CampusId = s.Class.CampusId, CampusName = s.Class.Campus.Name,
                ClassName = s.Class.Name, Subject = s.Subject.Abbreviation,
            }).ToListAsync();
        var index = occupied.GroupBy(o => (o.TeacherId, o.SessionDate, o.PeriodSlotId)).ToDictionary(g => g.Key, g => g.First());

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
            plan.ConflictCells.Add((n.ClassId, vnDay, n.SlotId));
        }

        // Cùng một giáo viên ở hai lớp của chính bản Nháp: ô thứ hai trở đi được báo là bên mới, ô đầu là bên đang chiếm chỗ.
        var internalHits = plan.Entries.GroupBy(e => (e.TeacherId, e.DayOfWeek, e.PeriodSlotId)).Where(g => g.Count() > 1)
            .Select(g => g.OrderBy(e => e.Class.GradeLevel).ThenBy(e => e.Class.Name).ToList()).ToList();
        foreach (var g in internalHits)
            foreach (var e in g) plan.ConflictCells.Add((e.ClassId, e.DayOfWeek, e.PeriodSlotId));
        if (hits.Count == 0 && internalHits.Count == 0) return;

        // Mỗi dòng xung đột ghi rõ phân hiệu đang chiếm chỗ và ai là hiệu phó phụ trách phân hiệu đó (BR-14).
        var occCampusIds = hits.Keys.Select(k => k.OccCampus).Append(campusId).Distinct().ToList();
        var today = clock.Today;
        var vices = await db.CampusLeadership.AsNoTracking().Where(EffectivePeriod.EffectiveAt<CampusLeadership>(today))
            .Where(l => occCampusIds.Contains(l.CampusId))
            .Select(l => new { l.CampusId, Name = l.Staff.Staff.FullName }).ToListAsync();
        var campusNames = occupied.GroupBy(o => o.CampusId).ToDictionary(g => g.Key, g => g.First().CampusName);
        var teachers = plan.Entries.Select(e => e.Teacher).DistinctBy(t => t.StaffId).ToDictionary(t => t.StaffId);
        var slots = plan.Entries.Select(e => e.PeriodSlot).DistinctBy(p => p.PeriodSlotId).ToDictionary(p => p.PeriodSlotId);

        foreach (var g in internalHits)
        {
            var first = g[0];
            foreach (var e in g.Skip(1))
            {
                var dates = plan.NewSessions.Where(n => n.ClassId == e.ClassId && n.SlotId == e.PeriodSlotId
                                                        && SchoolCalendarRules.ToVnDay(n.Date.DayOfWeek) == e.DayOfWeek).Select(n => n.Date).OrderBy(d => d).ToList();
                plan.Conflicts.Add(new TeacherConflictDto(
                    e.Teacher.Staff.FullName, e.Teacher.TeacherAbbr ?? "", e.DayOfWeek, e.PeriodSlot.SessionOfDay, e.PeriodSlot.PeriodNo, e.Class.Name,
                    plan.CampusName, first.Class.Name, first.Subject.Abbreviation,
                    vices.FirstOrDefault(v => v.CampusId == campusId)?.Name,
                    dates.Count, dates.Count > 0 ? dates[0] : plan.From, dates.Count > 0 ? dates[^1] : plan.From));
            }
        }

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

    /// <summary>
    /// Xung đột của một bản Nháp, dùng cho lưới sửa nháp: danh sách xung đột và các ô (lớp, thứ, khung giờ) đang trùng.
    /// Người gọi không có quyền sửa hoặc học kỳ đã kết thúc thì trả rỗng — lưới vẫn xem được.
    /// </summary>
    public async Task<(IReadOnlyList<TeacherConflictDto> Conflicts, HashSet<(int ClassId, int Day, int SlotId)> Cells)> DraftConflictsAsync(int versionId)
    {
        try
        {
            var plan = await BuildPlanAsync(versionId);
            return (plan.Conflicts, plan.ConflictCells);
        }
        catch (ApiException)
        {
            return ([], []);
        }
    }
}
