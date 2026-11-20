using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data.Entities;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>Hồ sơ học sinh, chỉ xem (PPL-02, 4.3.12): phạm vi theo vai trò, lịch sử lớp, chuyên cần năm học hiện hành.</summary>
public class StudentDetailTests
{
    private static readonly DateOnly YearStart = new(2026, 9, 5);

    private static async Task NotFound(Func<Task> act)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(act);
        Assert.Equal(404, ex.StatusCode);
    }

    /// <summary>Một tiết của lớp vào ngày cho trước, kèm bản ghi điểm danh của học sinh nếu có.</summary>
    private static void AddSession(PplEnv e, long id, int classId, DateOnly date, int studentId, string? status, string sessionStatus = "Scheduled")
    {
        if (!e.Db.Subject.Any()) e.Db.Subject.Add(new Subject { SubjectId = 1, DepartmentId = PplEnv.Dept1, Name = "Toán", Abbreviation = "TO", IsActive = true });
        e.Db.TeachingSession.Add(new TeachingSession
        {
            SessionId = id, ClassId = classId, SubjectId = 1, SessionDate = date, PeriodSlotId = 1, Status = sessionStatus, Source = "Timetable", CreatedAt = e.Clock.Now,
        });
        if (status is not null)
            e.Db.AttendanceRecord.Add(new AttendanceRecord
            {
                AttendanceId = id, SessionId = id, StudentId = studentId, Status = status, RecordedByPersonId = PplEnv.Teacher, RecordedAt = e.Clock.Now,
            });
        e.Db.SaveChanges();
    }

    [Fact(DisplayName = "PPL | Hồ sơ học sinh | School Admin | thấy đủ hồ sơ, phụ huynh đại diện và lớp hiện tại")]
    public async Task Admin_SeesProfile()
    {
        using var e = new PplEnv();
        var d = await e.Students.GetDetailAsync(PplEnv.StudentA);
        Assert.Equal("Học sinh A", d.FullName);
        Assert.Equal("6A", d.ClassName);
        Assert.Equal("Phân hiệu 1", d.CampusName);
        Assert.Equal(YearStart, d.AdmissionDate);
        Assert.NotNull(d.Guardian);
        Assert.Equal("Phụ huynh A", d.Guardian!.FullName);
        Assert.Equal("Mẹ", d.Guardian.Relationship);
        var only = Assert.Single(d.Enrollments);
        Assert.True(only.IsCurrent);
        Assert.Equal("2026-2027", only.AcademicYearName);
        Assert.Equal(new DateOnly(2027, 5, 31), only.EndDate);
    }

    [Fact(DisplayName = "PPL | Hồ sơ học sinh | Đã tốt nghiệp, chưa xếp lớp | không có lớp hiện tại, lịch sử lớp rỗng")]
    public async Task Graduated_NoClass()
    {
        using var e = new PplEnv();
        var d = await e.Students.GetDetailAsync(PplEnv.StudentGraduated);
        Assert.Equal("Graduated", d.Status);
        Assert.Null(d.ClassName);
        Assert.Empty(d.Enrollments);
        Assert.Null(d.Guardian);
    }

    [Fact(DisplayName = "PPL | Hồ sơ học sinh | Mã không tồn tại | báo không tìm thấy")]
    public async Task Missing_NotFound()
    {
        using var e = new PplEnv();
        await NotFound(() => e.Students.GetDetailAsync(9999));
    }

    [Fact(DisplayName = "PPL | Hồ sơ học sinh | Hiệu trưởng | xem được học sinh toàn trường")]
    public async Task Principal_SeesAll()
    {
        using var e = new PplEnv(AccountTypes.Principal, PplEnv.PrincipalId);
        Assert.Equal("9A", (await e.Students.GetDetailAsync(PplEnv.StudentGrade9)).ClassName);
    }

    [Fact(DisplayName = "PPL | Hồ sơ học sinh | Hiệu phó | chỉ xem được khi đang phụ trách phân hiệu của học sinh")]
    public async Task VicePrincipal_ByCampus()
    {
        // Phạm vi được tính một lần mỗi request, nên mỗi trường hợp dùng một môi trường riêng.
        using (var none = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP))
            await NotFound(() => none.Students.GetDetailAsync(PplEnv.StudentA));

        using var e = new PplEnv(AccountTypes.VicePrincipal, PplEnv.ViceP);
        e.Seed("campus", PplEnv.Campus1, PplEnv.ViceP, YearStart);
        Assert.Equal("Học sinh A", (await e.Students.GetDetailAsync(PplEnv.StudentA)).FullName);
    }

    [Fact(DisplayName = "PPL | Hồ sơ học sinh | Chủ nhiệm | xem được học sinh lớp mình, không xem được lớp khác")]
    public async Task Homeroom_OwnClassOnly()
    {
        using var e = new PplEnv(AccountTypes.Teacher, PplEnv.Teacher);
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, YearStart);
        Assert.Equal("Học sinh A", (await e.Students.GetDetailAsync(PplEnv.StudentA)).FullName);
        await NotFound(() => e.Students.GetDetailAsync(PplEnv.StudentGrade9));
        await NotFound(() => e.Students.GetDetailAsync(PplEnv.StudentNoClass));
    }

    [Fact(DisplayName = "PPL | Hồ sơ học sinh | Giáo viên không chủ nhiệm, chủ nhiệm đã kết thúc | không xem được")]
    public async Task Teacher_WithoutHomeroom_Denied()
    {
        using (var none = new PplEnv(AccountTypes.Teacher, PplEnv.Teacher))
            await NotFound(() => none.Students.GetDetailAsync(PplEnv.StudentA));

        using var e = new PplEnv(AccountTypes.Teacher, PplEnv.Teacher);
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, YearStart, PplEnv.Today.AddDays(-1));
        await NotFound(() => e.Students.GetDetailAsync(PplEnv.StudentA));
    }

    [Fact(DisplayName = "PPL | Hồ sơ học sinh | Lịch sử lớp | mới nhất lên đầu, ghi danh đã đóng có lý do; chủ nhiệm lớp cũ vẫn mở được hồ sơ")]
    public async Task History_NewestFirst()
    {
        using var e = new PplEnv();
        e.Db.Enrollment.AddRange(
            new Enrollment { StudentId = PplEnv.StudentNoClass, ClassId = PplEnv.Class6A, StartDate = YearStart, EndDate = new(2026, 10, 1), Reason = EnrollmentCloseReasons.ClassTransfer, CreatedAt = e.Clock.Now },
            new Enrollment { StudentId = PplEnv.StudentNoClass, ClassId = PplEnv.Class9A, StartDate = new(2026, 10, 2), CreatedAt = e.Clock.Now });
        e.Db.SaveChanges();

        var d = await e.Students.GetDetailAsync(PplEnv.StudentNoClass);
        Assert.Equal("9A", d.ClassName);
        Assert.Collection(d.Enrollments,
            x => { Assert.Equal("9A", x.ClassName); Assert.True(x.IsCurrent); Assert.Null(x.Reason); },
            x => { Assert.Equal("6A", x.ClassName); Assert.False(x.IsCurrent); Assert.Equal(EnrollmentCloseReasons.ClassTransfer, x.Reason); });

        using var t = new PplEnv(AccountTypes.Teacher, PplEnv.Teacher);
        t.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, YearStart);
        t.Db.Enrollment.Add(new Enrollment { StudentId = PplEnv.StudentNoClass, ClassId = PplEnv.Class6A, StartDate = YearStart, EndDate = new(2026, 10, 1), Reason = EnrollmentCloseReasons.ClassTransfer, CreatedAt = t.Clock.Now });
        t.Db.SaveChanges();
        Assert.Single((await t.Students.GetDetailAsync(PplEnv.StudentNoClass)).Enrollments);
    }

    [Fact(DisplayName = "PPL | Hồ sơ học sinh | Chuyên cần | bốn nhãn theo BR-18, tỷ lệ bỏ qua tiết chưa điểm danh, tiết hủy và tiết sau hôm nay")]
    public async Task Attendance_Overview()
    {
        using var e = new PplEnv();
        var a = PplEnv.StudentA;
        AddSession(e, 1, PplEnv.Class6A, new(2026, 10, 19), a, AttendanceStatuses.Present);
        AddSession(e, 2, PplEnv.Class6A, new(2026, 10, 20), a, AttendanceStatuses.Late);
        AddSession(e, 3, PplEnv.Class6A, new(2026, 10, 21), a, AttendanceStatuses.Absent);
        AddSession(e, 4, PplEnv.Class6A, new(2026, 10, 22), a, AttendanceStatuses.Absent);
        AddSession(e, 5, PplEnv.Class6A, new(2026, 10, 23), a, null);
        AddSession(e, 6, PplEnv.Class6A, new(2026, 10, 26), a, AttendanceStatuses.Absent, sessionStatus: "Cancelled");
        AddSession(e, 7, PplEnv.Class6A, new(2026, 10, 28), a, null);
        e.Db.LeaveRequest.Add(new LeaveRequest
        {
            StudentId = a, ParentId = PplEnv.Parent1, ClassId = PplEnv.Class6A, FromDate = new(2026, 10, 22), ToDate = new(2026, 10, 22),
            SessionOfDay = "AllDay", Status = AttendanceRules.LeaveApproved, CreatedAt = e.Clock.Now,
        });
        e.Db.SaveChanges();

        var att = (await e.Students.GetDetailAsync(a)).Attendance!;
        Assert.Equal("2026-2027", att.AcademicYearName);
        Assert.Equal(PplEnv.Today, att.To);
        Assert.Equal((1, 1, 1, 1, 1), (att.Counts.Present, att.Counts.Late, att.Counts.ExcusedAbsent, att.Counts.UnexcusedAbsent, att.Counts.NotMarked));
        Assert.Equal(50.0, att.Rate);
        Assert.Equal([AttendanceLabels.ExcusedAbsent, AttendanceLabels.UnexcusedAbsent, AttendanceLabels.Late], att.Recent.Select(r => r.Label));
    }

    [Fact(DisplayName = "PPL | Hồ sơ học sinh | Chuyên cần | chưa có lượt nào thì tỷ lệ để trống, không hiện 0%")]
    public async Task Attendance_NoData()
    {
        using var e = new PplEnv();
        var att = (await e.Students.GetDetailAsync(PplEnv.StudentA)).Attendance!;
        Assert.Null(att.Rate);
        Assert.Empty(att.Recent);
    }

    [Fact(DisplayName = "PPL | Hồ sơ học sinh | Chuyên cần | chủ nhiệm chỉ thấy tiết của lớp mình chủ nhiệm")]
    public async Task Attendance_HomeroomScope()
    {
        using var e = new PplEnv(AccountTypes.Teacher, PplEnv.Teacher);
        e.Seed("homeroom", PplEnv.Class6A, PplEnv.Teacher, YearStart);
        var s = PplEnv.StudentNoClass;
        e.Db.Enrollment.AddRange(
            new Enrollment { StudentId = s, ClassId = PplEnv.Class6A, StartDate = YearStart, EndDate = new(2026, 10, 1), Reason = EnrollmentCloseReasons.ClassTransfer, CreatedAt = e.Clock.Now },
            new Enrollment { StudentId = s, ClassId = PplEnv.Class9A, StartDate = new(2026, 10, 2), CreatedAt = e.Clock.Now });
        e.Db.SaveChanges();
        AddSession(e, 1, PplEnv.Class6A, new(2026, 9, 30), s, AttendanceStatuses.Absent);
        AddSession(e, 2, PplEnv.Class9A, new(2026, 10, 20), s, AttendanceStatuses.Absent);

        var att = (await e.Students.GetDetailAsync(s)).Attendance!;
        Assert.Equal(1, att.Counts.UnexcusedAbsent);
        Assert.Equal(new DateOnly(2026, 9, 30), Assert.Single(att.Recent).Date);
    }
}
