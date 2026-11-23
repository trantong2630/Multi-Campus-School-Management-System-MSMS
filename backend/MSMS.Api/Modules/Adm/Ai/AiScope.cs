using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MSMS.Api.Common.Auth;
using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Rules;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;
using MSMS.Api.Data.Entities;
using MSMS.Api.Modules.Adm.Services;

namespace MSMS.Api.Modules.Adm.Ai;

internal sealed record ScopeClass(int Id, string Name, int Grade, int CampusId, string CampusName, int Students);

internal sealed record ScopeNamed(int Id, string Name);

internal sealed record ScopeSubject(int Id, string Name, int DepartmentId);

/// <summary>Mã giả của người gửi cho AI: AI chỉ thấy mã, Backend đổi lại thành tên khi trả lời (danh sách trắng của MSMS AI).</summary>
internal static class AiCodes
{
    public static string Student(int id) => $"HS#{id}";
    public static string Teacher(int id) => $"GV#{id}";
}

/// <summary>Một khoảng ngày đã chốt (đầu và cuối đều tính).</summary>
internal sealed record AiPeriod(DateOnly From, DateOnly To)
{
    public int Days => To.DayNumber - From.DayNumber + 1;
    public DateTime FromTime => From.ToDateTime(TimeOnly.MinValue);
    public DateTime ToEnd => To.ToDateTime(TimeOnly.MaxValue);

    /// <summary>Kỳ liền trước, cùng độ dài.</summary>
    public AiPeriod Previous => new(From.AddDays(-Days), From.AddDays(-1));

    public string Label => $"{From:dd/MM/yyyy}–{To:dd/MM/yyyy}";

    public JsonObject ToJson() => new() { ["from"] = From.ToString("yyyy-MM-dd"), ["to"] = To.ToString("yyyy-MM-dd"), ["label"] = Label };
}

/// <summary>Bộ lọc chung của các tool. Danh sách rỗng là không lọc.</summary>
internal sealed record AiFilter(IReadOnlyList<int> CampusIds, IReadOnlyList<int> Grades, IReadOnlyList<int> ClassIds,
    IReadOnlyList<int> SubjectIds, IReadOnlyList<int> DepartmentIds, int? StudentId, int? TeacherId);

/// <summary>
/// Phạm vi của người hỏi MSMS AI: Principal toàn trường, Vice Principal các phân hiệu đang phụ trách (BR-09).
/// Mọi tool đi qua đây nên lọc ngoài phạm vi bị từ chối, không phụ thuộc lời nhắc gửi AI.
/// </summary>
internal sealed class AiScope
{
    public required DateOnly Today { get; init; }
    public required DateTime Now { get; init; }
    public required CurrentTerm Term { get; init; }
    public required bool IsPrincipal { get; init; }
    public required List<ScopeNamed> Campuses { get; init; }
    public required List<ScopeClass> Classes { get; init; }
    public required List<ScopeNamed> Departments { get; init; }
    public required List<ScopeSubject> Subjects { get; init; }

    public int? WeekNo => DashboardSupport.WeekNo(Term, Today);

    public static async Task<AiScope> LoadAsync(MsmsDbContext db, CurrentUser user, ScopeResolver scope, IClock clock)
    {
        var today = clock.Today;
        var isPrincipal = user.AccountType == AccountTypes.Principal;
        if (!isPrincipal && user.AccountType != AccountTypes.VicePrincipal)
            throw ApiException.Forbidden("Chỉ Hiệu trưởng và Hiệu phó dùng được MSMS AI.");
        var campusQuery = db.Campus.AsNoTracking().Where(c => c.IsActive);
        if (!isPrincipal)
        {
            var led = (await scope.LedCampusIdsAsync()).ToList();
            if (led.Count == 0) throw ApiException.Forbidden("Thầy cô chưa được gán phụ trách phân hiệu nào nên chưa dùng được MSMS AI.");
            campusQuery = campusQuery.Where(c => led.Contains(c.CampusId));
        }
        var campuses = await campusQuery.OrderBy(c => c.Name).Select(c => new ScopeNamed(c.CampusId, c.Name)).ToListAsync();
        var campusIds = campuses.Select(c => c.Id).ToList();
        var term = await DashboardSupport.CurrentTermAsync(db, today);

        var classes = await db.Class.AsNoTracking()
            .Where(c => c.IsActive && c.AcademicYearId == term.YearId && campusIds.Contains(c.CampusId))
            .Select(c => new ScopeClass(c.ClassId, c.Name, c.GradeLevel, c.CampusId, c.Campus.Name,
                c.Enrollment.AsQueryable().Count(EnrollmentRules.ActiveAt(today))))
            .ToListAsync();
        var vi = StringComparer.Create(new CultureInfo("vi-VN"), true);
        classes = classes.OrderBy(c => c.Grade).ThenBy(c => c.Name, vi).ToList();

        var departments = await db.SubjectDepartment.AsNoTracking().Where(d => d.IsActive)
            .OrderBy(d => d.Name).Select(d => new ScopeNamed(d.DepartmentId, d.Name)).ToListAsync();
        var subjects = await db.Subject.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.Name).Select(s => new ScopeSubject(s.SubjectId, s.Name, s.DepartmentId)).ToListAsync();

        return new AiScope
        {
            Today = today, Now = clock.Now, Term = term, IsPrincipal = isPrincipal,
            Campuses = campuses, Classes = classes, Departments = departments, Subjects = subjects,
        };
    }

    /// <summary>Phân hiệu được lọc; xin phân hiệu ngoài phạm vi thì từ chối.</summary>
    public List<int> CampusIdsFor(AiFilter f)
    {
        if (f.CampusIds.Count == 0) return Campuses.Select(c => c.Id).ToList();
        if (f.CampusIds.Any(id => Campuses.All(c => c.Id != id)))
            throw ApiException.Forbidden("Không có quyền xem dữ liệu của phân hiệu này.");
        return f.CampusIds.Distinct().ToList();
    }

    /// <summary>Lớp trong phạm vi sau khi lọc phân hiệu, khối, lớp; xin lớp ngoài phạm vi thì từ chối.</summary>
    public List<ScopeClass> ClassesFor(AiFilter f)
    {
        var campusIds = CampusIdsFor(f);
        if (f.ClassIds.Any(id => Classes.All(c => c.Id != id)))
            throw ApiException.Forbidden("Có lớp không thuộc phạm vi được xem.");
        return Classes.Where(c => campusIds.Contains(c.CampusId)
                && (f.Grades.Count == 0 || f.Grades.Contains(c.Grade))
                && (f.ClassIds.Count == 0 || f.ClassIds.Contains(c.Id)))
            .ToList();
    }

    /// <summary>Môn được lọc (gộp môn của các tổ được chọn); null là mọi môn.</summary>
    public List<int>? SubjectIdsFor(AiFilter f)
    {
        if (f.SubjectIds.Count == 0 && f.DepartmentIds.Count == 0) return null;
        if (f.SubjectIds.Any(id => Subjects.All(s => s.Id != id))) throw ApiException.BadRequest("Không tìm thấy môn được hỏi.");
        if (f.DepartmentIds.Any(id => Departments.All(d => d.Id != id))) throw ApiException.BadRequest("Không tìm thấy tổ được hỏi.");
        return Subjects.Where(s => f.SubjectIds.Contains(s.Id) || f.DepartmentIds.Contains(s.DepartmentId)).Select(s => s.Id).ToList();
    }

    /// <summary>Mô tả phạm vi của một câu hỏi để AI mở đầu câu trả lời đúng.</summary>
    public string Describe(AiFilter f, bool subjects = false)
    {
        var parts = new List<string>();
        if (f.ClassIds.Count > 0) parts.Add("Lớp " + string.Join(", ", Classes.Where(c => f.ClassIds.Contains(c.Id)).Select(c => c.Name)));
        else
        {
            if (f.CampusIds.Count > 0) parts.Add(string.Join(", ", Campuses.Where(c => f.CampusIds.Contains(c.Id)).Select(c => c.Name)));
            else parts.Add(IsPrincipal ? "Toàn trường" : string.Join(", ", Campuses.Select(c => c.Name)));
            if (f.Grades.Count > 0) parts.Add("Khối " + string.Join(", ", f.Grades.Order()));
        }
        if (subjects)
        {
            if (f.DepartmentIds.Count > 0) parts.Add("Tổ " + string.Join(", ", Departments.Where(d => f.DepartmentIds.Contains(d.Id)).Select(d => d.Name)));
            if (f.SubjectIds.Count > 0) parts.Add("Môn " + string.Join(", ", Subjects.Where(s => f.SubjectIds.Contains(s.Id)).Select(s => s.Name)));
        }
        if (f.TeacherId is { } teacher) parts.Add("Giáo viên " + AiCodes.Teacher(teacher));
        return string.Join(" · ", parts);
    }

    /// <summary>
    /// Kỳ của câu hỏi. Mặc định 4 tuần tính tới hôm nay (như màn Tình hình); ngày cuối không vượt quá hôm nay.
    /// </summary>
    public AiPeriod PeriodFor(JsonElement args)
    {
        var key = AiArgs.Str(args, "period") ?? "last_4_weeks";
        var week = DashboardSupport.WeekStart(Today);
        var month = new DateOnly(Today.Year, Today.Month, 1);
        AiPeriod period = key switch
        {
            "today" => new(Today, Today),
            "this_week" => new(week, Today),
            "last_week" => new(week.AddDays(-7), week.AddDays(-1)),
            "last_4_weeks" => new(Today.AddDays(-DashboardSupport.PeriodDays + 1), Today),
            "this_month" => new(month, Today),
            "last_month" => new(month.AddMonths(-1), month.AddDays(-1)),
            "this_semester" => new(Term.SemesterStart ?? Term.YearStart ?? Today.AddDays(-DashboardSupport.PeriodDays + 1), Today),
            "this_school_year" => new(Term.YearStart ?? Today.AddDays(-DashboardSupport.PeriodDays + 1), Today),
            "custom" => Custom(args),
            _ => throw ApiException.BadRequest($"Kỳ \"{key}\" không hợp lệ."),
        };
        if (period.From > Today)
            throw ApiException.BadRequest($"Kỳ {period.Label} chưa tới (hôm nay là {Today:dd/MM/yyyy}) nên chưa có số liệu.");
        return period.To > Today ? period with { To = Today } : period;
    }

    private static AiPeriod Custom(JsonElement args)
    {
        if (!DateOnly.TryParseExact(AiArgs.Str(args, "from"), "yyyy-MM-dd", out var from)
            || !DateOnly.TryParseExact(AiArgs.Str(args, "to"), "yyyy-MM-dd", out var to))
            throw ApiException.BadRequest("Kỳ tự chọn cần from và to dạng yyyy-MM-dd.");
        if (from > to) throw ApiException.BadRequest("Ngày bắt đầu sau ngày kết thúc.");
        return new(from, to);
    }
}

/// <summary>Đọc tham số AI gửi: chấp nhận số ở dạng số nguyên, số thực hay chuỗi.</summary>
internal static class AiArgs
{
    public static string? Str(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static bool Bool(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    public static double? Number(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var v) ? AsNumber(v) : null;

    public static int? Int(JsonElement args, string name) => Number(args, name) is { } d ? (int)Math.Round(d) : null;

    public static List<int> Ints(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var v)) return [];
        if (v.ValueKind != JsonValueKind.Array) return AsNumber(v) is { } one ? [(int)Math.Round(one)] : [];
        return v.EnumerateArray().Select(AsNumber).Where(d => d is not null).Select(d => (int)Math.Round(d!.Value)).Distinct().ToList();
    }

    /// <summary>Đọc mã người "HS#12" hoặc "GV#5"; sai tiền tố thì báo lỗi để AI sửa.</summary>
    public static int? Person(JsonElement args, string name, string prefix)
    {
        var s = Str(args, name)?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (s.StartsWith(prefix + "#", StringComparison.OrdinalIgnoreCase) && int.TryParse(s[(prefix.Length + 1)..], out var id)) return id;
        throw ApiException.BadRequest($"Mã \"{s}\" không đúng dạng {prefix}#số.");
    }

    public static AiFilter Filter(JsonElement args) => new(Ints(args, "campus_ids"), Ints(args, "grade_levels"), Ints(args, "class_ids"),
        Ints(args, "subject_ids"), Ints(args, "department_ids"), Person(args, "student", "HS"), Person(args, "teacher", "GV"));

    private static double? AsNumber(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Number => v.GetDouble(),
        JsonValueKind.String when double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
        _ => null,
    };
}
