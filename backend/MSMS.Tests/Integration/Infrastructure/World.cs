using MSMS.Api.Common.Auth;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Tests.Integration.Infrastructure;

/// <summary>Người tạo thêm cho test: mã người + tên đăng nhập + họ tên.</summary>
public sealed record Who(int PersonId, string Username, string FullName);

/// <summary>Một kịch bản độc lập: tổ riêng, môn riêng, giáo viên riêng nên các test không dẫm lên nhau trên cùng một DB.</summary>
public sealed class Scenario
{
    public required int DepartmentId { get; init; }
    public required int SubjectId { get; init; }
    public required Who Head { get; init; }
    public required Who Absent { get; init; }
    public required Who Sub { get; init; }
    public required int ClassAId { get; init; }
    public required int ClassBId { get; init; }
}

/// <summary>Dựng dữ liệu bổ sung ngoài dữ liệu mẫu của DevSeeder (phân hiệu thứ hai, tổ, giáo viên, lớp, tiết).</summary>
public sealed class World(ApiFactory f)
{
    private static int counter;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public static string Uniq() => Interlocked.Increment(ref counter).ToString("D5");

    public int CampusAId { get; private set; }
    public int CampusBId { get; private set; }
    public int YearId { get; private set; }
    public Who ViceA { get; private set; } = null!;
    public Who ViceB { get; private set; } = null!;
    public Who Principal { get; private set; } = null!;
    public Who SchoolAdmin { get; private set; } = null!;
    private bool ready;

    /// <summary>Phân hiệu A = phân hiệu của dữ liệu mẫu (Lê Thị Mai phụ trách); phân hiệu B tạo thêm cùng Hiệu phó B.</summary>
    public async Task EnsureAsync()
    {
        if (ready) return;
        await Gate.WaitAsync();
        try
        {
            if (ready) return;
            CampusAId = await f.WithDbAsync(db => db.Campus.OrderBy(c => c.CampusId).Select(c => c.CampusId).FirstAsync());
            YearId = await f.WithDbAsync(db => db.AcademicYear.Where(y => y.IsCurrent).Select(y => y.AcademicYearId).SingleAsync());
            CampusBId = await f.WithDbAsync(async db =>
            {
                var c = new Campus { Code = "PH2", Name = "Phân hiệu 2", IsActive = true, CreatedAt = f.Clock.Now };
                db.Campus.Add(c);
                await db.SaveChangesAsync();
                return c.CampusId;
            });
            ViceA = await Find("Lê Thị Mai");
            Principal = await Find("Nguyễn Văn Hùng");
            SchoolAdmin = await Find("Trần Đức Kiên");
            ViceB = await NewStaffAsync("Hiệu phó B " + Uniq(), AccountTypes.VicePrincipal, CampusBId, null);
            await f.WithDbAsync(async db =>
            {
                db.CampusLeadership.Add(new CampusLeadership { CampusId = CampusBId, StaffId = ViceB.PersonId, StartDate = new DateOnly(2026, 9, 5), CreatedAt = f.Clock.Now });
                await db.SaveChangesAsync();
            });
            ready = true;
        }
        finally { Gate.Release(); }
    }

    /// <summary>Hồ sơ + tài khoản đủ loại, trạng thái và email tùy chọn (mật khẩu Msms@123).</summary>
    public Task<Who> NewAccountAsync(string fullName, string personType, string accountType, string status, string? email = null) => Task.Run(async () =>
    {
        await EnsureAsync();
        return await f.WithDbAsync(async db =>
    {
        var code = (personType == "Staff" ? "CB8" : personType == "Student" ? "HS8" : "PH8") + Uniq();
        var person = new Person { PersonType = personType, PersonCode = code, FullName = fullName, DateOfBirth = new DateOnly(1990, 1, 1), Gender = "Male", Email = email, CreatedAt = f.Clock.Now };
        db.Person.Add(person);
        var account = new Account { Person = person, Username = "it" + code.ToLowerInvariant(), AccountType = accountType, Status = status, CreatedAt = f.Clock.Now, PasswordHash = "" };
        account.PasswordHash = AuthService.HashPassword(account, ApiFactory.Password);
        db.Account.Add(account);
        if (personType == "Staff") db.StaffProfile.Add(new StaffProfile { Staff = person, PrimaryCampusId = CampusAId, IsActive = true, Position = accountType, HireDate = new DateOnly(2015, 9, 1) });
        if (personType == "Student") db.StudentProfile.Add(new StudentProfile { Student = person, Status = "Studying", AdmissionDate = new DateOnly(2026, 9, 5) });
        await db.SaveChangesAsync();
        return new Who(person.PersonId, account.Username, fullName);
    });
    });

    /// <summary>Học sinh có tài khoản, ghi danh vào lớp từ ngày <paramref name="from"/> (mặc định đầu năm học).</summary>
    public async Task<(Who Who, int EnrollmentId)> NewStudentAsync(int classId, string name, DateOnly? from = null, DateOnly? to = null)
    {
        var who = await NewAccountAsync(name, "Student", "Student", "Active");
        var id = await f.WithDbAsync(async db =>
        {
            var e = new Enrollment { StudentId = who.PersonId, ClassId = classId, StartDate = from ?? new DateOnly(2026, 9, 5), EndDate = to, CreatedAt = f.Clock.Now };
            db.Enrollment.Add(e);
            await db.SaveChangesAsync();
            return e.EnrollmentId;
        });
        return (who, id);
    }

    public Task<Who> Find(string fullName) => f.WithDbAsync(async db =>
    {
        var a = await db.Account.Where(x => x.Person.FullName == fullName).Select(x => new { x.PersonId, x.Username }).SingleAsync();
        return new Who(a.PersonId, a.Username, fullName);
    });

    public async Task<Who> NewStaffAsync(string fullName, string accountType, int campusId, int? departmentId)
    {
        var code = "CB9" + Uniq();
        return await f.WithDbAsync(async db =>
        {
            var person = new Person { PersonType = "Staff", PersonCode = code, FullName = fullName, DateOfBirth = new DateOnly(1985, 5, 5), Gender = "Male", CreatedAt = f.Clock.Now };
            db.Person.Add(person);
            var account = new Account { Person = person, Username = "it" + code.ToLowerInvariant(), AccountType = accountType, Status = AccountStatuses.Active, CreatedAt = f.Clock.Now, PasswordHash = "" };
            account.PasswordHash = AuthService.HashPassword(account, ApiFactory.Password);
            db.Account.Add(account);
            db.StaffProfile.Add(new StaffProfile { Staff = person, PrimaryCampusId = campusId, IsActive = true, Position = accountType, DepartmentId = departmentId, TeacherAbbr = "T" + Uniq(), HireDate = new DateOnly(2015, 9, 1) });
            await db.SaveChangesAsync();
            return new Who(person.PersonId, account.Username, fullName);
        });
    }

    public async Task<Scenario> NewScenarioAsync()
    {
        await EnsureAsync();
        var u = Uniq();
        var (dept, subject) = await f.WithDbAsync(async db =>
        {
            var d = new SubjectDepartment { Name = "Tổ kiểm thử " + u, IsActive = true };
            db.SubjectDepartment.Add(d);
            await db.SaveChangesAsync();
            var s = new Subject { DepartmentId = d.DepartmentId, Name = "Môn kiểm thử " + u, Abbreviation = "K" + u, IsActive = true };
            db.Subject.Add(s);
            await db.SaveChangesAsync();
            return (d.DepartmentId, s.SubjectId);
        });
        var head = await NewStaffAsync("Tổ trưởng " + u, AccountTypes.Teacher, CampusAId, dept);
        var absent = await NewStaffAsync("GV vắng " + u, AccountTypes.Teacher, CampusAId, dept);
        var sub = await NewStaffAsync("GV dạy thay " + u, AccountTypes.Teacher, CampusAId, dept);
        await f.WithDbAsync(async db =>
        {
            db.DepartmentHead.Add(new DepartmentHead { DepartmentId = dept, StaffId = head.PersonId, StartDate = new DateOnly(2026, 9, 5), CreatedAt = f.Clock.Now });
            await db.SaveChangesAsync();
        });
        var clsA = await NewClassAsync(CampusAId, "KA" + u, 6);
        var clsB = await NewClassAsync(CampusBId, "KB" + u, 6);
        return new Scenario { DepartmentId = dept, SubjectId = subject, Head = head, Absent = absent, Sub = sub, ClassAId = clsA, ClassBId = clsB };
    }

    public Task<int> NewClassAsync(int campusId, string name, byte grade) => f.WithDbAsync(async db =>
    {
        var c = new Class { CampusId = campusId, AcademicYearId = YearId, Name = name, GradeLevel = grade, IsActive = true, CreatedAt = f.Clock.Now };
        db.Class.Add(c);
        await db.SaveChangesAsync();
        return c.ClassId;
    });

    /// <summary>Tiết (từ thời khóa biểu) của giáo viên ở một lớp, ngày và tiết buổi sáng thứ <paramref name="slotNo"/>.</summary>
    public Task<long> NewSessionAsync(int classId, int subjectId, int? teacherId, DateOnly date, int slotNo, string status = "Scheduled") => f.WithDbAsync(async db =>
    {
        var slot = await db.PeriodSlot.Where(p => p.SessionOfDay == "Morning" && p.PeriodNo == slotNo).Select(p => p.PeriodSlotId).SingleAsync();
        var s = new TeachingSession { ClassId = classId, SubjectId = subjectId, TeacherId = teacherId, SessionDate = date, PeriodSlotId = slot, Status = status, Source = "Timetable", CreatedAt = f.Clock.Now };
        db.TeachingSession.Add(s);
        await db.SaveChangesAsync();
        return s.SessionId;
    });

    public Task<TeachingSession> SessionAsync(long id) => f.WithDbAsync(db => db.TeachingSession.AsNoTracking().SingleAsync(s => s.SessionId == id));

    /// <summary>Thứ Hai tuần sau, nằm trong năm học hiện hành.</summary>
    public DateOnly NextMonday()
    {
        var d = f.Clock.Today.AddDays(1);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d;
    }

    public static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd");
}
