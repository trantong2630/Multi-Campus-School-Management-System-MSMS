using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Audit;
using MSMS.Api.Common.Errors;
using MSMS.Api.Data;
using MSMS.Api.Modules.Adm.Dtos;

namespace MSMS.Api.Modules.Adm.Services;

/// <summary>Tra cứu nhật ký (ADM-02, BR-28). Chỉ đọc: nhật ký chỉ ghi thêm nên không có hàm sửa hay xóa.</summary>
public sealed class AuditLogService(MsmsDbContext db)
{
    private static class TargetKinds
    {
        public const string Account = "Account";
        public const string TimetableVersion = "TimetableVersion";
        public const string TeachingSession = "TeachingSession";
        public const string TeacherAbsence = "TeacherAbsence";
    }

    private enum NameKind { Person, Class, Subject, Campus, Semester, PeriodSlot }

    /// <summary>Các khóa trong Detail mang ID tra được ra tên. Khóa khác giữ nguyên giá trị.</summary>
    private static readonly Dictionary<string, NameKind> NamedKeys = new()
    {
        ["teacherId"] = NameKind.Person,
        ["fromTeacherId"] = NameKind.Person,
        ["toTeacherId"] = NameKind.Person,
        ["classId"] = NameKind.Class,
        ["subjectId"] = NameKind.Subject,
        ["campusId"] = NameKind.Campus,
        ["semesterId"] = NameKind.Semester,
        ["periodSlotId"] = NameKind.PeriodSlot,
        ["slotIds"] = NameKind.PeriodSlot,
    };

    public async Task<PagedResult<AuditLogItemDto>> ListAsync(string? category, int? actorPersonId, DateOnly? from, DateOnly? to, int page, int pageSize)
    {
        if (from > to) throw ApiException.BadRequest("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");

        var query = db.AuditLog.AsNoTracking();
        if (!string.IsNullOrEmpty(category)) query = query.Where(a => a.Category == category);
        if (actorPersonId is int actor) query = query.Where(a => a.ActorPersonId == actor);
        if (from is DateOnly start)
        {
            var fromTime = start.ToDateTime(TimeOnly.MinValue);
            query = query.Where(a => a.CreatedAt >= fromTime);
        }
        if (to is DateOnly end)
        {
            var beforeTime = end.AddDays(1).ToDateTime(TimeOnly.MinValue);   // tính trọn ngày kết thúc
            query = query.Where(a => a.CreatedAt < beforeTime);
        }

        var rows = await query
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AuditId)   // CreatedAt chỉ chính xác đến giây
            .Select(a => new { a.AuditId, a.Category, a.Action, a.ActorPerson.FullName, a.ActorPerson.PersonCode, a.CreatedAt, a.TargetIds })
            .ToPagedAsync(page, pageSize);

        return new PagedResult<AuditLogItemDto>(
            rows.Items.Select(a => new AuditLogItemDto(a.AuditId, a.Category, a.Action, a.FullName, a.PersonCode, a.CreatedAt, ParseIds(a.TargetIds).Count)).ToList(),
            rows.Total);
    }

    public Task<List<AuditActorDto>> ActorsAsync() =>
        db.Person.AsNoTracking().Where(p => p.AuditLog.Any()).OrderBy(p => p.FullName)
            .Select(p => new AuditActorDto(p.PersonId, p.FullName, p.PersonCode)).ToListAsync();

    public async Task<AuditLogDetailDto> GetAsync(long auditId)
    {
        var a = await db.AuditLog.AsNoTracking().Where(x => x.AuditId == auditId)
                    .Select(x => new { x.AuditId, x.Category, x.Action, x.ActorPerson.FullName, x.ActorPerson.PersonCode, x.CreatedAt, x.TargetIds, x.Detail })
                    .FirstOrDefaultAsync()
                ?? throw ApiException.NotFound("Không tìm thấy bản ghi nhật ký.");

        var kind = TargetKindOf(a.Category, a.Action);
        return new AuditLogDetailDto(a.AuditId, a.Category, a.Action, a.FullName, a.PersonCode, a.CreatedAt, kind,
            await TargetsAsync(kind, ParseIds(a.TargetIds)), await FieldsAsync(a.Detail));
    }

    private static List<long> ParseIds(string? targetIds) =>
        targetIds is null ? [] : JsonSerializer.Deserialize<List<long>>(targetIds) ?? [];

    private static string TargetKindOf(string category, string action) => category switch
    {
        AuditCategories.Account => TargetKinds.Account,
        AuditCategories.TimetablePublish => TargetKinds.TimetableVersion,
        _ => action is "DeclareTeacherAbsence" or "DeleteTeacherAbsence" ? TargetKinds.TeacherAbsence : TargetKinds.TeachingSession,
    };

    /// <summary>Chỉ tài khoản mới tra tên. Tiết và khai báo vắng có thể đã bị xóa (phát hành lại, xóa khai báo) nên hiện theo ID, kèm ngày trong nội dung bản ghi.</summary>
    private async Task<List<AuditTargetDto>> TargetsAsync(string kind, List<long> ids)
    {
        if (kind != TargetKinds.Account || ids.Count == 0) return ids.Select(id => new AuditTargetDto(id, null)).ToList();

        var accountIds = ids.Select(id => (int)id).ToList();
        var names = await db.Account.AsNoTracking().Where(x => accountIds.Contains(x.AccountId))
            .Select(x => new { x.AccountId, x.Person.FullName, x.Username })
            .ToDictionaryAsync(x => (long)x.AccountId, x => $"{x.FullName} ({x.Username})");
        return ids.Select(id => new AuditTargetDto(id, names.GetValueOrDefault(id))).ToList();
    }

    private async Task<List<AuditFieldDto>> FieldsAsync(string? detail)
    {
        if (detail is null) return [];
        using var json = JsonDocument.Parse(detail);
        var props = json.RootElement.EnumerateObject().Select(p => (p.Name, Value: p.Value.Clone())).ToList();

        var wanted = new Dictionary<NameKind, HashSet<int>>();
        foreach (var (key, value) in props)
        {
            if (!NamedKeys.TryGetValue(key, out var kind)) continue;
            var ids = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.Number).Select(v => v.GetInt32())
                : value.ValueKind == JsonValueKind.Number ? [value.GetInt32()] : Enumerable.Empty<int>();
            if (!wanted.TryGetValue(kind, out var set)) wanted[kind] = set = [];
            set.UnionWith(ids);
        }
        var names = new Dictionary<(NameKind, int), string>();
        foreach (var (kind, ids) in wanted)
            foreach (var (id, name) in await NamesAsync(kind, ids))
                names[(kind, id)] = name;

        string Show(string key, JsonElement v) => v.ValueKind switch
        {
            JsonValueKind.Number when NamedKeys.TryGetValue(key, out var kind) && v.TryGetInt32(out var id) =>
                names.GetValueOrDefault((kind, id), $"#{id}"),
            JsonValueKind.Array => string.Join(", ", v.EnumerateArray().Select(x => Show(key, x))),
            JsonValueKind.String => v.GetString() ?? "",
            _ => v.GetRawText(),
        };

        return props
            .Where(p => p.Value.ValueKind != JsonValueKind.Null && (p.Value.ValueKind != JsonValueKind.Array || p.Value.GetArrayLength() > 0))
            .Select(p => new AuditFieldDto(p.Name, Show(p.Name, p.Value)))
            .ToList();
    }

    private async Task<List<(int Id, string Name)>> NamesAsync(NameKind kind, HashSet<int> ids)
    {
        if (ids.Count == 0) return [];
        switch (kind)
        {
            case NameKind.Person:
                return (await db.Person.AsNoTracking().Where(x => ids.Contains(x.PersonId)).Select(x => new { x.PersonId, x.FullName }).ToListAsync())
                    .Select(x => (x.PersonId, x.FullName)).ToList();
            case NameKind.Class:
                return (await db.Class.AsNoTracking().Where(x => ids.Contains(x.ClassId)).Select(x => new { x.ClassId, x.Name }).ToListAsync())
                    .Select(x => (x.ClassId, x.Name)).ToList();
            case NameKind.Subject:
                return (await db.Subject.AsNoTracking().Where(x => ids.Contains(x.SubjectId)).Select(x => new { x.SubjectId, x.Name }).ToListAsync())
                    .Select(x => (x.SubjectId, x.Name)).ToList();
            case NameKind.Campus:
                return (await db.Campus.AsNoTracking().Where(x => ids.Contains(x.CampusId)).Select(x => new { x.CampusId, x.Name }).ToListAsync())
                    .Select(x => (x.CampusId, x.Name)).ToList();
            case NameKind.Semester:
                return (await db.Semester.AsNoTracking().Where(x => ids.Contains(x.SemesterId)).Select(x => new { x.SemesterId, x.Name, Year = x.AcademicYear.Name }).ToListAsync())
                    .Select(x => (x.SemesterId, $"{x.Name} ({x.Year})")).ToList();
            default:
                return (await db.PeriodSlot.AsNoTracking().Where(x => ids.Contains(x.PeriodSlotId)).Select(x => new { x.PeriodSlotId, x.PeriodNo, x.StartTime }).ToListAsync())
                    .Select(x => (x.PeriodSlotId, $"Tiết {x.PeriodNo} ({x.StartTime:HH:mm})")).ToList();
        }
    }
}
