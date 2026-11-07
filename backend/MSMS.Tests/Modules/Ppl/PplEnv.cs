using Microsoft.Extensions.Options;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;
using MSMS.Api.Modules.Ppl.Services;
using MSMS.Api.Modules.Sch.Services;
using MSMS.Tests.Modules.Sch;

namespace MSMS.Tests.Modules.Ppl;

/// <summary>
/// Môi trường test PPL (DB trong bộ nhớ). Hôm nay = 27/10/2026. Dữ liệu mẫu: phân hiệu 1 (hoạt động) và 2 (ngừng), năm học 2026-2027,
/// lớp 6A (khối 6) và 9A (khối 9), tổ 1 (hoạt động) và 2 (ngừng), một số cán bộ/học sinh/phụ huynh có tài khoản các loại.
/// </summary>
internal sealed class PplEnv : IDisposable
{
    public static readonly DateOnly Today = new(2026, 10, 27);
    public const int Campus1 = 1, CampusInactive = 2, Year1 = 1, Class6A = 11, Class9A = 12, Dept1 = 1, DeptInactive = 2;
    public const int Teacher = 10, ViceP = 11, LockedTeacher = 12, NoAccountStaff = 13, InactiveStaff = 14, PrincipalId = 15, OtherTeacher = 16;
    public const int StudentA = 101, StudentGrade9 = 102, StudentNoClass = 103, StudentGraduated = 104, Parent1 = 201, Parent2 = 202, Admin = 900;
    public const string ExistingNid = "111111111111";

    public MsmsDbContext Db { get; } = SchTestSupport.NewDb();
    public IClock Clock { get; } = SchTestSupport.ClockAt(Today);
    public CurrentUser User { get; }
    public ScopeResolver Scope { get; }

    public StaffService Staff => new(Db, Clock, Scope);
    public GuardianService Guardians => new(Db, Clock, Scope);
    public ResponsibilityService Resp => new(Db, User, Clock, Scope);
    public EnrollmentService Enrollments => new(Db, Clock, User, Scope, new AuditWriter(Db, User, Clock), new SessionService(Db, Clock, Options.Create(new JwtOptions())));
    public StudentService Students => new(Db, Clock, Scope, Enrollments);

    public PplEnv(string accountType = AccountTypes.SchoolAdmin, int personId = Admin)
    {
        User = SchTestSupport.UserOf(accountType, personId);
        Scope = new ScopeResolver(Db, User, Clock);

        SchTestSupport.AddCampus(Db, Campus1, "Phân hiệu 1");
        SchTestSupport.AddCampus(Db, CampusInactive, "Phân hiệu cũ", active: false);
        SchTestSupport.AddYear(Db, Year1, "2026-2027", new(2026, 9, 5), new(2027, 5, 31), current: true);
        SchTestSupport.AddClass(Db, Class6A, Campus1, Year1, "6A", 6);
        SchTestSupport.AddClass(Db, Class9A, Campus1, Year1, "9A", 9);
        Db.SubjectDepartment.AddRange(new SubjectDepartment { DepartmentId = Dept1, Name = "Tổ Toán", IsActive = true },
            new SubjectDepartment { DepartmentId = DeptInactive, Name = "Tổ cũ", IsActive = false });
        Db.SaveChanges();

        AddStaff(Admin, "Quản trị", AccountTypes.SchoolAdmin);
        AddStaff(Teacher, "Giáo viên A", AccountTypes.Teacher, dept: Dept1, abbr: "ABC", nid: ExistingNid);
        AddStaff(ViceP, "Hiệu phó A", AccountTypes.VicePrincipal);
        AddStaff(LockedTeacher, "Giáo viên bị khóa", AccountTypes.Teacher, status: AccountStatuses.Locked);
        AddStaff(NoAccountStaff, "Cán bộ chưa có tài khoản", null);
        AddStaff(InactiveStaff, "Cán bộ đã nghỉ", AccountTypes.Teacher, active: false);
        AddStaff(PrincipalId, "Hiệu trưởng", AccountTypes.Principal);
        AddStaff(OtherTeacher, "Giáo viên B", AccountTypes.Teacher, dept: Dept1, abbr: "XYZ");

        AddStudent(StudentA, "Học sinh A", Class6A);
        AddStudent(StudentGrade9, "Học sinh khối 9", Class9A);
        AddStudent(StudentNoClass, "Học sinh chưa xếp lớp", null);
        AddStudent(StudentGraduated, "Học sinh đã tốt nghiệp", null, status: "Graduated");
        AddParent(Parent1, "Phụ huynh A", "0912345678", StudentA);
        AddParent(Parent2, "Phụ huynh B", "0987654321", null);
    }

    public Person AddPerson(int id, string type, string name, string? nid = null, string? phone = null)
    {
        var p = new Person { PersonId = id, PersonType = type, PersonCode = $"{(type == "Staff" ? "CB" : type == "Student" ? "HS" : "PH")}{id:D6}",
            FullName = name, NationalId = nid, PhoneNumber = phone, CreatedAt = Clock.Now };
        Db.Person.Add(p);
        Db.SaveChanges();
        return p;
    }

    public void AddStaff(int id, string name, string? accountType, string status = "Active", bool active = true, int campus = Campus1, int? dept = null, string? abbr = null, string? nid = null)
    {
        AddPerson(id, "Staff", name, nid);
        Db.StaffProfile.Add(new StaffProfile { StaffId = id, PrimaryCampusId = campus, DepartmentId = dept, TeacherAbbr = abbr, IsActive = active });
        if (accountType is not null)
            Db.Account.Add(new Account { AccountId = id, PersonId = id, Username = $"user{id}", PasswordHash = "x", AccountType = accountType, Status = status, CreatedAt = Clock.Now });
        Db.SaveChanges();
    }

    public void AddStudent(int id, string name, int? classId, string status = "Studying", DateOnly? start = null)
    {
        AddPerson(id, "Student", name);
        Db.StudentProfile.Add(new StudentProfile { StudentId = id, Status = status, AdmissionDate = new(2026, 9, 5) });
        if (classId is not null) Db.Enrollment.Add(new Enrollment { StudentId = id, ClassId = classId.Value, StartDate = start ?? new(2026, 9, 5), CreatedAt = Clock.Now });
        if (status == "Studying" && classId is not null)
            Db.Account.Add(new Account { AccountId = id, PersonId = id, Username = $"hs{id}", PasswordHash = "x", AccountType = AccountTypes.Student, Status = "Active", CreatedAt = Clock.Now });
        Db.SaveChanges();
    }

    public void AddParent(int id, string name, string phone, int? studentId)
    {
        AddPerson(id, "Parent", name, phone: phone);
        if (studentId is not null) Db.ParentStudentLink.Add(new ParentStudentLink { ParentId = id, StudentId = studentId.Value, IsPrimary = true, Relationship = "Mẹ", CreatedAt = Clock.Now });
        Db.SaveChanges();
    }

    /// <summary>Chạy và kỳ vọng bị từ chối với mã 400 (có thể kèm đoạn thông báo).</summary>
    public static async Task Rejects(Func<Task> act, string? messagePart = null)
    {
        var ex = await Assert.ThrowsAsync<ApiException>(act);
        Assert.Equal(400, ex.StatusCode);
        if (messagePart is not null) Assert.Contains(messagePart, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    public static string Text(int length) => new('a', length);
    public void Dispose() => Db.Dispose();
}