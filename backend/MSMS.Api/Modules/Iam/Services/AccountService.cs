using System.Security.Cryptography;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Notifications;
using MSMS.Api.Common.Excel;
using MSMS.Api.Common.Text;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Iam.Dtos;

namespace MSMS.Api.Modules.Iam.Services;

/// <summary>IAM-02: quản lý tài khoản. Chỉ School Admin; mỗi hồ sơ đúng một tài khoản (BR-01).</summary>
public sealed class AccountService(MsmsDbContext db, CurrentUser user, IClock clock, AuditWriter audit, SessionService sessions, NotificationSender notifier)
{
    private static readonly string[] StaffTypes =
        [AccountTypes.SchoolAdmin, AccountTypes.Principal, AccountTypes.VicePrincipal, AccountTypes.Teacher];

    public Task<PagedResult<AccountDto>> ListAsync(string? search, string? accountType, string? status, int? campusId, int page, int pageSize)
    {
        var query = db.Account.AsNoTracking()
            .Where(a => accountType == null || a.AccountType == accountType)
            .Where(a => status == null || a.Status == status)
            .Where(a => campusId == null || (a.Person.StaffProfile != null && a.Person.StaffProfile.PrimaryCampusId == campusId));

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            // Tìm phân biệt dấu (collation Vietnamese_CI_AS của DB): gõ có dấu mới khớp, không phân biệt hoa thường.
            query = query.Where(a => a.Person.FullName.Contains(term) || a.Person.PersonCode.Contains(term) || a.Username.Contains(term));
        }

        return query.OrderBy(a => a.Person.FullName)
            .Select(a => new AccountDto(a.AccountId, a.PersonId, a.Person.PersonCode, a.Person.FullName, a.Person.PersonType,
                a.Username, a.AccountType, a.Status, a.LastLoginAt,
                a.Person.StaffProfile == null ? null : a.Person.StaffProfile.PrimaryCampus.Name))
            .ToPagedAsync(page, pageSize);
    }

    public async Task<TempPasswordItem> CreateAsync(CreateAccountRequest r)
    {
        var result = await BulkCreateAsync(new BulkCreateAccountsRequest([r.PersonId], r.AccountType));
        if (result.Created.Count == 1) return result.Created[0];
        throw ApiException.BadRequest(result.Skipped.FirstOrDefault()?.Reason ?? "Không tạo được tài khoản.");
    }

    /// <summary>
    /// Danh bạ học sinh → Student, phụ huynh → Parent, cán bộ → một loại chọn cho cả lô. Hồ sơ đã có tài khoản thì bỏ qua (BR-04).
    /// Đơn lẻ và hàng loạt cùng một đường đi để cùng một bộ quy tắc.
    /// </summary>
    public async Task<BulkCreateResult> BulkCreateAsync(BulkCreateAccountsRequest r)
    {
        if (r.PersonIds is null || r.PersonIds.Count == 0) throw ApiException.BadRequest("Chọn ít nhất một hồ sơ để tạo tài khoản.");
        var people = await db.Person.Include(p => p.Account).Include(p => p.StaffProfile)
            .Where(p => r.PersonIds.Contains(p.PersonId)).ToListAsync();

        var created = new List<(Account Account, Person Person, string Password)>();
        var skipped = new List<SkippedAccount>();

        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var person in people.OrderBy(p => p.PersonId))
        {
            if (person.Account is not null)
            {
                skipped.Add(new SkippedAccount(person.PersonId, person.FullName, "Hồ sơ đã có tài khoản."));
                continue;
            }
            var type = person.PersonType switch
            {
                "Student" => AccountTypes.Student,
                "Parent" => AccountTypes.Parent,
                _ => r.AccountType,
            };
            if (person.PersonType == "Staff")
            {
                if (type is null || !StaffTypes.Contains(type))
                {
                    skipped.Add(new SkippedAccount(person.PersonId, person.FullName, "Chọn loại tài khoản cho cán bộ: Quản trị hệ thống, Hiệu trưởng, Hiệu phó hoặc Giáo viên."));
                    continue;
                }
                if (person.StaffProfile is { IsActive: false })
                {
                    skipped.Add(new SkippedAccount(person.PersonId, person.FullName, "Hồ sơ cán bộ đã ngừng hoạt động."));
                    continue;
                }
            }

            var password = TempPasswords.Generate();
            var account = new Account
            {
                Person = person, Username = UsernameGenerator.Build(person.FullName, person.PersonCode), AccountType = type!,
                Status = AccountStatuses.PendingPasswordChange, CreatedAt = clock.Now, PasswordHash = "",
            };
            account.PasswordHash = AuthService.HashPassword(account, password);
            db.Account.Add(account);
            created.Add((account, person, password));
        }
        await db.SaveChangesAsync();

        if (created.Count > 0)
        {
            audit.Add(AuditCategories.Account, created.Count == 1 ? "CreateAccount" : "BulkCreateAccounts",
                created.Select(c => c.Account.AccountId), new { count = created.Count });
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();

        var items = created.Select(c => new TempPasswordItem(c.Person.PersonId, c.Person.PersonCode, c.Person.FullName,
            c.Account.Username, c.Account.AccountType, c.Password)).ToList();
        return new BulkCreateResult(items, skipped);
    }

    /// <summary>Xem trước hệ quả khóa tài khoản để màn hình cảnh báo trước khi xác nhận (BR-05).</summary>
    public async Task<LockImpact> LockImpactAsync(int accountId)
    {
        var account = await FindAsync(accountId);
        var sessions = account.AccountType == AccountTypes.Teacher ? await AffectedSessionsAsync(account.PersonId) : [];
        var byCampus = sessions.GroupBy(s => s.Class.Campus.Name)
            .Select(g => new CampusSessionCount(g.Key, g.Count())).ToList();
        var duties = (await EffectiveDutiesAsync(account.PersonId)).Select(d => new DutyImpact(d.Kind, d.TargetName)).ToList();
        return new LockImpact(sessions.Count, byCampus, duties);
    }

    /// <summary>
    /// Khóa tài khoản không bị chặn bởi lịch dạy. Cùng transaction: tiết Đã xếp lịch từ hôm nay của Teacher chuyển Chờ xử lý,
    /// trách nhiệm và phụ trách chuyên môn đang hiệu lực kết thúc vào ngày khóa (BR-05, BR-09).
    /// </summary>
    public async Task LockAsync(int accountId)
    {
        var account = await FindAsync(accountId);
        if (account.AccountId == user.AccountId) throw ApiException.BadRequest("Không khóa được tài khoản đang đăng nhập.");
        if (account.Status == AccountStatuses.Locked) throw ApiException.BadRequest("Tài khoản đã bị khóa từ trước.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var (sessionCount, dutyCount) = await ReleaseDutiesAsync(account);
        account.Status = AccountStatuses.Locked;
        audit.Add(AuditCategories.Account, "LockAccount", [account.AccountId], new { pendingSessions = sessionCount, closedDuties = dutyCount });
        await sessions.RevokeAllAsync(account.AccountId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task<List<TeachingSession>> AffectedSessionsAsync(int teacherId)
    {
        var today = clock.Today;
        return await db.TeachingSession.Include(s => s.Class).ThenInclude(c => c.Campus)
            .Where(s => s.TeacherId == teacherId && s.Status == "Scheduled" && s.SessionDate >= today
                        && !s.AttendanceRecord.Any())
            .ToListAsync();
    }

    private record Duty(string Kind, string TargetName, IEffectivePeriod Record);

    private async Task<List<Duty>> EffectiveDutiesAsync(int staffId)
    {
        var today = clock.Today;
        var duties = new List<Duty>();
        duties.AddRange((await db.CampusLeadership.Include(x => x.Campus)
            .Where(x => x.StaffId == staffId && (x.EndDate == null || x.EndDate >= today)).ToListAsync())
            .Select(x => new Duty("Campus", x.Campus.Name, x)));
        duties.AddRange((await db.DepartmentHead.Include(x => x.Department)
            .Where(x => x.StaffId == staffId && (x.EndDate == null || x.EndDate >= today)).ToListAsync())
            .Select(x => new Duty("Department", x.Department.Name, x)));
        duties.AddRange((await db.HomeroomAssignment.Include(x => x.Class)
            .Where(x => x.StaffId == staffId && (x.EndDate == null || x.EndDate >= today)).ToListAsync())
            .Select(x => new Duty("Homeroom", x.Class.Name, x)));
        return duties;
    }

    /// <summary>
    /// Dùng chung cho khóa và đổi loại tài khoản: tiết tương lai của Teacher sang Chờ xử lý và để trống giáo viên,
    /// Vice Principal phân hiệu chứa tiết nhận thông báo; trách nhiệm đang hiệu lực kết thúc vào ngày hôm nay.
    /// Chỉ thêm vào context, người gọi SaveChanges trong transaction của mình.
    /// </summary>
    private async Task<(int Sessions, int Duties)> ReleaseDutiesAsync(Account account)
    {
        var today = clock.Today;
        var affected = account.AccountType == AccountTypes.Teacher ? await AffectedSessionsAsync(account.PersonId) : [];
        foreach (var s in affected)
        {
            s.Status = "Pending";
            s.Source = "Manual";
            s.UpdatedByPersonId = user.PersonId;
            s.UpdatedAt = clock.Now;
            s.TeacherId = null;
            s.TeacherAbsenceId = null;
            s.ChangeNote = "Giáo viên không còn dùng tài khoản";
        }

        foreach (var campusGroup in affected.GroupBy(s => s.Class.CampusId))
        {
            var viceIds = await db.CampusLeadership
                .Where(x => x.CampusId == campusGroup.Key && (x.EndDate == null || x.EndDate >= today) && x.StartDate <= today)
                .Select(x => x.StaffId).ToListAsync();
            notifier.Add(viceIds, NotificationTypes.SessionPending, "Có tiết chuyển Chờ xử lý",
                $"{campusGroup.Count()} tiết của {account.Person.FullName} cần đổi giáo viên hoặc hủy vì tài khoản không còn hoạt động.");
        }

        var duties = await EffectiveDutiesAsync(account.PersonId);
        foreach (var d in duties)
        {
            if (d.Record.StartDate > today) d.Record.EndDate = d.Record.StartDate;   // chưa hiệu lực: rút về đúng ngày bắt đầu
            else d.Record.EndDate = today;
        }
        return (affected.Count, duties.Count);
    }

    /// <summary>
    /// Mở khóa. UpdatedAt của tài khoản là lần đổi mật khẩu gần nhất (null = chưa từng đổi mật khẩu lần đầu),
    /// nên chưa từng đổi thì quay về Chờ đổi mật khẩu (BR-05).
    /// </summary>
    public async Task UnlockAsync(int accountId)
    {
        var account = await FindAsync(accountId);
        if (account.Status != AccountStatuses.Locked) throw ApiException.BadRequest("Tài khoản không ở trạng thái Đã khóa.");
        account.Status = account.UpdatedAt is null ? AccountStatuses.PendingPasswordChange : AccountStatuses.Active;
        audit.Add(AuditCategories.Account, "UnlockAccount", [account.AccountId]);
        await db.SaveChangesAsync();
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(int accountId)
    {
        var account = await FindAsync(accountId);
        await using var tx = await db.Database.BeginTransactionAsync();
        var password = TempPasswords.Generate();
        account.PasswordHash = AuthService.HashPassword(account, password);
        account.Status = AccountStatuses.PendingPasswordChange;
        account.UpdatedAt = null;
        audit.Add(AuditCategories.Account, "ResetPassword", [account.AccountId]);   // không ghi mật khẩu
        await sessions.RevokeAllAsync(account.AccountId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new ResetPasswordResult(account.Username, password);
    }

    /// <summary>Cấp sai loại thì đổi loại: ghi Audit, cấp lại mật khẩu tạm, về Chờ đổi mật khẩu. Hồ sơ và lịch sử giữ nguyên (BR-01).</summary>
    public async Task<ResetPasswordResult> ChangeTypeAsync(int accountId, ChangeAccountTypeRequest r)
    {
        var account = await FindAsync(accountId);
        if (account.AccountId == user.AccountId) throw ApiException.BadRequest("Không đổi loại của tài khoản đang đăng nhập.");
        var allowed = account.Person.PersonType switch
        {
            "Student" => new[] { AccountTypes.Student },
            "Parent" => new[] { AccountTypes.Parent },
            _ => StaffTypes,
        };
        if (!allowed.Contains(r.AccountType)) throw ApiException.BadRequest("Loại tài khoản không phù hợp với loại hồ sơ.");
        if (account.AccountType == r.AccountType) throw ApiException.BadRequest("Tài khoản đã đúng loại này.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var (sessionCount, dutyCount) = await ReleaseDutiesAsync(account);   // loại cũ chưa bị đổi nên Teacher vẫn nhận diện được
        var password = TempPasswords.Generate();
        var from = account.AccountType;
        account.AccountType = r.AccountType;
        account.PasswordHash = AuthService.HashPassword(account, password);
        account.Status = AccountStatuses.PendingPasswordChange;
        account.UpdatedAt = null;
        audit.Add(AuditCategories.Account, "ChangeAccountType", [account.AccountId],
            new { from, to = r.AccountType, pendingSessions = sessionCount, closedDuties = dutyCount });
        await sessions.RevokeAllAsync(account.AccountId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new ResetPasswordResult(account.Username, password);
    }

    /// <summary>File mật khẩu tạm sinh tức thời từ kết quả vừa tạo, không lưu, không xuất lại (BR-04).</summary>
    public FileContentResult BuildTempPasswordSheet(TempPasswordSheetRequest r)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Mat_khau_tam");
        string[] headers = ["STT", "Mã hồ sơ", "Họ và tên", "Tên đăng nhập", "Loại tài khoản", "Mật khẩu tạm"];
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Range(1, 1, 1, headers.Length).StyleHeader();
        var row = 2;
        foreach (var item in r.Items)
        {
            ws.Cell(row, 1).Value = row - 1;
            ws.Cell(row, 2).Value = item.PersonCode;
            ws.Cell(row, 3).Value = item.FullName;
            ws.Cell(row, 4).Value = item.Username;
            ws.Cell(row, 5).Value = item.AccountType;
            ws.Cell(row, 6).Value = item.TempPassword;
            row++;
        }
        ws.Columns().AdjustToContents();
        return wb.ToFile("MSMS_Mat_khau_tam.xlsx");
    }

    private async Task<Account> FindAsync(int accountId) =>
        await db.Account.Include(a => a.Person).FirstOrDefaultAsync(a => a.AccountId == accountId)
        ?? throw ApiException.NotFound("Không tìm thấy tài khoản.");
}

public static class TempPasswords
{
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "@#$%";

    /// <summary>12 ký tự ngẫu nhiên, loại bỏ ký tự dễ nhầm (0/O, 1/l/I) vì mật khẩu được bàn giao bằng giấy.</summary>
    public static string Generate()
    {
        var chars = new List<char>(12);
        for (var i = 0; i < 8; i++) chars.Add(Letters[RandomNumberGenerator.GetInt32(Letters.Length)]);
        for (var i = 0; i < 3; i++) chars.Add(Digits[RandomNumberGenerator.GetInt32(Digits.Length)]);
        chars.Add(Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)]);
        RandomNumberGenerator.Shuffle(CollectionsMarshalSpan(chars));
        return new string(chars.ToArray());
    }

    private static Span<char> CollectionsMarshalSpan(List<char> list) =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list);
}
