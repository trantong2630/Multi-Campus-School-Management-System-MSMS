using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Text;
using MSMS.Api.Common.Time;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Data;

/// <summary>Dữ liệu tạm cho môi trường Development: 1 phân hiệu + 6 tài khoản (mỗi loại một). Chỉ chạy khi chưa có tài khoản nào.</summary>
public static class DevSeeder
{
    public const string DefaultPassword = "Msms@123";

    private record Seed(string FullName, string PersonType, string Prefix, string AccountType, DateOnly Dob);

    public static async Task RunAsync(MsmsDbContext db, IClock clock, ILogger logger)
    {
        if (await db.Account.AnyAsync()) return;

        var campus = await db.Campus.FirstOrDefaultAsync();
        if (campus is null)
        {
            campus = new Campus { Code = "PH1", Name = "Phân hiệu 1", IsActive = true, CreatedAt = clock.Now };
            db.Campus.Add(campus);
            await db.SaveChangesAsync();
        }

        Seed[] seeds =
        [
            new("Trần Đức Kiên", "Staff", "CB", AccountTypes.SchoolAdmin, new(1990, 1, 15)),
            new("Nguyễn Văn Hùng", "Staff", "CB", AccountTypes.Principal, new(1975, 3, 2)),
            new("Lê Thị Mai", "Staff", "CB", AccountTypes.VicePrincipal, new(1980, 7, 21)),
            new("Phạm Minh Tuấn", "Staff", "CB", AccountTypes.Teacher, new(1988, 11, 9)),
            new("Hoàng Gia Bảo", "Student", "HS", AccountTypes.Student, new(2013, 5, 30)),
            new("Vũ Thị Lan", "Parent", "PH", AccountTypes.Parent, new(1985, 9, 12)),
        ];

        foreach (var s in seeds)
        {
            // NEXT VALUE FOR không dùng được trong subquery nên không gọi thêm toán tử LINQ nào sau SqlQuery.
            var number = (await db.Database.SqlQuery<int>($"SELECT NEXT VALUE FOR dbo.seq_PersonCode AS Value").ToListAsync())[0];
            var code = $"{s.Prefix}{number}";
            var person = new Person
            {
                PersonType = s.PersonType, PersonCode = code, FullName = s.FullName,
                DateOfBirth = s.Dob, PhoneNumber = s.PersonType == "Parent" ? "0900000001" : null,
                CreatedAt = clock.Now,
            };
            db.Person.Add(person);
            if (s.PersonType == "Staff") db.StaffProfile.Add(new StaffProfile { Staff = person, PrimaryCampusId = campus.CampusId, IsActive = true });
            if (s.PersonType == "Student") db.StudentProfile.Add(new StudentProfile { Student = person, Status = "Studying" });

            var account = new Account
            {
                Person = person, Username = UsernameGenerator.Build(s.FullName, code), AccountType = s.AccountType,
                Status = AccountStatuses.Active, CreatedAt = clock.Now, PasswordHash = "",
            };
            account.PasswordHash = AuthService.HashPassword(account, DefaultPassword);
            db.Account.Add(account);
            logger.LogInformation("Seed: {Type} {Username}", s.AccountType, account.Username);
        }
        await db.SaveChangesAsync();
    }
}
