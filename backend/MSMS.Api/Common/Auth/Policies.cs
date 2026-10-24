using Microsoft.AspNetCore.Authorization;

namespace MSMS.Api.Common.Auth;

/// <summary>Tầng 1 của kiểm quyền: loại tài khoản có được gọi chức năng không. Tầng 2 (đối tượng nằm trong phạm vi) do service kiểm.</summary>
public static class Policies
{
    public const string SchoolAdmin = nameof(SchoolAdmin);
    public const string Management = nameof(Management);   // SchoolAdmin, Principal, VicePrincipal
    public const string Staff = nameof(Staff);             // mọi cán bộ
    public const string TimetableManager = nameof(TimetableManager);   // ghi thời khóa biểu: VicePrincipal (SchoolAdmin không làm thời khóa biểu)
    public const string TimetableReader = nameof(TimetableReader);     // đọc thời khóa biểu và lịch dạy theo phân hiệu: Principal, VicePrincipal
    public const string ResponsibilityManager = nameof(ResponsibilityManager);   // gán trách nhiệm: SchoolAdmin, Principal
    public const string SchoolProfileManager = nameof(SchoolProfileManager);   // sửa thông tin trường: SchoolAdmin, Principal
    public const string DashboardReader = nameof(DashboardReader);     // Dashboard: Principal (toàn trường hoặc lọc phân hiệu), VicePrincipal (phân hiệu mình)
    public const string CampusReader = nameof(CampusReader);           // đọc dữ liệu phân hiệu: SchoolAdmin, Principal, VicePrincipal
    public const string Teacher = nameof(Teacher);
    public const string Parent = nameof(Parent);


    public static void Register(AuthorizationOptions o)
    {
        o.AddPolicy(SchoolAdmin, p => p.RequireClaim(CurrentUser.TypeClaim, AccountTypes.SchoolAdmin));
        o.AddPolicy(Management, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.SchoolAdmin, AccountTypes.Principal, AccountTypes.VicePrincipal));
        o.AddPolicy(TimetableManager, p => p.RequireClaim(CurrentUser.TypeClaim, AccountTypes.VicePrincipal));
        o.AddPolicy(TimetableReader, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.Principal, AccountTypes.VicePrincipal));
        o.AddPolicy(ResponsibilityManager, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.SchoolAdmin, AccountTypes.Principal));
        o.AddPolicy(SchoolProfileManager, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.SchoolAdmin, AccountTypes.Principal));
        o.AddPolicy(CampusReader, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.SchoolAdmin, AccountTypes.Principal, AccountTypes.VicePrincipal));
        o.AddPolicy(DashboardReader, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.Principal, AccountTypes.VicePrincipal));
        o.AddPolicy(Staff, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.SchoolAdmin, AccountTypes.Principal, AccountTypes.VicePrincipal, AccountTypes.Teacher));
        o.AddPolicy(Teacher, p => p.RequireClaim(CurrentUser.TypeClaim, AccountTypes.Teacher));
        o.AddPolicy(Parent, p => p.RequireClaim(CurrentUser.TypeClaim, AccountTypes.Parent));

    }
}
