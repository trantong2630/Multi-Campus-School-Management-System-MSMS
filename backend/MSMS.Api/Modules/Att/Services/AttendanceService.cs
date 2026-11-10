using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Att.Dtos;

namespace MSMS.Api.Modules.Att.Services;

/// <summary>
/// ATT-01 Điểm danh theo tiết (BR-17, BR-18).
/// Kiểm quyền hai tầng: policy ở controller chặn loại tài khoản; service kiểm đối tượng có nằm trong phạm vi người gọi (BR-01).
/// Nhãn có phép/không phép không lưu, luôn tính lúc đọc qua <see cref="AttendanceRules"/>.
/// Điểm danh không ghi Audit Log; người nhập, thời điểm và cờ nhập bù lưu ngay trên bản ghi (BR-28, 4.5.9).
/// </summary>
public sealed class AttendanceService(MsmsDbContext db, CurrentUser user, ScopeResolver scope, AccountScopeResolver accountScope, IClock clock)
{
    private const int MaxRangeDays = 62;

    // ---------------------------------------------------------------- 4.5.1 Tiết hôm nay của giáo viên

    /// <summary>4.5.1: các tiết giáo viên dạy hôm nay (giờ VN do Backend quyết định) kèm tình trạng đã điểm danh.</summary>
    public async Task<List<AttendanceSessionDto>> TodayAsync()
    {
        var today = clock.Today;
        var sessions = await db.TeachingSession.AsNoTracking()
            .Where(s => s.TeacherId == user.PersonId && s.SessionDate == today)
            .ToListAsync();
        return await ProjectSessionsAsync(sessions.Select(s => s.SessionId).ToList());
    }

    // ---------------------------------------------------------------- Danh sách lớp và tiết theo lớp (4.5.6)

    /// <summary>Lớp năm học hiện hành mà người gọi xem được điểm danh: SA, HT toàn trường; HP phân hiệu mình; GV lớp chủ nhiệm.</summary>
    public async Task<List<AttendanceClassOptionDto>> ClassesAsync()
    {
        var homeroom = await HomeroomClassIdsAsync();
        var query = db.Class.AsNoTracking().Where(c => c.IsActive && c.AcademicYear.IsCurrent);
        switch (user.AccountType)
        {
            case AccountTypes.SchoolAdmin or AccountTypes.Principal:
                break;
            case AccountTypes.VicePrincipal:
                var led = await scope.LedCampusIdsAsync();
                query = query.Where(c => led.Contains(c.CampusId));
                break;
            case AccountTypes.Teacher:
                query = db.Class.AsNoTracking().Where(c => homeroom.Contains(c.ClassId));
                break;
            default:
                throw ApiException.Forbidden("Không có quyền xem điểm danh theo lớp.");
        }
        var rows = await query.OrderBy(c => c.Campus.Name).ThenBy(c => c.GradeLevel).ThenBy(c => c.Name)
            .Select(c => new { c.ClassId, c.Name, c.CampusId, CampusName = c.Campus.Name })
            .ToListAsync();
        return rows.Select(c => new AttendanceClassOptionDto(c.ClassId, c.Name, c.CampusId, c.CampusName, homeroom.Contains(c.ClassId))).ToList();
    }

    /// <summary>
    /// 4.5.6: các tiết của một lớp trong khoảng ngày; <paramref name="onlyMissing"/> chỉ lấy tiết Đã xếp lịch, ngày nhỏ hơn hôm nay,
    /// chưa có bản ghi điểm danh nào — School Admin dùng để tìm tiết cần nhập bù.
    /// </summary>
    public async Task<List<AttendanceSessionDto>> ClassSessionsAsync(int classId, DateOnly from, DateOnly to, bool onlyMissing)
    {
        CheckRange(from, to);
        await EnsureCanReadClassAsync(classId);
        var today = clock.Today;
        var query = db.TeachingSession.AsNoTracking().Where(s => s.ClassId == classId && s.SessionDate >= from && s.SessionDate <= to);
        if (onlyMissing)
            query = query.Where(s => s.Status == AttendanceRules.SessionScheduled && s.SessionDate < today && !s.AttendanceRecord.Any());
        var ids = await query.Select(s => s.SessionId).ToListAsync();
        return await ProjectSessionsAsync(ids);
    }

    // ---------------------------------------------------------------- 4.5.2 Bảng điểm danh của tiết

    /// <summary>4.5.2: danh sách học sinh có ghi danh hiệu lực ở lớp VÀO NGÀY CỦA TIẾT (BR-17), kèm bản ghi đã có.</summary>
    public async Task<AttendanceSheetDto> GetSheetAsync(long sessionId)
    {
        var session = await LoadSessionAsync(sessionId);
        await EnsureCanReadSessionAsync(session);
        return await BuildSheetAsync(session);
    }

    // ---------------------------------------------------------------- 4.5.3, 4.5.4, 4.5.5, 4.5.7, 4.5.8, 4.5.9 Ghi điểm danh

    /// <summary>
    /// Ghi hoặc sửa điểm danh của một tiết. Mỗi học sinh đúng một bản ghi mỗi tiết; lưu lại là cập nhật bản ghi đó, hiệu lực ngay (BR-17).
    /// - Giáo viên của tiết: tiết Đã xếp lịch, đúng ngày của tiết (4.5.3, 4.5.4).
    /// - School Admin: nhập bù hoặc sửa tiết Đã xếp lịch có ngày ≤ hôm nay; bản ghi mới mang cờ nhập bù (4.5.7, 4.5.8).
    /// - Mọi trường hợp khác bị chặn ở đây, không dựa vào giao diện (4.5.5).
    /// </summary>
    public async Task<AttendanceSheetDto> SaveAsync(long sessionId, SaveAttendanceRequest r)
    {
        var session = await LoadSessionAsync(sessionId);
        var isAdmin = user.AccountType == AccountTypes.SchoolAdmin;
        var today = clock.Today;
        var block = AttendanceRules.WriteBlockReason(user.AccountType, session.Status, session.TeacherId, session.SessionDate, user.PersonId, today);
        // Tiết Chờ xử lý / Đã hủy: báo đúng điều kiện nghiệp vụ trước (BR-16). Sau đó mới tới phạm vi: không phải giáo viên của tiết → 403.
        if (block is not null && session.Status != AttendanceRules.SessionScheduled) throw ApiException.BadRequest(block);
        if (!isAdmin && session.TeacherId != user.PersonId)
            throw ApiException.Forbidden("Chỉ giáo viên của tiết mới điểm danh được tiết này.");
        if (block is not null) throw ApiException.BadRequest(block);

        if (r.Items is null || r.Items.Count == 0) throw ApiException.BadRequest("Chưa có học sinh nào được điểm danh. Chọn trạng thái cho học sinh rồi lưu.");
        if (r.Items.Select(i => i.StudentId).Distinct().Count() != r.Items.Count)
            throw ApiException.BadRequest("Một học sinh xuất hiện hai lần trong bảng. Tải lại bảng điểm danh rồi lưu lại.");

        var roster = (await db.Enrollment.AsNoTracking().RosterAt(session.ClassId, session.SessionDate)
            .Select(e => e.StudentId).ToListAsync()).ToHashSet();
        var items = new List<(int StudentId, string Status, string? Note)>();
        foreach (var i in r.Items)
        {
            if (!roster.Contains(i.StudentId))
                throw ApiException.BadRequest("Có học sinh không thuộc lớp vào ngày của tiết. Tải lại bảng điểm danh rồi lưu lại.");
            if (!AttendanceStatuses.All.Contains(i.Status))
                throw ApiException.BadRequest("Trạng thái điểm danh chỉ nhận Có mặt, Muộn hoặc Vắng.");
            items.Add((i.StudentId, i.Status, Guard.Optional(i.Note, "Ghi chú", 255)));
        }

        var now = clock.Now;
        var existing = await db.AttendanceRecord.Where(a => a.SessionId == sessionId).ToDictionaryAsync(a => a.StudentId);
        foreach (var (studentId, status, note) in items)
        {
            if (existing.TryGetValue(studentId, out var rec))
            {
                if (rec.Status == status && rec.Note == note) continue;
                rec.Status = status;
                rec.Note = note;
                rec.UpdatedByPersonId = user.PersonId;
                rec.UpdatedAt = now;
            }
            else
            {
                db.AttendanceRecord.Add(new AttendanceRecord
                {
                    SessionId = sessionId, StudentId = studentId, Status = status, Note = note,
                    IsBackfilled = isAdmin,   // 4.5.9: School Admin nhập thay giáo viên luôn là nhập bù
                    RecordedByPersonId = user.PersonId, RecordedAt = now,
                });
            }
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // UQ_Attendance (SessionId, StudentId): hai người lưu cùng lúc.
            throw ApiException.Conflict("Bảng điểm danh vừa được lưu từ nơi khác. Tải lại bảng rồi lưu lại.");
        }

        return await BuildSheetAsync(session);
    }

    // ---------------------------------------------------------------- 4.5.11, 4.5.12 Chuyên cần của học sinh

    /// <summary>4.5.11: học sinh xem chuyên cần của mình.</summary>
    public Task<StudentAttendanceDto> MineAsync(DateOnly from, DateOnly to) => StudentAttendanceAsync(user.PersonId, from, to);

    /// <summary>4.5.12: phụ huynh xem chuyên cần của con được liên kết; con không thuộc phụ huynh thì bị từ chối.</summary>
    public async Task<StudentAttendanceDto> ChildAsync(int studentId, DateOnly from, DateOnly to)
    {
        if (!await db.ParentStudentLink.AnyAsync(l => l.ParentId == user.PersonId && l.StudentId == studentId))
            throw ApiException.Forbidden("Học sinh này không được liên kết với tài khoản phụ huynh này.");
        return await StudentAttendanceAsync(studentId, from, to);
    }

    private async Task<StudentAttendanceDto> StudentAttendanceAsync(int studentId, DateOnly from, DateOnly to)
    {
        CheckRange(from, to);
        var today = clock.Today;
        var person = await db.Person.AsNoTracking().Where(p => p.PersonId == studentId)
            .Select(p => new { p.FullName, p.PersonCode }).FirstOrDefaultAsync()
            ?? throw ApiException.NotFound("Không tìm thấy học sinh.");

        // Tiết của lớp mà học sinh có ghi danh hiệu lực vào ngày của tiết; tiết Đã hủy, Chờ xử lý không vào chuyên cần.
        var sessions = await db.TeachingSession.AsNoTracking()
            .Where(s => s.SessionDate >= from && s.SessionDate <= to && s.Status == AttendanceRules.SessionScheduled
                && s.Class.Enrollment.Any(e => e.StudentId == studentId && e.StartDate <= s.SessionDate
                    && (e.EndDate ?? e.Class.AcademicYear.EndDate) >= s.SessionDate))
            .OrderBy(s => s.SessionDate).ThenBy(s => s.PeriodSlot.StartTime)
            .Select(s => new
            {
                s.SessionId, s.SessionDate, s.PeriodSlotId, s.PeriodSlot.SessionOfDay, s.PeriodSlot.PeriodNo,
                ClassName = s.Class.Name, s.SubjectId, SubjectName = s.Subject.Name,
                TeacherName = s.Teacher == null ? null : s.Teacher.Staff.FullName,
                Record = s.AttendanceRecord.Where(a => a.StudentId == studentId).Select(a => new { a.Status, a.Note }).FirstOrDefault(),
            })
            .ToListAsync();

        var leaves = await ApprovedLeavesAsync([studentId], from, to);
        var items = sessions.Select(s => new StudentAttendanceItemDto(
            s.SessionId, s.SessionDate, s.PeriodSlotId, s.SessionOfDay, s.PeriodNo, s.ClassName, s.SubjectId, s.SubjectName, s.TeacherName,
            AttendanceRules.Label(s.Record?.Status, AttendanceRules.IsExcused(studentId, s.SessionDate, s.SessionOfDay, s.PeriodNo, leaves)),
            s.Record?.Note)).ToList();

        var counts = Count(items.Where(i => i.Date <= today).Select(i => i.Label));
        return new StudentAttendanceDto(studentId, person.FullName, person.PersonCode, counts, items);
    }

    // ---------------------------------------------------------------- 4.5.13 Chuyên cần của lớp

    /// <summary>
    /// 4.5.13: chuyên cần của lớp theo ngày hoặc khoảng ngày. Chủ nhiệm xem lớp mình; SA, HT, HP (phân hiệu mình) xem số liệu.
    /// Chỉ tính tiết Đã xếp lịch; tiết sau hôm nay không vào thống kê.
    /// </summary>
    public async Task<ClassAttendanceDto> ClassSummaryAsync(int classId, DateOnly from, DateOnly to)
    {
        CheckRange(from, to);
        await EnsureCanReadClassAsync(classId);
        var today = clock.Today;
        var className = await db.Class.AsNoTracking().Where(c => c.ClassId == classId).Select(c => c.Name).FirstAsync();

        var sessionIds = await db.TeachingSession.AsNoTracking()
            .Where(s => s.ClassId == classId && s.SessionDate >= from && s.SessionDate <= to && s.Status == AttendanceRules.SessionScheduled)
            .Select(s => s.SessionId).ToListAsync();
        var sessions = await ProjectSessionsAsync(sessionIds);

        var enrollments = await EnrollmentsOverlappingAsync(classId, from, to);
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var people = await db.Person.AsNoTracking().Where(p => studentIds.Contains(p.PersonId))
            .Select(p => new { p.PersonId, p.FullName, p.PersonCode }).ToListAsync();
        var records = await db.AttendanceRecord.AsNoTracking().Where(a => sessionIds.Contains(a.SessionId))
            .Select(a => new { a.SessionId, a.StudentId, a.Status }).ToListAsync();
        var recordMap = records.ToDictionary(a => (a.SessionId, a.StudentId), a => a.Status);
        var leaves = await ApprovedLeavesAsync(studentIds, from, to);

        var students = people.OrderBy(p => LastName(p.FullName)).ThenBy(p => p.FullName).Select(p =>
        {
            var labels = new Dictionary<long, string>();
            foreach (var s in sessions)
            {
                if (!enrollments.Any(e => e.StudentId == p.PersonId && e.Start <= s.Date && e.End >= s.Date)) continue;
                recordMap.TryGetValue((s.SessionId, p.PersonId), out var status);
                labels[s.SessionId] = AttendanceRules.Label(status,
                    AttendanceRules.IsExcused(p.PersonId, s.Date, s.SessionOfDay, s.PeriodNo, leaves));
            }
            var counted = sessions.Where(s => s.Date <= today && labels.ContainsKey(s.SessionId)).Select(s => labels[s.SessionId]);
            return new ClassAttendanceStudentDto(p.PersonId, p.PersonCode, p.FullName, Count(counted), labels);
        }).ToList();

        return new ClassAttendanceDto(classId, className, from, to, sessions, students);
    }

    // ---------------------------------------------------------------- 4.9.10 Dữ liệu xuất điểm danh (ADM-03)

    /// <summary>
    /// 4.9.10: bản ghi điểm danh của lớp trong khoảng ngày, kèm nhãn cuối suy ra (BR-18, spec 5.2).
    /// Chỉ chủ nhiệm (lớp đang chủ nhiệm) và Hiệu phó (lớp thuộc phân hiệu phụ trách); School Admin và Hiệu trưởng không xuất.
    /// Gồm tiết Đã xếp lịch tới hôm nay; tiết đã qua mà học sinh chưa có bản ghi thì hiện Chưa điểm danh.
    /// </summary>
    public async Task<AttendanceExportDto> ExportAsync(int classId, DateOnly from, DateOnly to)
    {
        ExportRules.CheckRange(from, to);
        var cls = await db.Class.AsNoTracking().Where(c => c.ClassId == classId)
            .Select(c => new { c.Name, c.CampusId, CampusName = c.Campus.Name }).FirstOrDefaultAsync()
            ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        var allowed = user.AccountType switch
        {
            AccountTypes.VicePrincipal => (await scope.LedCampusIdsAsync()).Contains(cls.CampusId),
            AccountTypes.Teacher => (await HomeroomClassIdsAsync()).Contains(classId),
            _ => false,
        };
        if (!allowed) throw ApiException.Forbidden("Chỉ xuất được điểm danh của lớp đang chủ nhiệm hoặc lớp thuộc phân hiệu đang phụ trách.");

        var today = clock.Today;
        var until = to < today ? to : today;
        var sessions = await db.TeachingSession.AsNoTracking()
            .Where(s => s.ClassId == classId && s.SessionDate >= from && s.SessionDate <= until && s.Status == AttendanceRules.SessionScheduled)
            .OrderBy(s => s.SessionDate).ThenBy(s => s.PeriodSlot.StartTime)
            .Select(s => new
            {
                s.SessionId, s.SessionDate, s.PeriodSlot.SessionOfDay, s.PeriodSlot.PeriodNo, SubjectName = s.Subject.Name,
                TeacherName = s.Teacher == null ? null : s.Teacher.Staff.FullName,
            })
            .ToListAsync();
        if (sessions.Count == 0) return new AttendanceExportDto(cls.Name, cls.CampusName, from, to, []);

        var sessionIds = sessions.Select(s => s.SessionId).ToList();
        var enrollments = await EnrollmentsOverlappingAsync(classId, from, until);
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var people = (await db.Person.AsNoTracking().Where(p => studentIds.Contains(p.PersonId))
                .Select(p => new { p.PersonId, p.FullName, p.PersonCode }).ToListAsync())
            .OrderBy(p => LastName(p.FullName)).ThenBy(p => p.FullName).ToList();
        var records = await db.AttendanceRecord.AsNoTracking().Where(a => sessionIds.Contains(a.SessionId))
            .Select(a => new { a.SessionId, a.StudentId, a.Status, a.Note }).ToListAsync();
        var recordMap = records.ToDictionary(a => (a.SessionId, a.StudentId));
        var leaves = await ApprovedLeavesAsync(studentIds, from, until);

        var rows = new List<AttendanceExportRowDto>();
        foreach (var s in sessions)
        {
            foreach (var p in people)
            {
                // Chỉ học sinh có ghi danh hiệu lực ở lớp vào ngày của tiết (BR-17).
                if (!enrollments.Any(e => e.StudentId == p.PersonId && e.Start <= s.SessionDate && e.End >= s.SessionDate)) continue;
                recordMap.TryGetValue((s.SessionId, p.PersonId), out var rec);
                var label = AttendanceRules.Label(rec?.Status,
                    AttendanceRules.IsExcused(p.PersonId, s.SessionDate, s.SessionOfDay, s.PeriodNo, leaves));
                rows.Add(new AttendanceExportRowDto(s.SessionDate, s.SessionOfDay, s.PeriodNo, s.SubjectName, s.TeacherName,
                    p.PersonCode, p.FullName, label, rec?.Note));
            }
        }
        return new AttendanceExportDto(cls.Name, cls.CampusName, from, to, rows);
    }

    // ---------------------------------------------------------------- Dựng dữ liệu

    private async Task<AttendanceSheetDto> BuildSheetAsync(TeachingSession session)
    {
        var today = clock.Today;
        var students = await db.Enrollment.AsNoTracking().RosterAt(session.ClassId, session.SessionDate)
            .Select(e => new { e.StudentId, e.Student.Student.FullName, e.Student.Student.PersonCode })
            .ToListAsync();
        var records = await db.AttendanceRecord.AsNoTracking().Where(a => a.SessionId == session.SessionId)
            .Select(a => new
            {
                a.StudentId, a.Status, a.Note, a.IsBackfilled, a.RecordedAt, a.UpdatedAt,
                RecordedBy = a.RecordedByPerson.FullName,
                UpdatedBy = a.UpdatedByPerson == null ? null : a.UpdatedByPerson.FullName,
            })
            .ToDictionaryAsync(a => a.StudentId);
        var slot = await db.PeriodSlot.AsNoTracking().FirstAsync(p => p.PeriodSlotId == session.PeriodSlotId);
        var leaves = await ApprovedLeavesAsync(students.Select(s => s.StudentId).ToList(), session.SessionDate, session.SessionDate);

        var rows = students.Select(s =>
        {
            records.TryGetValue(s.StudentId, out var rec);
            var label = AttendanceRules.Label(rec?.Status, AttendanceRules.IsExcused(s.StudentId, session.SessionDate, slot.SessionOfDay, slot.PeriodNo, leaves));
            return new AttendanceSheetRowDto(s.StudentId, s.PersonCode, s.FullName, rec?.Status, label, rec?.Note, rec?.IsBackfilled ?? false,
                rec?.RecordedBy, rec?.RecordedAt, rec?.UpdatedBy, rec?.UpdatedAt);
        }).OrderBy(r => LastName(r.FullName)).ThenBy(r => r.FullName).ToList();

        var dto = (await ProjectSessionsAsync([session.SessionId])).Single();
        var readOnly = AttendanceRules.WriteBlockReason(user.AccountType, session.Status, session.TeacherId, session.SessionDate, user.PersonId, today);
        var counts = session.Status == AttendanceRules.SessionScheduled ? Count(rows.Select(r => r.Label)) : new AttendanceCountsDto(0, 0, 0, 0, 0);
        return new AttendanceSheetDto(dto, user.AccountType == AccountTypes.SchoolAdmin && readOnly is null, readOnly, counts, rows);
    }

    /// <summary>Tiết kèm sĩ số tại ngày của tiết và số học sinh đã có bản ghi.</summary>
    private async Task<List<AttendanceSessionDto>> ProjectSessionsAsync(IReadOnlyCollection<long> sessionIds)
    {
        if (sessionIds.Count == 0) return [];
        var today = clock.Today;
        var rows = await db.TeachingSession.AsNoTracking().Where(s => sessionIds.Contains(s.SessionId))
            .OrderBy(s => s.SessionDate).ThenBy(s => s.PeriodSlot.StartTime).ThenBy(s => s.Class.Name)
            .Select(s => new
            {
                s.SessionId, s.SessionDate, s.PeriodSlotId, s.PeriodSlot.SessionOfDay, s.PeriodSlot.PeriodNo,
                s.PeriodSlot.StartTime, s.PeriodSlot.EndTime, s.ClassId, ClassName = s.Class.Name, CampusName = s.Class.Campus.Name,
                s.SubjectId, SubjectName = s.Subject.Name, TeacherName = s.Teacher == null ? null : s.Teacher.Staff.FullName,
                s.TeacherId, s.Status, Marked = s.AttendanceRecord.Count(),
            })
            .ToListAsync();

        var classIds = rows.Select(r => r.ClassId).Distinct().ToList();
        var from = rows.Min(r => r.SessionDate);
        var to = rows.Max(r => r.SessionDate);
        var enrollments = await db.Enrollment.AsNoTracking()
            .Where(e => classIds.Contains(e.ClassId) && e.StartDate <= to && (e.EndDate ?? e.Class.AcademicYear.EndDate) >= from)
            .Select(e => new { e.ClassId, e.StartDate, End = e.EndDate ?? e.Class.AcademicYear.EndDate })
            .ToListAsync();

        return rows.Select(r => new AttendanceSessionDto(
            r.SessionId, r.SessionDate, r.PeriodSlotId, r.SessionOfDay, r.PeriodNo, r.StartTime, r.EndTime, r.ClassId, r.ClassName, r.CampusName,
            r.SubjectId, r.SubjectName, r.TeacherName, r.Status,
            enrollments.Count(e => e.ClassId == r.ClassId && e.StartDate <= r.SessionDate && e.End >= r.SessionDate),
            r.Marked,
            r.Status == AttendanceRules.SessionScheduled && r.SessionDate < today && r.Marked == 0,
            AttendanceRules.WriteBlockReason(user.AccountType, r.Status, r.TeacherId, r.SessionDate, user.PersonId, today) is null
        )).ToList();
    }

    private async Task<List<(int StudentId, DateOnly Start, DateOnly End)>> EnrollmentsOverlappingAsync(int classId, DateOnly from, DateOnly to)
    {
        var rows = await db.Enrollment.AsNoTracking()
            .Where(e => e.ClassId == classId && e.StartDate <= to && (e.EndDate ?? e.Class.AcademicYear.EndDate) >= from)
            .Select(e => new { e.StudentId, e.StartDate, End = e.EndDate ?? e.Class.AcademicYear.EndDate })
            .ToListAsync();
        return rows.Select(e => (e.StudentId, e.StartDate, e.End)).ToList();
    }

    /// <summary>Đơn Đã duyệt phủ khoảng ngày; đơn duyệt muộn tự đổi nhãn vì nhãn luôn tính lúc đọc (BR-18).</summary>
    private async Task<List<ApprovedLeave>> ApprovedLeavesAsync(IReadOnlyCollection<int> studentIds, DateOnly from, DateOnly to)
    {
        if (studentIds.Count == 0) return [];
        return await db.LeaveRequest.AsNoTracking()
            .Where(l => l.Status == AttendanceRules.LeaveApproved && studentIds.Contains(l.StudentId) && l.FromDate <= to && l.ToDate >= from)
            .Select(l => new ApprovedLeave(l.StudentId, l.FromDate, l.ToDate, l.SessionOfDay, l.SlotIds))
            .ToListAsync();
    }

    private static AttendanceCountsDto Count(IEnumerable<string> labels)
    {
        int present = 0, late = 0, excused = 0, unexcused = 0, notMarked = 0;
        foreach (var l in labels)
        {
            switch (l)
            {
                case AttendanceLabels.Present: present++; break;
                case AttendanceLabels.Late: late++; break;
                case AttendanceLabels.ExcusedAbsent: excused++; break;
                case AttendanceLabels.UnexcusedAbsent: unexcused++; break;
                default: notMarked++; break;
            }
        }
        return new AttendanceCountsDto(present, late, excused, unexcused, notMarked);
    }

    // ---------------------------------------------------------------- Phạm vi (tầng 2)

    private async Task<TeachingSession> LoadSessionAsync(long sessionId) =>
        await db.TeachingSession.AsNoTracking().Include(s => s.Class).FirstOrDefaultAsync(s => s.SessionId == sessionId)
        ?? throw ApiException.NotFound("Không tìm thấy tiết học.");

    private async Task<IReadOnlyList<int>> HomeroomClassIdsAsync() =>
        user.AccountType == AccountTypes.Teacher
            ? (await accountScope.ResolveAsync(user.PersonId, user.AccountType)).HomeroomClassIds
            : [];

    /// <summary>Đọc bảng của tiết: SA, HT mọi tiết; HP tiết thuộc phân hiệu mình; GV tiết mình dạy hoặc tiết của lớp chủ nhiệm.</summary>
    private async Task EnsureCanReadSessionAsync(TeachingSession s)
    {
        var allowed = user.AccountType switch
        {
            AccountTypes.SchoolAdmin or AccountTypes.Principal => true,
            AccountTypes.VicePrincipal => (await scope.LedCampusIdsAsync()).Contains(s.Class.CampusId),
            AccountTypes.Teacher => s.TeacherId == user.PersonId || (await HomeroomClassIdsAsync()).Contains(s.ClassId),
            _ => false,
        };
        if (!allowed) throw ApiException.Forbidden("Không có quyền xem điểm danh của tiết này.");
    }

    /// <summary>Đọc số liệu của lớp: SA, HT mọi lớp; HP lớp thuộc phân hiệu mình; GV lớp đang chủ nhiệm tại hôm nay.</summary>
    private async Task EnsureCanReadClassAsync(int classId)
    {
        var campusId = await db.Class.AsNoTracking().Where(c => c.ClassId == classId).Select(c => (int?)c.CampusId).FirstOrDefaultAsync()
            ?? throw ApiException.NotFound("Không tìm thấy lớp.");
        var allowed = user.AccountType switch
        {
            AccountTypes.SchoolAdmin or AccountTypes.Principal => true,
            AccountTypes.VicePrincipal => (await scope.LedCampusIdsAsync()).Contains(campusId),
            AccountTypes.Teacher => (await HomeroomClassIdsAsync()).Contains(classId),
            _ => false,
        };
        if (!allowed) throw ApiException.Forbidden("Không có quyền xem chuyên cần của lớp này.");
    }

    private static void CheckRange(DateOnly from, DateOnly to)
    {
        if (to < from) throw ApiException.BadRequest("Ngày kết thúc phải sau ngày bắt đầu.");
        if (to.DayNumber - from.DayNumber > MaxRangeDays) throw ApiException.BadRequest($"Chỉ xem tối đa {MaxRangeDays} ngày mỗi lần. Chọn khoảng ngắn hơn.");
    }

    /// <summary>Sắp theo tên (chữ cuối) như sổ điểm danh của trường.</summary>
    private static string LastName(string fullName) => fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? fullName;
}
