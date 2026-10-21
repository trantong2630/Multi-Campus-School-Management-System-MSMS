namespace MSMS.Api.Common.Errors;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            await Write(context, ex.StatusCode, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi không xử lý: {Path}", context.Request.Path);
            await Write(context, 500, ErrorCodes.Internal, "Hệ thống gặp lỗi. Thử lại sau, nếu vẫn lỗi thì liên hệ quản trị.");
        }
    }

    public static Task Write(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { code, message });
    }
}
