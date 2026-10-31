using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Text;
using MSMS.Api.Common.Time;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;
using MSMS.Api.Modules.Sch.Services;

namespace MSMS.Api.Data;

/// <summary>
/// Dữ liệu tạm cho môi trường Development, đủ để demo từ đăng nhập đến import và phát hành thời khóa biểu:
/// 1 phân hiệu, năm học 2026-2027 (2 học kỳ), 10 khung giờ tiết (5 sáng + 5 chiều), 3 tổ, 6 môn,
/// 3 lớp, 6 giáo viên có viết tắt, chủ nhiệm, phân công giảng dạy, học sinh được ghi danh và phụ huynh liên kết.
/// Chỉ chạy khi chưa có tài khoản nào.
/// </summary>
public static class DevSeeder
{
    public const string DefaultPassword = "Msms@123";

    public static async Task RunAsync(MsmsDbContext db, IClock clock, ILogger logger)
    {
        if (await db.Account.AnyAsync())
        {
            await EnsureMissingParentsAsync(db, clock, logger);
            return;
        }
        var now = clock.Now;

        // ---- Phân hiệu ----
        var campus = await db.Campus.FirstOrDefaultAsync();
        if (campus is null)
        {
            campus = new Campus { Code = "PH1", Name = "Phân hiệu 1", IsActive = true, CreatedAt = now };
            db.Campus.Add(campus);
        }

        // ---- Năm học và học kỳ ----
        var yearStart = new DateOnly(2026, 9, 5);
        var year = await db.AcademicYear.FirstOrDefaultAsync(y => y.Name == "2026-2027");
        if (year is null)
        {
            year = new AcademicYear
            {
                Name = "2026-2027", StartDate = yearStart, EndDate = new(2027, 5, 31), IsCurrent = !await db.AcademicYear.AnyAsync(),
            };
            year.Semester.Add(new Semester { SemesterNo = 1, Name = "Học kỳ 1", StartDate = yearStart, EndDate = new(2027, 1, 15) });
            year.Semester.Add(new Semester { SemesterNo = 2, Name = "Học kỳ 2", StartDate = new(2027, 1, 18), EndDate = new(2027, 5, 31) });
            db.AcademicYear.Add(year);
        }

        // ---- Khung giờ tiết: 5 sáng + 5 chiều ----
        if (!await db.PeriodSlot.AnyAsync())
        {
            (string Session, byte No, string Start, string End)[] slots =
            [
                (SessionOfDay.Morning, 1, "07:00", "07:45"), (SessionOfDay.Morning, 2, "07:50", "08:35"),
                (SessionOfDay.Morning, 3, "08:55", "09:40"), (SessionOfDay.Morning, 4, "09:45", "10:30"),
                (SessionOfDay.Morning, 5, "10:35", "11:20"),
                (SessionOfDay.Afternoon, 1, "13:30", "14:15"), (SessionOfDay.Afternoon, 2, "14:20", "15:05"),
                (SessionOfDay.Afternoon, 3, "15:20", "16:05"), (SessionOfDay.Afternoon, 4, "16:10", "16:55"),
                (SessionOfDay.Afternoon, 5, "17:00", "17:45"),
            ];
            foreach (var (session, no, start, end) in slots)
                db.PeriodSlot.Add(new PeriodSlot
                {
                    SessionOfDay = session, PeriodNo = no, StartTime = TimeOnly.Parse(start), EndTime = TimeOnly.Parse(end), IsActive = true,
                });
        }

        // ---- Tổ chuyên môn và môn học ----
        var deptNames = new[] { "Tổ Toán - Tin", "Tổ Khoa học tự nhiên", "Tổ Ngữ văn - Xã hội", "Tổ Ngoại ngữ" };
        var depts = deptNames.Select(n => new SubjectDepartment { Name = n, IsActive = true }).ToArray();
        db.SubjectDepartment.AddRange(depts);
        await db.SaveChangesAsync();

        (string Name, string Abbr, int Dept)[] subjectDefs =
        [
            ("Toán", "TOAN", 0), ("Vật lí", "LY", 1), ("Hóa học", "HOA", 1),
            ("Ngữ văn", "VAN", 2), ("Lịch sử", "SU", 2), ("Tiếng Anh", "ANH", 3),
        ];
        var subjects = subjectDefs.ToDictionary(
            s => s.Abbr, s => new Subject { DepartmentId = depts[s.Dept].DepartmentId, Name = s.Name, Abbreviation = s.Abbr, IsActive = true });
        db.Subject.AddRange(subjects.Values);

        // ---- Lớp ----
        var classes = new[] { ("6A1", (byte)6), ("6A2", (byte)6), ("7A1", (byte)7) }
            .Select(c => new Class { Campus = campus, AcademicYear = year, Name = c.Item1, GradeLevel = c.Item2, IsActive = true, CreatedAt = now })
            .ToArray();
        db.Class.AddRange(classes);
        await db.SaveChangesAsync();

        // ---- Người dùng: cán bộ, giáo viên, học sinh, phụ huynh ----
        async Task<Person> AddPersonAsync(string fullName, string personType, string prefix, string accountType, DateOnly dob,
            string gender, string? phone = null)
        {
            // NEXT VALUE FOR không dùng được trong subquery nên không gọi thêm toán tử LINQ nào sau SqlQuery.
            var number = (await db.Database.SqlQuery<int>($"SELECT NEXT VALUE FOR dbo.seq_PersonCode AS Value").ToListAsync())[0];
            var code = $"{prefix}{number}";
            var person = new Person
            {
                PersonType = personType, PersonCode = code, FullName = fullName, DateOfBirth = dob, Gender = gender,
                PhoneNumber = phone, CreatedAt = now,
            };
            db.Person.Add(person);
            var account = new Account
            {
                Person = person, Username = UsernameGenerator.Build(fullName, code), AccountType = accountType,
                Status = AccountStatuses.Active, CreatedAt = now, PasswordHash = "",
            };
            account.PasswordHash = AuthService.HashPassword(account, DefaultPassword);
            db.Account.Add(account);
            logger.LogInformation("Seed: {Type} {Username}", accountType, account.Username);
            return person;
        }

        Person AddStaff(Person person, string? position, string? abbr = null, int? deptIdx = null)
        {
            db.StaffProfile.Add(new StaffProfile
            {
                Staff = person, PrimaryCampusId = campus.CampusId, IsActive = true, Position = position, TeacherAbbr = abbr,
                DepartmentId = deptIdx is null ? null : depts[deptIdx.Value].DepartmentId, HireDate = new(2015, 9, 1),
            });
            return person;
        }

        AddStaff(await AddPersonAsync("Trần Đức Kiên", "Staff", "CB", AccountTypes.SchoolAdmin, new(1990, 1, 15), "Male"), "Quản trị nhà trường");
        var principal = AddStaff(await AddPersonAsync("Nguyễn Văn Hùng", "Staff", "CB", AccountTypes.Principal, new(1975, 3, 2), "Male"), "Hiệu trưởng");
        var vice = AddStaff(await AddPersonAsync("Lê Thị Mai", "Staff", "CB", AccountTypes.VicePrincipal, new(1980, 7, 21), "Female"), "Phó hiệu trưởng");

        // Mỗi giáo viên dạy một môn ở cả 3 lớp; ba giáo viên đầu làm chủ nhiệm 6A1, 6A2, 7A1.
        (string Name, DateOnly Dob, string Gender, string Abbr, string Subject)[] teacherDefs =
        [
            ("Phạm Minh Tuấn", new(1988, 11, 9), "Male", "PMT", "TOAN"),
            ("Nguyễn Thị Hoa", new(1986, 4, 18), "Female", "NTH", "VAN"),
            ("Trần Quốc Bảo", new(1990, 8, 3), "Male", "TQB", "ANH"),
            ("Lý Văn Dũng", new(1984, 2, 27), "Male", "LVD", "LY"),
            ("Đặng Thu Hà", new(1989, 12, 5), "Female", "DTH", "HOA"),
            ("Bùi Văn Sơn", new(1983, 6, 14), "Male", "BVS", "SU"),
        ];
        var teachers = new List<(Person Person, string Subject)>();
        foreach (var t in teacherDefs)
        {
            var person = await AddPersonAsync(t.Name, "Staff", "CB", AccountTypes.Teacher, t.Dob, t.Gender);
            var deptIdx = subjectDefs.First(s => s.Abbr == t.Subject).Dept;
            AddStaff(person, "Giáo viên", t.Abbr, deptIdx);
            teachers.Add((person, t.Subject));
        }

        // Học sinh: 3 em mỗi lớp. Phụ huynh liên kết: lớp 6A1 có phụ huynh cho cả 3 em (Hoàng Gia Bảo, Đỗ Minh Anh, Ngô Quang Huy), lớp 6A2 và 7A1 liên kết em đầu tiên.
        (string Name, DateOnly Dob, string Gender)[][] studentDefs =
        [
            [("Hoàng Gia Bảo", new(2014, 5, 30), "Male"), ("Đỗ Minh Anh", new(2014, 2, 11), "Female"), ("Ngô Quang Huy", new(2014, 9, 23), "Male")],
            [("Phan Khánh Linh", new(2014, 7, 8), "Female"), ("Trịnh Đức Long", new(2014, 1, 19), "Male"), ("Vương Thảo Nhi", new(2014, 11, 2), "Female")],
            [("Mai Anh Khoa", new(2013, 3, 15), "Male"), ("Cao Bảo Ngọc", new(2013, 10, 27), "Female"), ("Lương Gia Hân", new(2013, 6, 6), "Female")],
        ];
        var parentDefsByStudent = new Dictionary<string, (string Name, DateOnly Dob, string Gender, string Phone)>
        {
            ["Hoàng Gia Bảo"] = ("Vũ Thị Lan", new(1985, 9, 12), "Female", "0900000001"),
            ["Đỗ Minh Anh"] = ("Đỗ Minh Tuấn", new(1984, 3, 15), "Male", "0900000004"),
            ["Ngô Quang Huy"] = ("Ngô Quang Hải", new(1982, 11, 20), "Male", "0900000005"),
            ["Phan Khánh Linh"] = ("Phan Văn Nam", new(1982, 5, 20), "Male", "0900000002"),
            ["Mai Anh Khoa"] = ("Mai Thị Hương", new(1983, 8, 9), "Female", "0900000003"),
        };
        var students = new List<(Person Person, int ClassIdx)>();
        var parentLinks = new List<(Person Parent, Person Student)>();
        for (var ci = 0; ci < classes.Length; ci++)
            for (var si = 0; si < studentDefs[ci].Length; si++)
            {
                var d = studentDefs[ci][si];
                var student = await AddPersonAsync(d.Name, "Student", "HS", AccountTypes.Student, d.Dob, d.Gender);
                db.StudentProfile.Add(new StudentProfile { Student = student, Status = "Studying", AdmissionDate = yearStart });
                students.Add((student, ci));
                if (parentDefsByStudent.TryGetValue(d.Name, out var p))
                {
                    parentLinks.Add((await AddPersonAsync(p.Name, "Parent", "PH", AccountTypes.Parent, p.Dob, p.Gender, p.Phone), student));
                }
            }
        await db.SaveChangesAsync();

        // ---- Phân công và ghi danh (cần khóa đã sinh ở trên) ----
        db.CampusLeadership.Add(new CampusLeadership { CampusId = campus.CampusId, StaffId = principal.PersonId, StartDate = yearStart, CreatedAt = now });
        db.CampusLeadership.Add(new CampusLeadership { CampusId = campus.CampusId, StaffId = vice.PersonId, StartDate = yearStart, CreatedAt = now });

        // Tổ trưởng: giáo viên đầu tiên của mỗi tổ có giáo viên.
        foreach (var group in teachers.GroupBy(t => subjectDefs.First(s => s.Abbr == t.Subject).Dept))
            db.DepartmentHead.Add(new DepartmentHead
            {
                DepartmentId = depts[group.Key].DepartmentId, StaffId = group.First().Person.PersonId, StartDate = yearStart, CreatedAt = now,
            });

        for (var ci = 0; ci < classes.Length; ci++)
        {
            db.HomeroomAssignment.Add(new HomeroomAssignment
            {
                ClassId = classes[ci].ClassId, StaffId = teachers[ci].Person.PersonId, StartDate = yearStart, CreatedAt = now,
            });
        }
        foreach (var (person, ci) in students)
            db.Enrollment.Add(new Enrollment { StudentId = person.PersonId, ClassId = classes[ci].ClassId, StartDate = yearStart, CreatedAt = now });
        foreach (var (parent, student) in parentLinks)
            db.ParentStudentLink.Add(new ParentStudentLink
            {
                ParentId = parent.PersonId, StudentId = student.PersonId, Relationship = "Phụ huynh", IsPrimary = true, CreatedAt = now,
            });
        await db.SaveChangesAsync();
    }

    private static async Task EnsureMissingParentsAsync(MsmsDbContext db, IClock clock, ILogger logger)
    {
        (string StudentName, string ParentName, DateOnly Dob, string Gender, string Phone)[] targets =
        [
            ("Đỗ Minh Anh", "Đỗ Minh Tuấn", new(1984, 3, 15), "Male", "0900000004"),
            ("Ngô Quang Huy", "Ngô Quang Hải", new(1982, 11, 20), "Male", "0900000005"),
        ];

        var now = clock.Now;
        foreach (var (studentName, parentName, dob, gender, phone) in targets)
        {
            var student = await db.Person.FirstOrDefaultAsync(p => p.FullName == studentName && p.PersonType == "Student");
            if (student is null) continue;

            var hasLink = await db.ParentStudentLink.AnyAsync(l => l.StudentId == student.PersonId);
            if (hasLink) continue;

            var parent = await db.Person.FirstOrDefaultAsync(p => p.FullName == parentName && p.PersonType == "Parent");
            if (parent is null)
            {
                var number = (await db.Database.SqlQuery<int>($"SELECT NEXT VALUE FOR dbo.seq_PersonCode AS Value").ToListAsync())[0];
                var code = $"PH{number}";
                parent = new Person
                {
                    PersonType = "Parent",
                    PersonCode = code,
                    FullName = parentName,
                    DateOfBirth = dob,
                    Gender = gender,
                    PhoneNumber = phone,
                    CreatedAt = now,
                };
                db.Person.Add(parent);

                var account = new Account
                {
                    Person = parent,
                    Username = UsernameGenerator.Build(parentName, code),
                    AccountType = AccountTypes.Parent,
                    Status = AccountStatuses.Active,
                    CreatedAt = now,
                    PasswordHash = "",
                };
                account.PasswordHash = AuthService.HashPassword(account, DefaultPassword);
                db.Account.Add(account);
                await db.SaveChangesAsync();

                logger.LogInformation("EnsureMissingParents: Tạo phụ huynh {Username} ({ParentName})", account.Username, parentName);
            }

            db.ParentStudentLink.Add(new ParentStudentLink
            {
                ParentId = parent.PersonId,
                StudentId = student.PersonId,
                Relationship = "Phụ huynh",
                IsPrimary = true,
                CreatedAt = now,
            });
            await db.SaveChangesAsync();

            logger.LogInformation("EnsureMissingParents: Đã liên kết phụ huynh {ParentName} với học sinh {StudentName}", parentName, studentName);
        }
    }
}
