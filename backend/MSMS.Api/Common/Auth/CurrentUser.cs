using System.Security.Claims;
using MSMS.Api.Common.Errors;

namespace MSMS.Api.Common.Auth;

public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    public const string AccountIdClaim = "aid";
    public const string PersonIdClaim = "pid";
    public const string TypeClaim = "atype";
    public const string SessionIdClaim = "sid";

    private ClaimsPrincipal Principal => accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
    public int AccountId => Read(AccountIdClaim);
    public int PersonId => Read(PersonIdClaim);
    public Guid? SessionId => Guid.TryParse(Principal.FindFirstValue(SessionIdClaim), out var v) ? v : null;
    public string AccountType => Principal.FindFirstValue(TypeClaim) ?? throw ApiException.Unauthorized("Phiên đăng nhập không hợp lệ.");
    public bool IsSchoolAdmin => AccountType == AccountTypes.SchoolAdmin;

    private int Read(string claim) =>
        int.TryParse(Principal.FindFirstValue(claim), out var v) ? v : throw ApiException.Unauthorized("Phiên đăng nhập không hợp lệ.");
}
