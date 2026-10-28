namespace MSMS.Api.Modules.Par.Dtos;

public record CreateConversationDto(
    int StudentId,
    int? StaffId,
    int? ParentId
);

public record SendMessageDto(
    string Content
);

public record AvailableTeacherDto(
    int StaffId,
    string FullName,
    string? TeacherAbbr,
    string RoleDescription
);

public record AvailableParentDto(
    int ParentId,
    string FullName,
    string? Relationship,
    string? Phone
);

public record TeachingStudentDto(
    int StudentId,
    string FullName,
    string StudentCode,
    int ClassId,
    string ClassName
);

public record ConversationItemDto(
    int ConversationId,
    int StudentId,
    string StudentName,
    string StudentCode,
    string ClassName,
    int StaffId,
    string StaffName,
    string? StaffAbbr,
    string RoleDescription,
    string? LastMessageContent,
    DateTime? LastMessageAt,
    int UnreadCount,
    bool IsReadOnly
);

public record MessageItemDto(
    long MessageId,
    int ConversationId,
    int SenderPersonId,
    string SenderName,
    bool IsMine,
    string Content,
    DateTime SentAt,
    DateTime? ReadAt
);
