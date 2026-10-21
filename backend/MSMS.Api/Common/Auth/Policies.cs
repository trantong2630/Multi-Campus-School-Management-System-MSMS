using Microsoft.AspNetCore.Authorization;

namespace MSMS.Api.Common.Auth;

/// <summary>Tầng 1 của kiểm quyền: loại tài khoản có được gọi chức năng không. Tầng 2 (đối tượng nằm trong phạm vi) do service kiểm.</summary>
public static class Policies
{
    public const string SchoolAdmin = nameof(SchoolAdmin);
    public const string Management = nameof(Management);   // SchoolAdmin, Principal, VicePrincipal
    public const string Staff = nameof(Staff);             // mọi cán bộ
    public const string TimetableManager = nameof(TimetableManager);   // ghi thời khóa biểu: SchoolAdmin, VicePrincipal
    public const string ResponsibilityManager = nameof(ResponsibilityManager);   // gán trách nhiệm: SchoolAdmin, Principal
    public const string SchoolProfileManager = nameof(SchoolProfileManager);   // sửa thông tin trường: SchoolAdmin, Principal
    public const string CampusReader = nameof(CampusReader);           // đọc dữ liệu phân hiệu: SchoolAdmin, Principal, VicePrincipal

    public static void Register(AuthorizationOptions o)
    {
        o.AddPolicy(SchoolAdmin, p => p.RequireClaim(CurrentUser.TypeClaim, AccountTypes.SchoolAdmin));
        o.AddPolicy(Management, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.SchoolAdmin, AccountTypes.Principal, AccountTypes.VicePrincipal));
        o.AddPolicy(TimetableManager, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.SchoolAdmin, AccountTypes.VicePrincipal));
        o.AddPolicy(ResponsibilityManager, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.SchoolAdmin, AccountTypes.Principal));
        o.AddPolicy(SchoolProfileManager, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.SchoolAdmin, AccountTypes.Principal));
        o.AddPolicy(CampusReader, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.SchoolAdmin, AccountTypes.Principal, AccountTypes.VicePrincipal));
        o.AddPolicy(Staff, p => p.RequireClaim(CurrentUser.TypeClaim,
            AccountTypes.SchoolAdmin, AccountTypes.Principal, AccountTypes.VicePrincipal, AccountTypes.Teacher));
    }
}
