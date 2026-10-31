using Microsoft.AspNetCore.Mvc;

namespace MSMS.Api.Common.Errors;

/// <summary>
/// Lỗi do ASP.NET phát hiện trước khi vào code của dự án (thiếu trường bắt buộc, JSON hỏng, sai kiểu dữ liệu) cũng trả đúng định dạng
/// <c>{ code, message }</c> bằng tiếng Việt như mọi lỗi khác (Conventions §3). Chi tiết từng trường (tiếng Anh) đi kèm trong <c>errors</c> để dò lỗi.
/// </summary>
public static class ModelStateErrorResponse
{
    public const string Message = "Dữ liệu gửi lên chưa đầy đủ hoặc sai định dạng. Kiểm tra lại các ô đã nhập rồi gửi lại.";

    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(e => e.Value is { Errors.Count: > 0 })
            .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());
        return new BadRequestObjectResult(new { code = ErrorCodes.ValidationFailed, message = Message, errors });
    }
}
