using System;

namespace MSMS.Api.Data.Entities;

/// <summary>
/// Phân quyền động: School Admin bật hoặc tắt một chức năng cho một loại tài khoản.
/// Không có dòng nghĩa là đang bật. Chỉ áp trong trần nghiệp vụ của FeatureCatalog, không cấp thêm chức năng.
/// </summary>
public partial class FeaturePermission
{
    public string AccountType { get; set; } = null!;

    public string FeatureKey { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int UpdatedBy { get; set; }

    public virtual Account UpdatedByNavigation { get; set; } = null!;
}
