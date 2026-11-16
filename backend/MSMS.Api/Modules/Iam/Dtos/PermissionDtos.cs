namespace MSMS.Api.Modules.Iam.Dtos;

/// <summary>Một ô của bảng phân quyền. Allowed = nằm trong trần nghiệp vụ; Locked = luôn bật, không tắt được.</summary>
public record FeatureCell(string AccountType, bool Allowed, bool Locked, bool Enabled);

public record FeatureRow(string Key, string Group, string Label, List<FeatureCell> Cells);

public record FeatureMatrix(List<string> AccountTypes, List<FeatureRow> Features, DateTime? UpdatedAt, string? UpdatedByName);

public record FeatureChange(string AccountType, string FeatureKey, bool Enabled);

public record UpdateFeaturePermissionsRequest(List<FeatureChange>? Changes);

/// <summary>Việc đang dở của một chức năng sắp tắt (chỉ để cảnh báo trước khi lưu).</summary>
public record FeatureImpact(string AccountType, string FeatureKey, string Label, string Message);
