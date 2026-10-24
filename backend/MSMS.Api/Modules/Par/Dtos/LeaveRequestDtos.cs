namespace MSMS.Api.Modules.Par.Dtos;

public record CreateLeaveRequestDto(int StudentId, DateOnly FromDate, DateOnly ToDate, string SessionOfDay, string? Reason);

public record ParentLeaveItemDto(
    int LeaveRequestId,
    int StudentId,
    string StudentName,
    int ClassId,
    string ClassName,
    DateOnly FromDate,
    DateOnly ToDate,
    string SessionOfDay,
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
    string? Reason,
    string Status,
    DateTime CreatedAt,
    int? DecidedByPersonId,
    string? DecidedByPersonName,
    DateTime? DecidedAt,
    string? DecisionNote);

