using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data;
using MSMS.Api.Modules.Adm.Dtos;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>Một tiết bị ảnh hưởng kèm khóa để gom nhóm (lớp, tổ của môn, giáo viên vắng).</summary>
internal sealed record AffectedSession(AffectedSessionRow Row, int ClassId, int DepartmentId, string DepartmentName, int? AbsentTeacherId, int? TeacherId);

/// <summary>Một bài rèn luyện đã giao, chỉ số liệu tổng hợp: số người nhận và số đã nộp.</summary>
internal sealed record AssignmentTally(int AssignmentId, int ClassId, int GradeLevel, int SubjectId, string SubjectName, int DepartmentId, int AssignedBy,
    DateTime OpenAt, DateTime DueAt, int Recipients, int Submitted);

/// <summary>Tiết bị ảnh hưởng và bài rèn luyện cho các màn Tình hình. Chỉ đọc.</summary>
internal static class OperationsSupport
{
    public static class Outcomes
    {
        public const string Substituted = "Substituted";
        public const string Cancelled = "Cancelled";
        public const string Pending = "Pending";
    }

    /// <summary>
    /// Tiết trong khoảng ngày bị ảnh hưởng: tiết gắn với một lần giáo viên vắng (dạy thay, hủy hoặc còn Chờ xử lý) và tiết đã hủy vì lý do khác.
    /// Không tính tiết hủy rơi vào ngày nghỉ của trường hoặc của phân hiệu: đó là lịch nghỉ có kế hoạch, và số liệu không được phụ thuộc
    /// việc ngày nghỉ khai trước hay sau khi phát hành thời khóa biểu. Xếp theo ngày, tiết.
    /// </summary>
    public static async Task<List<AffectedSession>> AffectedAsync(MsmsDbContext db, List<int> classIds, DateOnly from, DateOnly to, List<int>? subjectIds = null)
    {
        if (classIds.Count == 0) return [];
        var q = db.TeachingSession.AsNoTracking()
            .Where(s => classIds.Contains(s.ClassId) && s.SessionDate >= from && s.SessionDate <= to
                && (s.TeacherAbsenceId != null || s.Status == Outcomes.Cancelled)
                && !(s.Status == Outcomes.Cancelled && db.Holiday.Any(h => h.HolidayDate == s.SessionDate
                    && h.AcademicYearId == s.Class.AcademicYearId && (h.CampusId == null || h.CampusId == s.Class.CampusId))));
        if (subjectIds is not null) q = q.Where(s => subjectIds.Contains(s.SubjectId));
        var rows = await q
            .Select(s => new
            {
                s.SessionId, s.SessionDate, s.PeriodSlot.SessionOfDay, s.PeriodSlot.PeriodNo, s.PeriodSlot.StartTime,
                s.ClassId, ClassName = s.Class.Name, SubjectName = s.Subject.Name, s.Subject.DepartmentId, DepartmentName = s.Subject.Department.Name,
                AbsentTeacherId = s.TeacherAbsence == null ? (int?)null : s.TeacherAbsence.TeacherId,
                AbsentTeacher = s.TeacherAbsence == null ? null : s.TeacherAbsence.Teacher.Staff.FullName,
                s.TeacherId, Teacher = s.Teacher == null ? null : s.Teacher.Staff.FullName, s.Status,
            }).ToListAsync();
        return rows
            .OrderBy(r => r.SessionDate).ThenBy(r => r.StartTime)
            .Select(r =>
            {
                var outcome = r.Status switch { Outcomes.Pending => Outcomes.Pending, Outcomes.Cancelled => Outcomes.Cancelled, _ => Outcomes.Substituted };
                var teacher = outcome == Outcomes.Substituted && r.TeacherId != r.AbsentTeacherId ? r.Teacher : null;
                return new AffectedSession(
                    new AffectedSessionRow(r.SessionId, r.SessionDate, r.SessionOfDay, r.PeriodNo, r.StartTime, r.ClassName, r.SubjectName, r.AbsentTeacher, teacher, outcome),
                    r.ClassId, r.DepartmentId, r.DepartmentName, r.AbsentTeacherId, r.TeacherId);
            }).ToList();
    }

    /// <summary>Số tiết bị ảnh hưởng ở dạng Kpi: kỳ 4 tuần hiện tại, kỳ liền trước và 8 tuần.</summary>
    public static Kpi AffectedKpi(IReadOnlyCollection<AffectedSession> sessions, DateOnly today, IReadOnlyList<DateOnly> weekStarts)
    {
        var (current, previous) = DashboardSupport.Periods(today);
        var byWeek = sessions.GroupBy(s => DashboardSupport.WeekStart(s.Row.Date)).ToDictionary(g => g.Key, g => g.Count());
        return new Kpi(sessions.Count(s => s.Row.Date >= current && s.Row.Date <= today),
            sessions.Count(s => s.Row.Date >= previous && s.Row.Date < current),
            weekStarts.Select(w => (double?)byWeek.GetValueOrDefault(w)).ToList());
    }

    /// <summary>Bài rèn luyện của các lớp mở từ ngày from hoặc hết hạn từ ngày from trở đi (kể cả bài chưa hết hạn).</summary>
    public static async Task<List<AssignmentTally>> AssignmentsAsync(MsmsDbContext db, List<int> classIds, DateTime from, List<int>? subjectIds = null)
    {
        if (classIds.Count == 0) return [];
        var q = db.Assignment.AsNoTracking().Where(a => classIds.Contains(a.ClassId) && (a.DueAt >= from || a.OpenAt >= from));
        if (subjectIds is not null) q = q.Where(a => subjectIds.Contains(a.SubjectId));
        return await q.Select(a => new AssignmentTally(a.AssignmentId, a.ClassId, a.Class.GradeLevel, a.SubjectId, a.Subject.Name, a.Subject.DepartmentId,
                a.AssignedByPersonId, a.OpenAt, a.DueAt, a.AssignmentRecipient.Count, a.Submission.Count(s => s.SubmittedAt != null)))
            .ToListAsync();
    }

    /// <summary>Tỷ lệ nộp của các bài đã hết hạn nộp trong [from, until]: tổng bài đã nộp trên tổng người nhận.</summary>
    public static (int Count, double? Rate) Submission(IEnumerable<AssignmentTally> items, DateTime from, DateTime until)
    {
        var closed = items.Where(a => a.DueAt >= from && a.DueAt <= until).ToList();
        return (closed.Count, DashboardSupport.Percent(closed.Sum(a => a.Submitted), closed.Sum(a => a.Recipients)));
    }

    /// <summary>Tỷ lệ nộp ở dạng Kpi: bài hết hạn trong 4 tuần gần nhất, 4 tuần liền trước, và theo tuần hết hạn.</summary>
    public static Kpi SubmissionKpi(IReadOnlyCollection<AssignmentTally> items, DateOnly today, DateTime now, IReadOnlyList<DateOnly> weekStarts)
    {
        var (current, previous) = DashboardSupport.Periods(today);
        var currentFrom = current.ToDateTime(TimeOnly.MinValue);
        return new Kpi(Submission(items, currentFrom, now).Rate,
            Submission(items, previous.ToDateTime(TimeOnly.MinValue), currentFrom.AddTicks(-1)).Rate,
            WeeklySubmission(items, now, weekStarts));
    }

    /// <summary>Tỷ lệ nộp theo tuần hết hạn (chỉ bài đã hết hạn), đúng thứ tự weekStarts.</summary>
    public static List<double?> WeeklySubmission(IEnumerable<AssignmentTally> items, DateTime now, IReadOnlyList<DateOnly> weekStarts)
    {
        var byWeek = items.Where(a => a.DueAt <= now)
            .GroupBy(a => DashboardSupport.WeekStart(DateOnly.FromDateTime(a.DueAt))).ToDictionary(g => g.Key, g => g.ToList());
        return weekStarts.Select(w => byWeek.TryGetValue(w, out var g) ? DashboardSupport.Percent(g.Sum(a => a.Submitted), g.Sum(a => a.Recipients)) : null).ToList();
    }

    /// <summary>
    /// Số ngày vắng của từng giáo viên trong [from, to]: số ngày khác nhau có ít nhất một tiết của giáo viên bị khai vắng, gộp các lần khai báo chồng nhau.
    /// Khai theo tiết chỉ tính ngày có tiết bị khai; ngày nghỉ của trường hoặc phân hiệu (tiết hủy vì ngày nghỉ) và ngày giáo viên không có tiết không tính.
    /// </summary>
    public static async Task<Dictionary<int, int>> AbsentDaysAsync(MsmsDbContext db, List<int> staffIds, DateOnly from, DateOnly to)
    {
        if (staffIds.Count == 0) return [];
        var rows = await db.TeachingSession.AsNoTracking()
            .Where(s => s.TeacherAbsenceId != null && staffIds.Contains(s.TeacherAbsence!.TeacherId)
                && s.SessionDate >= from && s.SessionDate <= to
                && !(s.Status == Outcomes.Cancelled && db.Holiday.Any(h => h.HolidayDate == s.SessionDate
                    && h.AcademicYearId == s.Class.AcademicYearId && (h.CampusId == null || h.CampusId == s.Class.CampusId))))
            .Select(s => new { TeacherId = s.TeacherAbsence!.TeacherId, s.SessionDate }).Distinct().ToListAsync();
        return rows.GroupBy(r => r.TeacherId).ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>Tiết đã qua (trước hôm nay), Đã xếp lịch, chưa có bản ghi điểm danh, theo giáo viên đứng lớp.</summary>
    public static async Task<Dictionary<int, int>> UnmarkedByTeacherAsync(MsmsDbContext db, List<int> classIds, DateOnly from, DateOnly today, List<int>? subjectIds = null)
    {
        if (classIds.Count == 0) return [];
        var q = db.TeachingSession.AsNoTracking()
            .Where(s => s.TeacherId != null && classIds.Contains(s.ClassId) && s.Status == AttendanceRules.SessionScheduled
                && s.SessionDate >= from && s.SessionDate < today && !s.AttendanceRecord.Any());
        if (subjectIds is not null) q = q.Where(s => subjectIds.Contains(s.SubjectId));
        return await q.GroupBy(s => s.TeacherId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
    }

    /// <summary>Tiết đã qua chưa điểm danh theo tổ của môn.</summary>
    public static async Task<Dictionary<int, int>> UnmarkedByDepartmentAsync(MsmsDbContext db, List<int> classIds, DateOnly from, DateOnly today)
    {
        if (classIds.Count == 0) return [];
        return await db.TeachingSession.AsNoTracking()
            .Where(s => classIds.Contains(s.ClassId) && s.Status == AttendanceRules.SessionScheduled
                && s.SessionDate >= from && s.SessionDate < today && !s.AttendanceRecord.Any())
            .GroupBy(s => s.Subject.DepartmentId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
    }

    /// <summary>Tiết bị ảnh hưởng theo tuần (đúng thứ tự weekStarts): số giáo viên vắng có tiết bị ảnh hưởng và số tiết theo kết quả.</summary>
    public static List<AbsenceWeek> AbsenceWeeks(IEnumerable<AffectedSession> sessions, IReadOnlyList<DateOnly> weekStarts)
    {
        var byWeek = sessions.GroupBy(s => DashboardSupport.WeekStart(s.Row.Date)).ToDictionary(g => g.Key, g => g.ToList());
        return weekStarts.Select(w =>
        {
            var g = byWeek.GetValueOrDefault(w) ?? [];
            return new AbsenceWeek(w, g.Where(s => s.AbsentTeacherId is not null).Select(s => s.AbsentTeacherId).Distinct().Count(),
                g.Count(s => s.Row.Outcome == Outcomes.Substituted), g.Count(s => s.Row.Outcome == Outcomes.Cancelled), g.Count(s => s.Row.Outcome == Outcomes.Pending));
        }).ToList();
    }

    /// <summary>Tổng quan bài rèn luyện (xem <see cref="AssignmentSummary"/>).</summary>
    public static AssignmentSummary Summary(IReadOnlyCollection<AssignmentTally> items, DateOnly today, DateTime now)
    {
        var currentFrom = DashboardSupport.Periods(today).Current.ToDateTime(TimeOnly.MinValue);
        return new AssignmentSummary(
            items.Count(a => a.OpenAt >= currentFrom && a.OpenAt <= now),
            items.Count(a => a.OpenAt <= now && a.DueAt > now),
            items.Count(a => a.DueAt > now && a.DueAt <= now.AddDays(7)),
            items.Count(a => a.DueAt >= currentFrom && a.DueAt <= now && a.Recipients > 0 && a.Submitted * 2 < a.Recipients));
    }
}
