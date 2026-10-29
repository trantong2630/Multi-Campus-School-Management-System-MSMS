using MSMS.Api.Common.Errors;
using MSMS.Api.Common.Time;
using MSMS.Api.Data;

namespace MSMS.Api.Common.Auth;

/// <summary>
/// Tầng 2 của kiểm quyền (BR-01): đối tượng cụ thể có nằm trong phạm vi của người gọi không.
/// Phạm vi luôn tính tại ngày hiện tại theo bản đang hiệu lực, không theo bản mới nhất (BR-09).
/// </summary>
public sealed class ScopeResolver(MsmsDbContext db, CurrentUser user, IClock clock)
{
    private IReadOnlyList<int>? _ledCampuses;

    /// <summary>Các phân hiệu mà Vice Principal đang phụ trách tại hôm nay.</summary>
    public async Task<IReadOnlyList<int>> LedCampusIdsAsync()
    {
        if (_ledCampuses is not null) return _ledCampuses;
        _ledCampuses = (await new AccountScopeResolver(db, clock).ResolveAsync(user.PersonId, user.AccountType)).LedCampusIds;
        return _ledCampuses;
    }

    /// <summary>Có đọc được lịch dạy và thời khóa biểu của phân hiệu này không. School Admin không làm thời khóa biểu.</summary>
    public async Task<bool> CanReadCampusAsync(int campusId) => user.AccountType switch
    {
        AccountTypes.Principal => true,
        AccountTypes.VicePrincipal => (await LedCampusIdsAsync()).Contains(campusId),
        _ => false,
    };

    /// <summary>Có ghi được (thời khóa biểu, điều chỉnh lịch) cho phân hiệu này không. Chỉ hiệu phó phụ trách phân hiệu; Principal chỉ đọc.</summary>
    public async Task<bool> CanManageCampusAsync(int campusId) => user.AccountType switch
    {
        AccountTypes.VicePrincipal => (await LedCampusIdsAsync()).Contains(campusId),
        _ => false,
    };

    public async Task EnsureCanManageCampusAsync(int campusId)
    {
        if (!await CanManageCampusAsync(campusId))
            throw ApiException.Forbidden("Chỉ hiệu phó đang phụ trách phân hiệu này mới thao tác được. Liên hệ hiệu trưởng để được gán phụ trách phân hiệu.");
    }

    public async Task EnsureCanReadCampusAsync(int campusId)
    {
        if (!await CanReadCampusAsync(campusId))
            throw ApiException.Forbidden("Không có quyền xem dữ liệu của phân hiệu này.");
    }
}
