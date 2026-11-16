using MSMS.Api.Common.Auth;

namespace MSMS.Api.Modules.Iam.Permissions;

/// <summary>Nhóm HTTP method mà một luật chặn.</summary>
/// <summary>Modify: PUT, PATCH, DELETE (không gồm POST).</summary>
public enum FeatureMethods { All, Read, Write, Modify }

/// <summary>
/// Một luật chặn API khi chức năng bị tắt. <paramref name="Path"/> so theo từng đoạn, "*" khớp một đoạn bất kỳ;
/// mặc định khớp tiền tố, <paramref name="Exact"/> thì phải đủ số đoạn. <paramref name="OnlyFor"/> giới hạn luật ở vài loại tài khoản.
/// </summary>
public sealed record FeatureRule(FeatureMethods Methods, string Path, bool Exact = false, string[]? OnlyFor = null)
{
    private readonly string[] _segments = Path.Trim('/').Split('/');

    public bool Matches(string method, string[] requestSegments, string accountType)
    {
        if (OnlyFor is not null && !OnlyFor.Contains(accountType)) return false;
        var isRead = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);
        if (Methods == FeatureMethods.Read && !isRead) return false;
        if (Methods == FeatureMethods.Write && isRead) return false;
        if (Methods == FeatureMethods.Modify && !(HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))) return false;
        if (requestSegments.Length < _segments.Length || (Exact && requestSegments.Length != _segments.Length)) return false;
        for (var i = 0; i < _segments.Length; i++)
            if (_segments[i] != "*" && !string.Equals(_segments[i], requestSegments[i], StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}

/// <summary>
/// Một chức năng bật/tắt được. Key trùng key mục menu ở web (app/menu.tsx) để menu ẩn đúng mục.
/// <paramref name="Ceiling"/> là trần nghiệp vụ: chỉ các loại này mới có chức năng, Admin chỉ bật/tắt trong trần.
/// <paramref name="Locked"/>: luôn bật (tránh Admin tự khóa mình hoặc mất màn hình bắt buộc).
/// </summary>
public sealed record FeatureDefinition(string Key, string Group, string Label, string[] Ceiling, FeatureRule[] Rules, bool Locked = false);

/// <summary>
/// Danh mục chức năng theo menu hiện có. Luật chỉ chặn API riêng của màn hình (ghi dữ liệu, thống kê, màn hình đặc thù).
/// API đọc dữ liệu dùng chung (danh mục, lớp, khung giờ, lịch của tôi trên Trang chủ...) không bị chặn để màn hình khác không vỡ.
/// </summary>
public static class FeatureCatalog
{
    private const string SA = AccountTypes.SchoolAdmin, P = AccountTypes.Principal, VP = AccountTypes.VicePrincipal,
        T = AccountTypes.Teacher, S = AccountTypes.Student, PA = AccountTypes.Parent;

    private static readonly string[] Mgmt = [SA, P, VP];
    private static readonly string[] StaffTypes = [SA, P, VP, T];
    private static readonly string[] All = [SA, P, VP, T, S, PA];

    private static FeatureRule R(string path, bool exact = false, params string[] onlyFor) =>
        new(FeatureMethods.Read, path, exact, onlyFor.Length == 0 ? null : onlyFor);
    private static FeatureRule W(string path, bool exact = false) => new(FeatureMethods.Write, path, exact);
    private static FeatureRule A(string path, bool exact = false, params string[] onlyFor) =>
        new(FeatureMethods.All, path, exact, onlyFor.Length == 0 ? null : onlyFor);

    public static readonly IReadOnlyList<FeatureDefinition> Features =
    [
        new("home", "Tổng quan", "Trang chủ", All, [], Locked: true),

        new("dash-school", "Bảng điều khiển", "Tình hình toàn trường", [P], [A("/api/adm/dashboard", exact: true)]),
        new("dash-campus", "Bảng điều khiển", "Tình hình phân hiệu", [VP], [A("/api/adm/dashboard", exact: true)]),
        new("dash-dept", "Bảng điều khiển", "Tình hình tổ chuyên môn", [T], [A("/api/adm/dashboard/department")]),
        new("dash-class", "Bảng điều khiển", "Tình hình lớp chủ nhiệm", [T], [A("/api/adm/homeroom")]),
        new("dash-progress", "Bảng điều khiển", "Tiến độ bài tập rèn luyện", [P, VP, T], [A("/api/adm/progress")]),
        new("dash-system", "Bảng điều khiển", "Thống kê hệ thống", [SA], [A("/api/adm/dashboard/admin")]),

        new("com-manage", "Bảng tin", "Đăng tin", StaffTypes, [W("/api/com/posts")]),
        // Cán bộ đọc danh sách bài cả ở màn Đăng tin nên chỉ chặn API đọc với học sinh, phụ huynh.
        new("com-feed", "Bảng tin", "Bảng tin", All, [R("/api/com/posts", false, S, PA)]),
        new("report", "Tổng quan", "Báo cáo toàn trường", [P], [A("/api/adm/report")]),

        new("sch-school", "Cơ cấu nhà trường", "Thông tin trường", [SA, P], [W("/api/school")]),
        new("sch-campus", "Cơ cấu nhà trường", "Phân hiệu", [SA, P], [W("/api/sch/campuses"), R("/api/sch/stats/campuses")]),
        new("sch-subject", "Cơ cấu nhà trường", "Tổ chuyên môn & Môn học", [SA, P],
            [W("/api/sch/departments"), W("/api/sch/subjects"), R("/api/sch/stats/catalog")]),
        new("sch-year", "Cơ cấu nhà trường", "Năm học & Học kỳ", [SA, P], [W("/api/sch/academic-years")]),
        new("sch-holiday", "Cơ cấu nhà trường", "Ngày nghỉ", [SA], [W("/api/sch/holidays"), R("/api/sch/holidays/impact")]),
        new("sch-slot", "Cơ cấu nhà trường", "Khung giờ tiết", [SA], [W("/api/sch/period-slots")]),
        new("sch-class", "Cơ cấu nhà trường", "Lớp & Ghi danh", [SA, P],
            // /sch/classes/{id}/graduate do màn Kết chuyển năm học gọi nên không chặn.
            [W("/api/sch/classes", exact: true), new(FeatureMethods.Modify, "/api/sch/classes/*"),
             W("/api/sch/classes/*/enrollments"), A("/api/sch/enrollments"), R("/api/sch/stats/classes")]),
        new("sch-rollover", "Cơ cấu nhà trường", "Kết chuyển năm học", [SA], [A("/api/sch/rollover")]),

        new("ppl-staff", "Hồ sơ", "Cán bộ", Mgmt, [W("/api/ppl/staff"), R("/api/ppl/staff/import-template")]),
        // Nhập danh sách học sinh (import, import-template) còn có ở màn Lớp & Ghi danh nên không chặn.
        new("ppl-student", "Hồ sơ", "Học sinh", Mgmt,
            [W("/api/ppl/students", exact: true), new(FeatureMethods.Modify, "/api/ppl/students/*"), W("/api/ppl/students/leave")]),
        new("ppl-parent", "Hồ sơ", "Phụ huynh", Mgmt, [W("/api/ppl/guardians"), R("/api/ppl/guardians/import-template")]),
        new("ppl-resp", "Hồ sơ", "Trách nhiệm", [SA, P],
            [A("/api/ppl/responsibilities/assign"), A("/api/ppl/responsibilities/end"),
             A("/api/ppl/responsibilities/export"), A("/api/ppl/responsibilities/history")]),

        new("scd-timetable", "Thời khóa biểu", "Thời khóa biểu", [VP],
            // draft-options dùng chung với Lịch dạy (điều chỉnh tiết) và Phụ trách chuyên môn nên không chặn.
            [W("/api/scd/timetable"), R("/api/scd/timetable/template"), R("/api/scd/timetable/versions/*/preview")]),
        new("scd-responsibility", "Thời khóa biểu", "Phụ trách chuyên môn", [VP], [R("/api/scd/responsibilities", exact: true)]),
        // Không chặn: /scd/schedule/mine (Trang chủ gọi), /scd/schedule/classes (Giáo viên vắng gọi).
        new("scd-schedule", "Thời khóa biểu", "Lịch dạy", [P, VP, T],
            [R("/api/scd/schedule/day"), R("/api/scd/schedule/teachers"),
             R("/api/scd/schedule/class", exact: true), R("/api/scd/schedule/teacher", exact: true), A("/api/scd/adjust")]),
        new("scd-my-class", "Thời khóa biểu", "Lịch học", [S, PA], [R("/api/scd/schedule/student"), R("/api/scd/schedule/child")]),
        // Gợi ý và xử lý tiết Chờ xử lý (pending/*/suggestions, pending/*/resolve) còn được gọi từ Lịch dạy nên không chặn.
        new("scd-absence", "Thời khóa biểu", "Giáo viên vắng", [VP],
            [A("/api/scd/absences", exact: true), new(FeatureMethods.Modify, "/api/scd/absences/*"),
             A("/api/scd/absences/teachers"), R("/api/scd/absences/pending", exact: true)]),

        // /att/classes (danh sách lớp) dùng chung với Xuất dữ liệu nên không chặn.
        new("att", "Điểm danh", "Điểm danh", [SA, P, VP, T, S, PA],
            [A("/api/att/today"), A("/api/att/sessions"), A("/api/att/class-summary"), A("/api/att/me"), A("/api/att/child")]),

        // GET /lrn/questions (danh sách câu) dùng chung với Soạn đề nên chỉ chặn tạo, sửa, xóa, AI soạn.
        new("lrn-bank", "Bài tập rèn luyện", "Kho câu hỏi", [T],
            [W("/api/lrn/questions", exact: true), new(FeatureMethods.Modify, "/api/lrn/questions/*"),
             A("/api/lrn/questions/ai-drafts")]),
        new("lrn-assign", "Bài tập rèn luyện", "Soạn đề", [T],
            [W("/api/lrn/assignments", exact: true), A("/api/lrn/papers"), A("/api/lrn/questions/matrix"), A("/api/lrn/questions/pick")]),
        new("lrn-assigned", "Bài tập rèn luyện", "Bài tập đã giao", [T],
            // Chi tiết, kết quả và đề của một bài dùng chung với Tình hình tổ chuyên môn nên chỉ chặn danh sách bài đã giao.
            [R("/api/lrn/assignments", exact: true)]),
        new("lrn-todo", "Bài tập rèn luyện", "Bài tập được giao", [S], [W("/api/lrn/my-assignments")]),
        new("lrn-done", "Bài tập rèn luyện", "Danh sách bài tập", [S], [R("/api/lrn/results", false, S)]),
        new("lrn-children", "Bài tập rèn luyện", "Kết quả của con", [PA], [R("/api/lrn/results", false, PA)]),

        // /par/leave-requests/my-children (danh sách con) là ô "Chọn con" của Lịch học, Điểm danh, Sổ liên lạc nên không chặn.
        new("par-leave", "Phụ huynh", "Đơn xin nghỉ", [T, PA],
            [A("/api/par/leave-requests", exact: true), A("/api/par/leave-requests/bulk"), A("/api/par/leave-requests/student"),
             A("/api/par/leave-requests/homeroom"), new(FeatureMethods.Modify, "/api/par/leave-requests/*")]),
        new("par-report", "Phụ huynh", "Sổ liên lạc", [T, S, PA], [A("/api/par/weekly-reports")]),
        // Số tin chưa đọc vẫn trả để header không báo lỗi; mở và gửi tin bị chặn.
        new("par-inbox", "Phụ huynh", "Hộp thư", [T, PA],
            [A("/api/par/conversations", exact: true), A("/api/par/conversations/*/messages"),
             A("/api/par/conversations/available-teachers"), A("/api/par/conversations/available-parents"),
             A("/api/par/conversations/my-teaching-students")]),

        new("adm-account", "Quản trị", "Tài khoản", [SA], [], Locked: true),
        new("adm-permission", "Quản trị", "Phân quyền", [SA], [], Locked: true),
        new("adm-audit", "Quản trị", "Nhật ký", [SA], [A("/api/adm/audit-log")]),
        new("adm-export", "Quản trị", "Xuất dữ liệu", [P, VP, T], [A("/api/adm/exports")]),
    ];

    private static readonly Dictionary<string, FeatureDefinition> ByKey = Features.ToDictionary(f => f.Key);

    public static FeatureDefinition? Find(string key) => ByKey.GetValueOrDefault(key);

    /// <summary>Chức năng có thể tắt với loại tài khoản này: nằm trong trần và không bị khóa.</summary>
    public static bool IsToggleable(FeatureDefinition f, string accountType) => !f.Locked && f.Ceiling.Contains(accountType);

    /// <summary>Các chức năng mà request này thuộc về, với loại tài khoản của người gọi.</summary>
    public static IEnumerable<FeatureDefinition> Match(string method, string path, string accountType)
    {
        var segments = path.Trim('/').Split('/');
        return Features.Where(f => IsToggleable(f, accountType) && f.Rules.Any(r => r.Matches(method, segments, accountType)));
    }
}
