using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Sch.Dtos;

namespace MSMS.Api.Modules.Sch.Services;

public static class SessionOfDay
{
    public const string Morning = "Morning";
    public const string Afternoon = "Afternoon";

    public static string Label(string value) => value == Morning ? "Sáng" : "Chiều";
}

/// <summary>SCH-04: khung giờ dùng chung toàn trường, khóa là cặp (Buổi, Tiết) (BR-07).</summary>
public sealed class PeriodSlotService(MsmsDbContext db)
{
    public async Task<List<PeriodSlotDto>> ListAsync() =>
        await db.PeriodSlot.AsNoTracking()
            .OrderBy(p => p.SessionOfDay == SessionOfDay.Morning ? 0 : 1).ThenBy(p => p.PeriodNo)
            .Select(p => new PeriodSlotDto(p.PeriodSlotId, p.SessionOfDay, p.PeriodNo, p.StartTime, p.EndTime, p.IsActive))
            .ToListAsync();

    public async Task<PeriodSlotDto> CreateAsync(PeriodSlotRequest r)
    {
        if (r.SessionOfDay != SessionOfDay.Morning && r.SessionOfDay != SessionOfDay.Afternoon)
            throw ApiException.BadRequest("Buổi chỉ có Sáng hoặc Chiều.");
        if (r.PeriodNo < 1) throw ApiException.BadRequest("Số tiết phải từ 1 trở lên.");
        EnsureTimes(r.StartTime, r.EndTime);
        if (await db.PeriodSlot.AnyAsync(p => p.SessionOfDay == r.SessionOfDay && p.PeriodNo == r.PeriodNo))
            throw ApiException.BadRequest($"Khung {SessionOfDay.Label(r.SessionOfDay)} tiết {r.PeriodNo} đã có. Sửa giờ của khung đó hoặc chọn tiết khác.");
        await EnsureNoConflictAsync(r.SessionOfDay, r.PeriodNo, r.StartTime, r.EndTime, exceptId: null);

        var slot = new PeriodSlot
        {
            SessionOfDay = r.SessionOfDay, PeriodNo = r.PeriodNo, StartTime = r.StartTime, EndTime = r.EndTime, IsActive = true,
        };
        db.PeriodSlot.Add(slot);
        await db.SaveChangesAsync();
        return new PeriodSlotDto(slot.PeriodSlotId, slot.SessionOfDay, slot.PeriodNo, slot.StartTime, slot.EndTime, true);
    }

    public async Task UpdateTimesAsync(int id, PeriodSlotTimesRequest r)
    {
        var slot = await db.PeriodSlot.FindAsync(id) ?? throw ApiException.NotFound("Không tìm thấy khung giờ.");
        EnsureTimes(r.StartTime, r.EndTime);
        if (slot.IsActive) await EnsureNoConflictAsync(slot.SessionOfDay, slot.PeriodNo, r.StartTime, r.EndTime, exceptId: id);
        slot.StartTime = r.StartTime;
        slot.EndTime = r.EndTime;
        await db.SaveChangesAsync();
    }

    /// <summary>Không bỏ được khung đang có tiết học tham chiếu: mọi tiết kể cả tiết bù phải nằm đúng một khung (BR-07).</summary>
    public async Task SetActiveAsync(int id, bool isActive)
    {
        var slot = await db.PeriodSlot.FindAsync(id) ?? throw ApiException.NotFound("Không tìm thấy khung giờ.");
        if (!isActive && (await db.TeachingSession.AnyAsync(s => s.PeriodSlotId == id)
                          || await db.TimetableEntry.AnyAsync(e => e.PeriodSlotId == id)))
            throw ApiException.BadRequest("Khung giờ đang có tiết học hoặc thời khóa biểu tham chiếu nên không ngừng dùng được.");
        if (isActive) await EnsureNoConflictAsync(slot.SessionOfDay, slot.PeriodNo, slot.StartTime, slot.EndTime, exceptId: id);   // lúc đang ngừng dùng giờ có thể đã bị khung khác chiếm
        slot.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Giờ của các khung đang dùng không được chồng nhau và phải đúng thứ tự: trong một buổi, tiết nhỏ hơn kết thúc trước khi tiết lớn hơn bắt đầu;
    /// buổi Sáng kết thúc trước khi buổi Chiều bắt đầu. Kiểm xung đột giáo viên (BR-13) dựa trên khung giờ nên giờ chồng nhau sẽ làm lọt xung đột thật.
    /// </summary>
    private async Task EnsureNoConflictAsync(string session, byte periodNo, TimeOnly start, TimeOnly end, int? exceptId)
    {
        var others = await db.PeriodSlot.AsNoTracking().Where(p => p.IsActive && p.PeriodSlotId != exceptId).ToListAsync();
        var clash = others.FirstOrDefault(o => Conflicts(session, periodNo, start, end, o));
        if (clash is not null)
            throw ApiException.BadRequest(
                $"Giờ {start:HH:mm}–{end:HH:mm} chồng hoặc sai thứ tự với khung {SessionOfDay.Label(clash.SessionOfDay)} tiết {clash.PeriodNo} ({clash.StartTime:HH:mm}–{clash.EndTime:HH:mm}). " +
                "Tiết trước phải kết thúc trước khi tiết sau bắt đầu, buổi Sáng phải kết thúc trước buổi Chiều.");
    }

    private static bool Conflicts(string session, byte periodNo, TimeOnly start, TimeOnly end, PeriodSlot other)
    {
        if (other.SessionOfDay == session)
        {
            if (other.PeriodNo == periodNo) return false;   // cùng khóa (Buổi, Tiết) đã được kiểm riêng
            return other.PeriodNo < periodNo ? other.EndTime > start : end > other.StartTime;
        }
        return session == SessionOfDay.Morning ? end > other.StartTime : other.EndTime > start;
    }

    private static void EnsureTimes(TimeOnly start, TimeOnly end)
    {
        if (end <= start) throw ApiException.BadRequest("Giờ kết thúc phải sau giờ bắt đầu.");
    }
}
