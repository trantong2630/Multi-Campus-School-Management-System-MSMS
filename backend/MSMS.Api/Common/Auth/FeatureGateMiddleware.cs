using MSMS.Api.Common.Errors;
using MSMS.Api.Modules.Iam.Permissions;
using MSMS.Api.Modules.Iam.Services;

namespace MSMS.Api.Common.Auth;

/// <summary>
/// Phân quyền động: chặn API của chức năng mà School Admin đã tắt cho loại tài khoản của người gọi.
/// Chạy sau UseAuthorization nên chỉ thu hẹp quyền, không cấp thêm.
/// </summary>
public sealed class FeatureGateMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CurrentUser user, FeaturePermissionService permissions)
    {
        if (user.IsAuthenticated && context.Request.Path.StartsWithSegments("/api"))
        {
            var type = user.AccountType;
            var matched = FeatureCatalog.Match(context.Request.Method, context.Request.Path.Value ?? "", type).ToList();
            if (matched.Count > 0)
            {
                var disabled = await permissions.DisabledForAsync(type);
                var blocked = matched.FirstOrDefault(f => disabled.Contains(f.Key));
                if (blocked is not null)
                {
                    await ExceptionMiddleware.Write(context, StatusCodes.Status403Forbidden, ErrorCodes.FeatureDisabled,
                        $"Chức năng “{blocked.Label}” đang tạm tắt cho loại tài khoản của bạn. Liên hệ quản trị nếu cần dùng.");
                    return;
                }
            }
        }
        await next(context);
    }
}
