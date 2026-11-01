namespace MSMS.Api.Modules.Par.Dtos;

public record CreateLeaveRequestDto(int StudentId, DateOnly FromDate, DateOnly ToDate, string SessionOfDay, IReadOnlyList<int>? SlotIds, string? Reason);

public record LeaveDayItemDto(DateOnly Date, string SessionOfDay, IReadOnlyList<int>? SlotIds);

public record BulkCreateLeaveRequestDto(int StudentId, string? Reason, List<LeaveDayItemDto> Days);

public record ParentLeaveItemDto(
    int LeaveRequestId,
    int StudentId,
    string StudentName,
    int ClassId,
    string ClassName,
    DateOnly FromDate,
    DateOnly ToDate,
    string SessionOfDay,
    string? SlotIds,
    string? Reason,
    string Status,
    DateTime CreatedAt,
    int? DecidedByPersonId,
    string? DecidedByPersonName,
    DateTime? DecidedAt,
    string? DecisionNote);

public record DecideLeaveRequestDto(string? DecisionNote);

public record HomeroomLeaveItemDto(
    int LeaveRequestId,
    int StudentId,
    string StudentName,
    string StudentCode,
    int ClassId,
    string ClassName,
    int ParentId,
    string ParentName,
    DateOnly FromDate,
    DateOnly ToDate,
    string SessionOfDay,
    string? SlotIds,
    string? Reason,
    string Status,
    DateTime CreatedAt,
    int? DecidedByPersonId,
    string? DecidedByPersonName,
    DateTime? DecidedAt,
    string? DecisionNote);

public record MyChildDto(int StudentId, string StudentCode, string FullName, string? ClassName, int? ClassId);
