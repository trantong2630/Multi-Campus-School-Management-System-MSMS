using MSMS.Api.Data.Entities;
using MSMS.Tests.Integration.Infrastructure;

namespace MSMS.Tests.Integration.Modules.Adm;

/// <summary>ADM-02: bảng tình hình phản ứng với dữ liệu các module khác; ADM-15: số ngày vắng của giáo viên. Dữ liệu dựng ở quá khứ gần (4 tuần xem lại).</summary>
[Collection("api")]
public class AdmBoardTests(ApiFactory f)
{
    private World w => f.World;
    private static string U() => World.Uniq();

    [Fact]
    public async Task ADM_02_The_dashboard_numbers_react_to_changes_made_in_other_modules()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var campus = await f.WithDbAsync(async db => { var c = new Campus { Code = "PHD" + U(), Name = "Phân hiệu bảng " + U(), IsActive = true, CreatedAt = f.Clock.Now }; db.Campus.Add(c); await db.SaveChangesAsync(); return c.CampusId; });
        var cls = await w.NewClassAsync(campus, "DB" + U(), 6);
        var teacher = await w.NewStaffAsync("GV bảng " + U(), "Teacher", campus, null);
        var principal = await f.LoginAsync(w.Principal.Username);
        var url = $"/api/adm/dashboard?campusId={campus}";
        var before = await principal.GetAsync(url);
        Assert.Equal(200, before.Status);
        Assert.Equal(0, before.Body!["studentCount"]!.GetValue<int>());
        // ghi danh 3 học sinh
        var kids = new List<Who>();
        for (var i = 0; i < 3; i++) kids.Add((await w.NewStudentAsync(cls, $"HS bảng {i} {U()}")).Who);
        var afterEnroll = await principal.GetAsync(url);
        Assert.Equal(3, afterEnroll.Body!["studentCount"]!.GetValue<int>());
        // một tiết tuần trước: 1 có mặt, 2 vắng không phép
        var day = w.NextMonday().AddDays(-13);
        var subject = await f.WithDbAsync(db => db.Subject.Select(s => s.SubjectId).FirstAsync());
        var sid = await w.NewSessionAsync(cls, subject, teacher.PersonId, day, 1);
        await f.WithDbAsync(async db =>
        {
            for (var i = 0; i < 3; i++)
                db.AttendanceRecord.Add(new AttendanceRecord { SessionId = sid, StudentId = kids[i].PersonId, Status = i == 0 ? "Present" : "Absent", RecordedByPersonId = teacher.PersonId, RecordedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var afterAtt = await principal.GetAsync(url);
        var mix = afterAtt.Body!["mix"]!;
        Assert.Equal(1, mix["present"]!.GetValue<int>());
        Assert.Equal(2, mix["absentUnexcused"]!.GetValue<int>());
        Assert.NotNull(afterAtt.Body["attendance"]!["value"]);
        Assert.Equal(33.3, Math.Round(afterAtt.Body["attendance"]!["value"]!.GetValue<double>(), 1));   // 1/3 có mặt
        // tiết thứ hai bị hủy làm tăng số tiết bị ảnh hưởng
        var cancelled = await w.NewSessionAsync(cls, subject, teacher.PersonId, day, 2, "Cancelled");
        var afterCancel = await principal.GetAsync(url);
        Assert.NotEqual(before.Body["affected"]!["value"]?.ToString(), afterCancel.Body!["affected"]!["value"]?.ToString());
        Assert.NotEqual(0, cancelled);
    }

    [Fact]
    public async Task ADM_15_Absent_days_of_a_teacher_count_the_days_on_which_a_session_was_declared_absent()
    {
        await w.EnsureAsync();
        f.Clock.Reset();
        var s = await w.NewScenarioAsync();
        var monday = w.NextMonday().AddDays(-14);
        var tue = monday.AddDays(1); var wed = monday.AddDays(2); var thu = monday.AddDays(3);
        var (abs1, abs2, abs3) = await f.WithDbAsync(async db =>
        {
            TeacherAbsence A(DateOnly from, DateOnly to) => new() { TeacherId = s.Absent.PersonId, FromDate = from, ToDate = to, DeclaredByPersonId = s.Head.PersonId, CreatedAt = f.Clock.Now, SubmittedAt = f.Clock.Now };
            var a1 = A(tue, tue); var a2 = A(tue, tue); var a3 = A(wed, monday.AddDays(4));
            db.TeacherAbsence.AddRange(a1, a2, a3);
            await db.SaveChangesAsync();
            db.Holiday.Add(new Holiday { AcademicYearId = w.YearId, CampusId = w.CampusAId, HolidayDate = thu, Reason = "Nghỉ phân hiệu", CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
            return (a1.AbsenceId, a2.AbsenceId, a3.AbsenceId);
        });
        var t1 = await w.NewSessionAsync(s.ClassAId, s.SubjectId, null, tue, 1, "Pending");
        var t2 = await w.NewSessionAsync(s.ClassAId, s.SubjectId, null, tue, 2, "Pending");
        var wd = await w.NewSessionAsync(s.ClassAId, s.SubjectId, null, wed, 1, "Pending");
        var th = await w.NewSessionAsync(s.ClassAId, s.SubjectId, null, thu, 1, "Cancelled");   // tiết bị hủy vì ngày nghỉ của phân hiệu
        await f.WithDbAsync(async db =>
        {
            foreach (var (id, ab) in new[] { (t1, abs1), (t2, abs2), (wd, abs3), (th, abs3) })
                (await db.TeachingSession.SingleAsync(x => x.SessionId == id)).TeacherAbsenceId = ab;
            await db.SaveChangesAsync();
        });
        var principal = await f.LoginAsync(w.Principal.Username);
        var board = await principal.GetAsync("/api/adm/dashboard");
        Assert.Equal(200, board.Status);
        var row = board.Body!["teachers"]!.AsArray().Single(x => x!["staffId"]!.GetValue<int>() == s.Absent.PersonId)!;
        Assert.Equal(2, row["absentDays"]!.GetValue<int>());   // thứ Ba (hai lần khai chồng nhau tính một lần) và thứ Tư; thứ Năm là ngày nghỉ, thứ Sáu không có tiết
        var dept = await (await f.LoginAsync(s.Head.Username)).GetAsync("/api/adm/dashboard/department");
        Assert.Equal(200, dept.Status);
        var member = dept.Body!["members"]!.AsArray().Single(x => x!["staffId"]!.GetValue<int>() == s.Absent.PersonId)!;
        Assert.Equal(2, member["absentDays"]!.GetValue<int>());   // hai màn hình cho cùng một con số
    }
}
