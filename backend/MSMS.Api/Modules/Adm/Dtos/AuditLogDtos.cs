namespace MSMS.Api.Modules.Adm.Dtos;

/// <summary>Một dòng nhật ký trong danh sách (ADM-02, task 4.9.6).</summary>
public sealed record AuditLogItemDto(
    long AuditId, string Category, string Action, string ActorName, string ActorCode, DateTime CreatedAt, int TargetCount);

/// <summary>Người đã có bản ghi nhật ký, dùng cho bộ lọc người thực hiện (task 4.9.7).</summary>
public sealed record AuditActorDto(int PersonId, string FullName, string PersonCode);

/// <summary>Một đối tượng của bản ghi. Label trống khi đối tượng không có tên hoặc đã bị xóa: giao diện hiện theo ID.</summary>
public sealed record AuditTargetDto(long Id, string? Label);

/// <summary>Một trường trong nội dung bản ghi; ID đã được đổi thành tên khi tra được, danh sách nối bằng dấu phẩy.</summary>
public sealed record AuditFieldDto(string Key, string Value);

/// <summary>Nội dung đầy đủ của một bản ghi, gồm danh sách ID của thao tác hàng loạt (task 4.9.8).</summary>
public sealed record AuditLogDetailDto(
    long AuditId, string Category, string Action, string ActorName, string ActorCode, DateTime CreatedAt,
    string TargetKind, List<AuditTargetDto> Targets, List<AuditFieldDto> Fields);
