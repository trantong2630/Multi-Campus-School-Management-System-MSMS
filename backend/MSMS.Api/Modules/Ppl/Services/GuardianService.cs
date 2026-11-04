using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;

namespace MSMS.Api.Modules.Ppl.Services;

/// <summary>
/// PPL-03: danh bạ phụ huynh và liên kết với học sinh (4.3.22–4.3.26). Số điện thoại là khóa nhận diện phụ huynh (BR-10, BR-11).
/// Mỗi học sinh có đúng một phụ huynh đại diện; hộp thư của phụ huynh cũ chuyển chỉ đọc vì liên kết không còn (BR-25).
/// </summary>
public sealed class GuardianService(MsmsDbContext db, IClock clock, ScopeResolver scope)
{
    public async Task<PagedResult<GuardianDto>> ListAsync(string? search, int page, int pageSize)
    {
        var query = db.Person.AsNoTracking().Where(p => p.PersonType == "Parent");
        // Hiệu phó chỉ thấy các con thuộc phân hiệu mình phụ trách: số con và tên các con cũng phải lọc theo phạm vi đó.
        Expression<Func<ParentStudentLink, bool>> linkScope = l => true;
        if (!scope.SeesWholeSchool)
        {
            var visible = await (await scope.FilterStudentsAsync(db.StudentProfile.AsNoTracking())).Select(s => s.StudentId).ToListAsync();
            linkScope = l => visible.Contains(l.StudentId);
            query = query.Where(p => p.ParentStudentLink.AsQueryable().Any(linkScope));
        }

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var phone = StudentImportService.NormalizePhone(term);
            query = query.Where(p => p.FullName.Contains(term) || p.PersonCode.Contains(term)
                                     || (phone.Length > 0 && p.PhoneNumber != null && p.PhoneNumber.Contains(phone)));
        }

        var paged = await query.OrderBy(p => p.FullName).ThenBy(p => p.PersonId)
            .Select(p => new GuardianDto(p.PersonId, p.PersonCode, p.FullName, p.PhoneNumber, p.Email, p.Address,
                p.ParentStudentLink.AsQueryable().Count(linkScope), null,
                p.Account == null ? null : p.Account.AccountId, p.Account == null ? null : p.Account.Status, p.Gender))
            .ToPagedAsync(page, pageSize);

        // Tên các con (tối đa 3) lấy ở truy vấn riêng cho trang hiện tại.
        var ids = paged.Items.Select(g => g.ParentId).ToList();
        var children = (await db.ParentStudentLink.AsNoTracking().Where(l => ids.Contains(l.ParentId)).Where(linkScope)
            .Select(l => new { l.ParentId, l.Student.Student.FullName }).ToListAsync())
            .GroupBy(x => x.ParentId).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.FullName).OrderBy(n => n).Take(3)));
        return paged with { Items = paged.Items.Select(g => g with { ChildrenSummary = children.GetValueOrDefault(g.ParentId) }).ToList() };
    }

    public async Task<GuardianDto> CreateAsync(GuardianRequest r)
    {
        var v = await ValidateAsync(null, r);
        var person = new Person
        {
            PersonType = "Parent", PersonCode = await PersonCodes.NextAsync(db, PersonCodes.Parent),
            FullName = v.FullName, PhoneNumber = v.Phone, Email = v.Email, Address = v.Address, Gender = v.Gender, CreatedAt = clock.Now,
        };
        db.Person.Add(person);
        await db.SaveChangesAsync();
        return await GetAsync(person.PersonId);
    }

    public async Task<GuardianDto> UpdateAsync(int id, GuardianRequest r)
    {
        var parent = await FindParentAsync(id);
        var v = await ValidateAsync(id, r);
        parent.FullName = v.FullName;
        parent.PhoneNumber = v.Phone;
        parent.Email = v.Email;
        parent.Address = v.Address;
        parent.Gender = v.Gender;
        parent.UpdatedAt = clock.Now;
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    /// <summary>4.3.25: các con của một phụ huynh, kèm lớp đang học và phân hiệu.</summary>
    public async Task<List<GuardianChildDto>> ChildrenAsync(int parentId)
    {
        await FindParentAsync(parentId);
        var active = EnrollmentRules.ActiveAt(clock.Today);
        var links = db.ParentStudentLink.AsNoTracking().Where(l => l.ParentId == parentId);
        if (!scope.SeesWholeSchool)
        {
            var visible = (await scope.FilterStudentsAsync(db.StudentProfile.AsNoTracking())).Select(s => s.StudentId);
            links = links.Where(l => visible.Contains(l.StudentId));
        }
        return await links.OrderBy(l => l.Student.Student.FullName)
            .Select(l => new GuardianChildDto(l.StudentId, l.Student.Student.PersonCode, l.Student.Student.FullName,
                l.Student.Enrollment.AsQueryable().Where(active).Select(e => e.Class.Name).FirstOrDefault(),
                l.Student.Enrollment.AsQueryable().Where(active).Select(e => e.Class.Campus.Name).FirstOrDefault(),
                l.Student.Status, l.Relationship, l.IsPrimary))
            .ToListAsync();
    }

    /// <summary>4.3.26: chuyển liên kết của một học sinh sang phụ huynh khác (có sẵn hoặc tạo mới) trong một transaction.</summary>
    public async Task ChangeRepresentativeAsync(int studentId, ChangeGuardianRequest r)
    {
        if ((r.ExistingParentId is null) == (r.NewGuardian is null))
            throw ApiException.BadRequest("Chọn một phụ huynh có sẵn hoặc khai phụ huynh mới, không chọn cả hai.");
        var visible = await scope.FilterStudentsAsync(db.StudentProfile.Where(s => s.StudentId == studentId));
        if (!await visible.AnyAsync()) throw ApiException.NotFound("Không tìm thấy hồ sơ học sinh.");

        var relationship = Guard.Optional(r.Relationship, "Quan hệ", 50);
        await using var tx = await db.Database.BeginTransactionAsync();

        int newParentId;
        if (r.ExistingParentId is int existing)
        {
            await FindParentAsync(existing);
            newParentId = existing;
        }
        else
        {
            var created = await CreateAsync(r.NewGuardian!);
            newParentId = created.ParentId;
        }

        var oldLinks = await db.ParentStudentLink.Where(l => l.StudentId == studentId).ToListAsync();
        if (oldLinks.Count == 1 && oldLinks[0].ParentId == newParentId)
            throw ApiException.BadRequest("Phụ huynh này đang là phụ huynh đại diện của học sinh. Chọn phụ huynh khác.");

        db.ParentStudentLink.RemoveRange(oldLinks);
        db.ParentStudentLink.Add(new ParentStudentLink
        {
            ParentId = newParentId, StudentId = studentId, Relationship = relationship ?? oldLinks.FirstOrDefault()?.Relationship,
            IsPrimary = true, CreatedAt = clock.Now,
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task<Person> FindParentAsync(int id) =>
        await db.Person.FirstOrDefaultAsync(p => p.PersonId == id && p.PersonType == "Parent")
        ?? throw ApiException.NotFound("Không tìm thấy hồ sơ phụ huynh.");

    private async Task<GuardianDto> GetAsync(int id) =>
        await db.Person.AsNoTracking().Where(p => p.PersonId == id)
            .Select(p => new GuardianDto(p.PersonId, p.PersonCode, p.FullName, p.PhoneNumber, p.Email, p.Address,
                p.ParentStudentLink.Count, null, p.Account == null ? null : p.Account.AccountId, p.Account == null ? null : p.Account.Status, p.Gender))
            .FirstAsync();

    private record Validated(string FullName, string Phone, string? Email, string? Address, string? Gender);

    private async Task<Validated> ValidateAsync(int? selfId, GuardianRequest r)
    {
        var name = Guard.Required(r.FullName, "Họ và tên", 150);

        // Số điện thoại bắt buộc và duy nhất giữa các phụ huynh: đó là khóa nhận diện khi import (BR-11).
        var phone = StudentImportService.NormalizePhone(r.PhoneNumber ?? "");
        if (phone.Length == 0) throw ApiException.BadRequest("Số điện thoại không được để trống. Đây là khóa nhận diện phụ huynh.");
        if (!StudentImportService.IsPhone(phone)) throw ApiException.BadRequest("Số điện thoại chỉ gồm 9 đến 15 chữ số.");
        var owner = await db.Person.Where(p => p.PersonType == "Parent" && p.PersonId != selfId && p.PhoneNumber == phone)
            .Select(p => new { p.PersonCode, p.FullName }).FirstOrDefaultAsync();
        if (owner is not null)
            throw ApiException.BadRequest($"Số điện thoại {phone} đã thuộc phụ huynh {owner.PersonCode} ({owner.FullName}). Dùng hồ sơ đó thay vì tạo mới.");

        var email = Guard.Optional(r.Email, "Email", 150);
        if (email is not null && !Guard.IsEmail(email)) throw ApiException.BadRequest("Email chưa đúng định dạng. Kiểm tra lại địa chỉ email.");

        var gender = Genders.Parse(r.Gender);
        if (gender == "?") throw ApiException.BadRequest("Giới tính chỉ gồm Nam, Nữ hoặc Khác.");

        return new Validated(name, phone, email, Guard.Optional(r.Address, "Địa chỉ", 255), gender);
    }
}