using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Rules;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Ppl.Dtos;

namespace MSMS.Api.Modules.Ppl.Services;

/// <summary>
/// Tra cứu chủ nhiệm lớp theo khoảng hiệu lực (BR-09). Chỉ đọc; module khác gọi qua đây thay vì query HomeroomAssignment.
/// </summary>
public sealed class HomeroomService(MsmsDbContext db)
{
    /// <summary>Chủ nhiệm của một lớp đang hiệu lực tại ngày <paramref name="date"/>; không có thì trả null.</summary>
    public async Task<HomeroomDto?> GetHomeroomAsync(int classId, DateOnly date)
    {
        var rows = await ListHomeroomsAsync([classId], date);
        return rows.FirstOrDefault();
    }

    /// <summary>Chủ nhiệm của các lớp đang hiệu lực tại ngày <paramref name="date"/>; lớp không có chủ nhiệm thì không xuất hiện.</summary>
    public async Task<IReadOnlyList<HomeroomDto>> ListHomeroomsAsync(IReadOnlyCollection<int> classIds, DateOnly date)
    {
        if (classIds.Count == 0)
            return [];

        var rows = await db.HomeroomAssignment.AsNoTracking()
            .Where(EffectivePeriod.EffectiveAt<HomeroomAssignment>(date))
            .Where(h => classIds.Contains(h.ClassId))
            .OrderByDescending(h => h.StartDate)
            .Select(h => new
            {
                h.ClassId,
                h.StaffId,
                FullName = h.Staff.Staff.FullName,
                h.Staff.TeacherAbbr
            })
            .ToListAsync();

        return rows
            .Select(h => new HomeroomDto(h.ClassId, h.StaffId, h.FullName, h.TeacherAbbr))
            .ToList();
    }

    /// <summary>Các lớp mà giáo viên đang làm chủ nhiệm tại ngày <paramref name="date"/>.</summary>
    public async Task<IReadOnlyList<int>> ListHomeroomClassIdsAsync(int staffId, DateOnly date)
    {
        return await db.HomeroomAssignment.AsNoTracking()
            .Where(EffectivePeriod.EffectiveAt<HomeroomAssignment>(date))
            .Where(h => h.StaffId == staffId)
            .Select(h => h.ClassId)
            .ToListAsync();
    }
}
