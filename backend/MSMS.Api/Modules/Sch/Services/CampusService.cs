using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;
using MSMS.Api.Common.Time;

namespace MSMS.Api.Modules.Sch.Services;

public sealed class CampusService(MsmsDbContext db, IClock clock)
{
    public async Task<List<CampusDto>> ListAsync() =>
        await db.Campus.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new CampusDto(c.CampusId, c.Code, c.Name, c.Address, c.PhoneNumber, c.IsActive,
                c.Class.Count(x => x.IsActive)))
            .ToListAsync();

    public async Task<CampusDto> CreateAsync(CampusRequest r)
    {
        var code = Guard.Required(r.Code, "Mã phân hiệu", 20);
        var name = Guard.Required(r.Name, "Tên phân hiệu", 150);
        await EnsureUniqueAsync(null, code, name);

        var campus = new Campus
        {
            Code = code, Name = name, Address = Guard.Optional(r.Address, "Địa chỉ", 255),
            PhoneNumber = Guard.Optional(r.PhoneNumber, "Số điện thoại", 20), IsActive = true, CreatedAt = clock.Now,
        };
        db.Campus.Add(campus);
        await db.SaveChangesAsync();
        return new CampusDto(campus.CampusId, campus.Code, campus.Name, campus.Address, campus.PhoneNumber, true, 0);
    }

    public async Task UpdateAsync(int id, CampusRequest r)
    {
        var campus = await db.Campus.FindAsync(id) ?? throw ApiException.NotFound("Không tìm thấy phân hiệu.");
        var code = Guard.Required(r.Code, "Mã phân hiệu", 20);
        var name = Guard.Required(r.Name, "Tên phân hiệu", 150);
        await EnsureUniqueAsync(id, code, name);

        campus.Code = code;
        campus.Name = name;
        campus.Address = Guard.Optional(r.Address, "Địa chỉ", 255);
        campus.PhoneNumber = Guard.Optional(r.PhoneNumber, "Số điện thoại", 20);
        await db.SaveChangesAsync();
    }

    /// <summary>Danh mục đã có dữ liệu tham chiếu thì ngừng hoạt động, không xóa (BR-07).</summary>
    public async Task SetActiveAsync(int id, bool isActive)
    {
        var campus = await db.Campus.FindAsync(id) ?? throw ApiException.NotFound("Không tìm thấy phân hiệu.");
        if (!isActive)
        {
            var classes = await db.Class.CountAsync(c => c.CampusId == id && c.IsActive);
            if (classes > 0)
                throw ApiException.BadRequest($"Phân hiệu còn {classes} lớp đang hoạt động. Ngừng hoạt động các lớp đó trước.");
        }
        campus.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    private async Task EnsureUniqueAsync(int? selfId, string code, string name)
    {
        if (await db.Campus.AnyAsync(c => c.CampusId != selfId && c.Code == code))
            throw ApiException.BadRequest($"Mã phân hiệu \"{code}\" đã có. Chọn mã khác.");
        if (await db.Campus.AnyAsync(c => c.CampusId != selfId && c.Name == name))
            throw ApiException.BadRequest($"Tên phân hiệu \"{name}\" đã có. File import tra cứu theo tên nên không được trùng.");
    }
}
