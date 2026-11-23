using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Text;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;

namespace MSMS.Api.Modules.Adm.Ai;

/// <summary>Một người trong phạm vi: mã giả gửi AI, tên và mô tả chỉ dùng trong hệ thống.</summary>
internal sealed record DirectoryPerson(string Code, string Name, string PersonCode, string Detail, bool IsStudent);

/// <summary>
/// Danh bạ học sinh (đang ghi danh ở lớp trong phạm vi) và giáo viên (theo phân hiệu chính) của người hỏi MSMS AI.
/// Đổi tên, mã thật sang mã giả trước khi gửi AI và đổi ngược khi trả lời; mã ngoài phạm vi giữ nguyên, không lộ tên.
/// </summary>
internal sealed partial class PersonDirectory
{
    private readonly Dictionary<string, DirectoryPerson> _byCode;
    private readonly Dictionary<string, DirectoryPerson> _byPersonCode;
    private readonly List<(string Name, string[] Words, List<DirectoryPerson> People)> _names;

    private PersonDirectory(List<DirectoryPerson> people)
    {
        _byCode = people.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        _byPersonCode = people.Where(p => !string.IsNullOrEmpty(p.PersonCode))
            .GroupBy(p => p.PersonCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        // Chỉ dò họ tên đầy đủ có từ hai chữ trở lên; tên dài dò trước để "Nguyễn Văn An" không bị "Văn An" cắt mất.
        _names = people.GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => (Name: g.Key, Words: Words(g.Key), People: g.ToList()))
            .Where(x => x.Words.Length >= 2)
            .OrderByDescending(x => x.Name.Length).ToList();
    }

    public IReadOnlyCollection<DirectoryPerson> People => _byCode.Values;

    public static async Task<PersonDirectory> LoadAsync(MsmsDbContext db, AiScope scope)
    {
        var classIds = scope.Classes.Select(c => c.Id).ToList();
        var classes = scope.Classes.ToDictionary(c => c.Id);
        var students = await db.Enrollment.AsNoTracking()
            .Where(EnrollmentRules.ActiveAt(scope.Today)).Where(e => classIds.Contains(e.ClassId))
            .Select(e => new { e.StudentId, e.ClassId, e.Student.Student.FullName, e.Student.Student.PersonCode })
            .ToListAsync();
        var campusIds = scope.Campuses.Select(c => c.Id).ToList();
        var departments = scope.Departments.ToDictionary(d => d.Id, d => d.Name);
        var teachers = await db.StaffProfile.AsNoTracking()
            .Where(s => s.IsActive && campusIds.Contains(s.PrimaryCampusId) && s.Staff.Account != null && s.Staff.Account.AccountType == AccountTypes.Teacher)
            .Select(s => new { s.StaffId, s.Staff.FullName, s.Staff.PersonCode, s.DepartmentId })
            .ToListAsync();
        var people = students.GroupBy(s => s.StudentId).Select(g => g.First())
            .Select(s => new DirectoryPerson(AiCodes.Student(s.StudentId), s.FullName, s.PersonCode,
                $"Học sinh {classes[s.ClassId].Name} · {classes[s.ClassId].CampusName}", true))
            .Concat(teachers.Select(t => new DirectoryPerson(AiCodes.Teacher(t.StaffId), t.FullName, t.PersonCode,
                t.DepartmentId is { } d && departments.TryGetValue(d, out var name) ? $"Giáo viên · {name}" : "Giáo viên", false)))
            .ToList();
        return new PersonDirectory(people);
    }

    /// <summary>Thay mã thật (HS000123, CB000045) và họ tên đầy đủ của người trong phạm vi bằng mã giả. Tên trùng thì ghi mọi mã để AI hỏi lại.</summary>
    public string Mask(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        text = PersonCodeToken().Replace(text, m => _byPersonCode.TryGetValue(m.Value, out var p) ? p.Code : m.Value);
        var present = Words(text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, words, people) in _names)
        {
            if (!words.All(present.Contains)) continue;
            var code = string.Join(" hoặc ", people.Select(p => p.Code));
            text = ReplaceWhole(text, name, code);
        }
        return text;
    }

    /// <summary>Đổi mã giả trong câu trả lời thành họ tên; mã không thuộc phạm vi giữ nguyên.</summary>
    public string Unmask(string text) =>
        CodeToken().Replace(text, m => _byCode.TryGetValue(m.Value, out var p) ? p.Name : m.Value);

    /// <summary>Gợi ý người cho ô chọn @: khớp tên không dấu hoặc đầu mã, tên bắt đầu bằng chuỗi gõ đứng trước.</summary>
    public List<DirectoryPerson> Search(string query, int limit)
    {
        var q = Fold(query.Trim());
        if (q.Length < 2) return [];
        return People
            .Select(p => (p, Name: Fold(p.Name)))
            .Where(x => x.Name.Contains(q, StringComparison.Ordinal) || x.p.PersonCode.StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name.StartsWith(q, StringComparison.Ordinal) || x.Name.Contains(" " + q, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(x => x.p.IsStudent ? 1 : 0).ThenBy(x => x.Name, StringComparer.Ordinal)
            .Take(limit).Select(x => x.p).ToList();
    }

    private static string ReplaceWhole(string text, string name, string replacement)
    {
        var sb = new StringBuilder();
        var i = 0;
        while (true)
        {
            var at = text.IndexOf(name, i, StringComparison.OrdinalIgnoreCase);
            if (at < 0) break;
            var end = at + name.Length;
            var whole = (at == 0 || !char.IsLetterOrDigit(text[at - 1])) && (end == text.Length || !char.IsLetterOrDigit(text[end]));
            sb.Append(text, i, at - i).Append(whole ? replacement : text.Substring(at, name.Length));
            i = end;
        }
        return sb.Append(text, i, text.Length - i).ToString();
    }

    private static string[] Words(string text) => WordToken().Matches(text).Select(m => m.Value).ToArray();

    /// <summary>Chữ thường không dấu để tìm tên khi gõ thiếu dấu.</summary>
    private static string Fold(string s) => TextNormalizer.RemoveDiacritics(s).ToLowerInvariant();

    [GeneratedRegex(@"\b(HS|GV)#\d+\b", RegexOptions.IgnoreCase)]
    private static partial Regex CodeToken();

    [GeneratedRegex(@"\b[A-Z]{2}\d{3,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex PersonCodeToken();

    [GeneratedRegex(@"[\p{L}\p{M}]+")]
    private static partial Regex WordToken();
}
