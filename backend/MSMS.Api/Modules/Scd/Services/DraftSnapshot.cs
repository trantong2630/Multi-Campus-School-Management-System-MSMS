using System.Text.Json;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Modules.Scd.Services;

/// <summary>
/// Nội dung file import lúc lưu thành bản Nháp. Dùng để đánh dấu ô đã sửa và khôi phục bản Nháp về file import (BR-12).
/// </summary>
public static class DraftSnapshot
{
    public sealed record Item(int ClassId, int DayOfWeek, int PeriodSlotId, int SubjectId, int TeacherId);

    public static string Serialize(IEnumerable<TimetableEntry> entries) =>
        JsonSerializer.Serialize(entries.Select(e => new Item(e.ClassId, e.DayOfWeek, e.PeriodSlotId, e.SubjectId, e.TeacherId)));

    public static List<Item>? Parse(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<Item>>(json);
}
