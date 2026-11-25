using MSMS.Api.Common.Auth;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Par.Services;
using MSMS.Tests.Modules.Ppl;

namespace MSMS.Tests.Modules.Par;

/// <summary>BR-24: chuyên cần trong báo cáo tuần chỉ tính tiết Đã xếp lịch đã diễn ra, như màn Điểm danh.</summary>
public class WeeklyReportAttendanceTests
{
    [Fact(DisplayName = "PAR | Báo cáo tuần | Tiết Chờ xử lý và Đã hủy đã qua | không vào tổng tiết, không thành Chưa điểm danh")]
    public async Task PendingAndCancelledSessions_AreNotCounted()
    {
        using var e = new PplEnv(AccountTypes.Parent, PplEnv.Parent1);
        e.Db.Subject.Add(new Subject { SubjectId = 1, DepartmentId = PplEnv.Dept1, Name = "Toán", Abbreviation = "TO", IsActive = true });
        var monday = PplEnv.Today.AddDays(-1);   // 26/10/2026
        TeachingSession Session(long id, string status, int? teacher) => new()
        {
            SessionId = id, ClassId = PplEnv.Class6A, SubjectId = 1, TeacherId = teacher, SessionDate = monday, PeriodSlotId = 1,
            Status = status, Source = "Generated", CreatedAt = e.Clock.Now,
        };
        e.Db.TeachingSession.AddRange(Session(1, "Scheduled", PplEnv.Teacher), Session(2, "Pending", null), Session(3, "Cancelled", PplEnv.Teacher));
        e.Db.AttendanceRecord.Add(new AttendanceRecord { SessionId = 1, StudentId = PplEnv.StudentA, Status = "Present", RecordedByPersonId = PplEnv.Teacher, RecordedAt = e.Clock.Now });
        e.Db.SaveChanges();

        var report = await new WeeklyReportService(e.Db, e.Clock, e.User).ComputeWeeklyReportAsync(PplEnv.StudentA, monday);

        Assert.Equal(1, report.Attendance.TotalSessions);
        Assert.Equal(1, report.Attendance.PresentCount);
        Assert.Equal(0, report.Attendance.UnmarkedCount);
    }
}
