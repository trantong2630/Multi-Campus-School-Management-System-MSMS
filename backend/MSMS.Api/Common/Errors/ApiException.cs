namespace MSMS.Api.Common.Errors;

/// <summary>Lỗi nghiệp vụ: mã lỗi cho FE xử lý riêng, message tiếng Việt để hiển thị.</summary>
public class ApiException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;

    public static ApiException BadRequest(string message, string code = ErrorCodes.ValidationFailed) => new(400, code, message);
    public static ApiException Unauthorized(string message, string code = ErrorCodes.Unauthorized) => new(401, code, message);
    public static ApiException Forbidden(string message, string code = ErrorCodes.Forbidden) => new(403, code, message);
    public static ApiException NotFound(string message) => new(404, ErrorCodes.NotFound, message);
    public static ApiException Conflict(string message, string code = ErrorCodes.Conflict) => new(409, code, message);
}
