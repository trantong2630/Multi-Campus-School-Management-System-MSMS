namespace MSMS.Api.Modules.Com.Dtos;

/// <summary>
/// Chi tiết một thông báo cho người dùng (BR-26).
/// </summary>
public record NotificationItemDto(
    long NotificationId,
    int RecipientPersonId,
    string NotificationType,
    string Title,
    string? Body,
    string? RefEntityType,
    long? RefEntityId,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt
);

/// <summary>
/// Bộ lọc và phân trang danh sách thông báo.
/// </summary>
public record NotificationQueryDto(
    bool? IsRead = null,
    int Page = 1,
    int PageSize = 20
);

/// <summary>
/// Kết quả danh sách thông báo kèm tổng số chưa đọc (BR-26).
/// </summary>
public record NotificationListResultDto(
    IReadOnlyList<NotificationItemDto> Items,
    int Total,
    int UnreadCount
);

/// <summary>
/// Kết quả khi người dùng mở thông báo và kiểm tra lại phân quyền tại thời điểm mở (BR-26: Re-check on open).
/// </summary>
public record OpenNotificationResultDto(
    long NotificationId,
    bool IsRead,
    bool CanAccess,
    string? TargetRoute,
    string? RefEntityType,
    long? RefEntityId,
    string? ReasonNotAllowed = null
);

/// <summary>
/// DTO đếm số thông báo chưa đọc.
/// </summary>
public record UnreadCountDto(int UnreadCount);
