using MSMS.Api.Common.Excel;

namespace MSMS.Api.Modules.Ppl.Dtos;

// PPL-01 Danh bạ cán bộ
public record StaffDto(
    int StaffId, string PersonCode, string FullName, DateOnly? DateOfBirth, string? Gender, string? PhoneNumber, string? Email,
    int PrimaryCampusId, string PrimaryCampusName, int? DepartmentId, string? DepartmentName,
    string? TeacherAbbr, string? Position, bool IsActive,
    int? AccountId, string? AccountType, string? AccountStatus);

public record StaffRequest(
    string FullName, DateOnly? DateOfBirth, string? Gender, string? PhoneNumber, string? Email,
    int PrimaryCampusId, int? DepartmentId, string? TeacherAbbr, string? Position);

public record StaffImportSkipped(int Row, string FullName, string Reason);

public record StaffImportResult(bool Accepted, int Created, IReadOnlyList<StaffImportSkipped> Skipped,
    IReadOnlyList<ImportError> Errors, int TotalErrors);

// PPL-04 Trách nhiệm
public record ResponsibilityRow(
    string Kind, int TargetId, string TargetName, string? TargetGroup,
    int? RecordId, int? StaffId, string? StaffName, DateOnly? StartDate);

public record ResponsibilityAssignRequest(string Kind, int TargetId, int StaffId, DateOnly StartDate);
public record ResponsibilityEndRequest(string Kind, int RecordId, DateOnly EndDate);
public record ResponsibilityHistoryRow(int RecordId, int StaffId, string StaffName, DateOnly StartDate, DateOnly? EndDate, bool IsEffective);
