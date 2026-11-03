using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Par.Dtos;

namespace MSMS.Api.Modules.Par.Services;

/// <summary>
/// Sổ liên lạc điện tử (BR-24): mọi chỉ số chuyên cần và bài tập tính trực tiếp khi mở, không lưu bảng,
/// không có job nền. Một sổ = 1 học sinh + 1 tuần (Thứ 2 - Chủ nhật) trong năm học hiện hành học sinh có ghi danh.
/// </summary>
public sealed class WeeklyReportService(MsmsDbContext db, IClock clock, CurrentUser user)
{
    /// <summary>Danh sách tuần (Thứ 2 - CN) trong năm học hiện hành mà học sinh có ghi danh giao với tuần đó.</summary>
    public async Task<IReadOnlyList<ReportWeekDto>> GetValidWeeksAsync(int studentId)
    {
        var year = await db.AcademicYear.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent)
            ?? throw ApiException.BadRequest("Chưa có năm học hiện hành.");

        var enrollments = await db.Enrollment
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Class.AcademicYearId == year.AcademicYearId)
            .Select(e => new { e.StartDate, e.EndDate })
            .ToListAsync();

        if (enrollments.Count == 0)
            return [];

        var today = clock.Today;
        var firstMonday = StartOfWeek(year.StartDate);
        var lastMonday = StartOfWeek(year.EndDate);

        var weeks = new List<ReportWeekDto>();
        var weekNo = 1;
        for (var monday = firstMonday; monday <= lastMonday; monday = monday.AddDays(7), weekNo++)
        {
            var sunday = monday.AddDays(6);

            // Tuần phải giao với ít nhất một khoảng ghi danh của học sinh trong năm học này.
            var hasEnrollment = enrollments.Any(e =>
                EnrollmentRules.Overlaps(monday, sunday, e.StartDate, e.EndDate ?? year.EndDate));
            if (!hasEnrollment)
                continue;

            var isCurrent = monday <= today && today <= sunday;
            weeks.Add(new ReportWeekDto(monday, sunday, weekNo, $"Tuần {weekNo} ({monday:dd/MM} - {sunday:dd/MM})", isCurrent));
        }

        return weeks;
    }

    /// <summary>Tính sổ liên lạc của một học sinh cho một tuần (mặc định tuần chứa hôm nay).</summary>
    public async Task<WeeklyReportDto> ComputeWeeklyReportAsync(int studentId, DateOnly? weekStart)
    {
        var start = weekStart ?? StartOfWeek(clock.Today);
        if (start.DayOfWeek != DayOfWeek.Monday)
            throw ApiException.BadRequest("Ngày bắt đầu tuần phải là Thứ 2.");
        var end = start.AddDays(6);

        var year = await db.AcademicYear.AsNoTracking().FirstOrDefaultAsync(y => y.IsCurrent)
            ?? throw ApiException.BadRequest("Chưa có năm học hiện hành.");

        var enrollments = await db.Enrollment
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Class.AcademicYearId == year.AcademicYearId)
            .Select(e => new { e.ClassId, ClassName = e.Class.Name, e.StartDate, e.EndDate })
            .ToListAsync();

        if (enrollments.Count == 0 || !enrollments.Any(e =>
                EnrollmentRules.Overlaps(start, end, e.StartDate, e.EndDate ?? year.EndDate)))
            throw ApiException.BadRequest("Học sinh không có ghi danh trong tuần này.");

        var student = await db.StudentProfile
            .AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => new { s.Student.FullName, s.Student.PersonCode })
            .FirstOrDefaultAsync()
            ?? throw ApiException.NotFound("Không tìm thấy học sinh.");

        // Lớp đại diện hiển thị trên đầu sổ: ghi danh hiệu lực cuối tuần, nếu không có thì ghi danh giao với tuần gần cuối tuần nhất.
        var classId = ResolveClassId(start, end, enrollments.Select(e => (e.ClassId, e.StartDate, e.EndDate ?? year.EndDate)).ToList());
        var className = enrollments.First(e => e.ClassId == classId).ClassName;

        var attendance = await ComputeAttendanceAsync(studentId, start, end);
        var assignments = await ComputeAssignmentsAsync(studentId, start, end);

        return new WeeklyReportDto(
            studentId,
            student.FullName,
            student.PersonCode,
            className,
            start,
            end,
            attendance,
            assignments);
    }

    /// <summary>Danh sách học sinh thuộc lớp GVCN đang chủ nhiệm hiệu lực hôm nay.</summary>
    public async Task<IReadOnlyList<HomeroomStudentItemDto>> GetHomeroomStudentsAsync()
    {
        var today = clock.Today;
        var myClasses = await db.HomeroomAssignment
            .AsNoTracking()
            .Where(EffectivePeriod.EffectiveAt<HomeroomAssignment>(today))
            .Where(ha => ha.StaffId == user.PersonId)
            .Select(ha => ha.ClassId)
            .ToListAsync();

        if (myClasses.Count == 0)
            return [];

        return await db.Enrollment
            .AsNoTracking()
            .Where(e => myClasses.Contains(e.ClassId))
            .Where(EnrollmentRules.ActiveAt(today))
            .OrderBy(e => e.Class.Name).ThenBy(e => e.Student.Student.FullName)
            .Select(e => new HomeroomStudentItemDto(
                e.StudentId,
                e.Student.Student.PersonCode,
                e.Student.Student.FullName,
                e.Class.Name))
            .ToListAsync();
    }

    /// <summary>Chọn ghi danh đại diện cho tuần: hiệu lực cuối tuần, hoặc ghi danh giao với tuần có StartDate muộn nhất.</summary>
    private static int ResolveClassId(DateOnly weekStart, DateOnly weekEnd, List<(int ClassId, DateOnly StartDate, DateOnly EffectiveEnd)> enrollments)
    {
        var atWeekEnd = enrollments.FirstOrDefault(e => e.StartDate <= weekEnd && e.EffectiveEnd >= weekEnd);
        if (atWeekEnd.ClassId != 0)
            return atWeekEnd.ClassId;

        return enrollments
            .Where(e => EnrollmentRules.Overlaps(weekStart, weekEnd, e.StartDate, e.EffectiveEnd))
            .OrderByDescending(e => e.StartDate)
            .First()
            .ClassId;
    }

    /// <summary>BR-24: 5 chỉ số chuyên cần từ các tiết "Đã xếp lịch đã diễn ra" trong tuần.</summary>
    private async Task<AttendanceSummaryDto> ComputeAttendanceAsync(int studentId, DateOnly weekStart, DateOnly weekEnd)
    {
        var today = clock.Today;
        var nowTime = TimeOnly.FromDateTime(clock.Now);

        // Ghi danh của học sinh trong năm học hiện hành, để xác định lớp theo từng ngày (chuyển lớp giữa tuần).
        var enrollments = await db.Enrollment
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .Select(e => new { e.ClassId, e.StartDate, e.EndDate })
            .ToListAsync();

        var classIds = enrollments.Select(e => e.ClassId).Distinct().ToList();

        var sessions = await db.TeachingSession
            .AsNoTracking()
            .Where(ts => classIds.Contains(ts.ClassId) && ts.SessionDate >= weekStart && ts.SessionDate <= weekEnd)
            .Where(ts => ts.Status != "Cancelled" && ts.Status != "PendingResolution")
            .Select(ts => new
            {
                ts.SessionId,
                ts.ClassId,
                ts.SessionDate,
                EndTime = ts.PeriodSlot.EndTime,
                AttendanceStatus = ts.AttendanceRecord
                    .Where(ar => ar.StudentId == studentId)
                    .Select(ar => (string?)ar.Status)
                    .FirstOrDefault(),
            })
            .ToListAsync();

        // Chỉ các tiết thuộc lớp mà học sinh có ghi danh hiệu lực đúng vào ngày của tiết đó (WBS 4.7.9, 4.2.33).
        var relevant = sessions.Where(s => enrollments.Any(e =>
                e.ClassId == s.ClassId && e.StartDate <= s.SessionDate && (e.EndDate == null || e.EndDate >= s.SessionDate)))
            .ToList();

        // Tiết "đã diễn ra": trước hôm nay, hoặc hôm nay và đã qua giờ kết thúc.
        var occurred = relevant.Where(s => s.SessionDate < today || (s.SessionDate == today && s.EndTime <= nowTime)).ToList();

        var present = occurred.Count(s => s.AttendanceStatus == "Present");
        var late = occurred.Count(s => s.AttendanceStatus == "Late");
        var excused = occurred.Count(s => s.AttendanceStatus == "Excused");
        var unexcused = occurred.Count(s => s.AttendanceStatus == "Unexcused");
        var unmarked = occurred.Count(s => s.AttendanceStatus is null);

        return new AttendanceSummaryDto(occurred.Count, present, late, excused, unexcused, unmarked);
    }

    /// <summary>BR-21, BR-22, BR-23: 4 chỉ số bài tập theo hạn nộp trong tuần và danh sách được giao đã chốt.</summary>
    private async Task<AssignmentSummaryDto> ComputeAssignmentsAsync(int studentId, DateOnly weekStart, DateOnly weekEnd)
    {
        var rangeStart = weekStart.ToDateTime(TimeOnly.MinValue);
        var rangeEnd = weekEnd.ToDateTime(TimeOnly.MaxValue);
        var now = clock.Now;

        var rows = await db.AssignmentRecipient
            .AsNoTracking()
            .Where(r => r.StudentId == studentId && r.Assignment.DueAt >= rangeStart && r.Assignment.DueAt <= rangeEnd)
            .Select(r => new
            {
                r.Assignment.AssignmentId,
                r.Assignment.Title,
                SubjectName = r.Assignment.Subject.Name,
                r.Assignment.DueAt,
                Submission = r.Assignment.Submission
                    .Where(sb => sb.StudentId == studentId)
                    .Select(sb => new
                    {
                        sb.SubmittedAt,
                        sb.Score,
                        AiFeedback = sb.AiFeedback != null && sb.AiFeedback.Status == MSMS.Api.Modules.Lrn.Services.AnalysisStatuses.Done ? sb.AiFeedback.AiContent : null,
                    })
                    .FirstOrDefault(),
            })
            .OrderBy(r => r.DueAt)
            .ToListAsync();

        var items = rows.Select(r =>
        {
            var submittedAt = r.Submission?.SubmittedAt;
            string status;
            if (submittedAt != null)
                status = "Submitted";
            else if (now < r.DueAt)
                status = "Unsubmitted";
            else
                status = "Missed";

            // BR-22, BR-23: chưa nộp thì điểm và nhận xét AI để trống.
            decimal? score = submittedAt != null ? r.Submission!.Score : null;
            string? aiFeedback = submittedAt != null ? r.Submission!.AiFeedback : null;

            return new WeeklyAssignmentItemDto(r.AssignmentId, r.Title, r.SubjectName, r.DueAt, status, score, aiFeedback);
        }).ToList();

        var submitted = items.Count(i => i.Status == "Submitted");
        var unsubmitted = items.Count(i => i.Status == "Unsubmitted");
        var missed = items.Count(i => i.Status == "Missed");

        return new AssignmentSummaryDto(items.Count, submitted, unsubmitted, missed, items);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }
}
