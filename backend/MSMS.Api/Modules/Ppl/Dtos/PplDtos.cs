using MSMS.Api.Common.Excel;

namespace MSMS.Api.Modules.Ppl.Dtos;

// PPL-01 Danh bạ cán bộ
public record StaffDto(
    int StaffId, string PersonCode, string FullName, DateOnly? DateOfBirth, string? NationalId, string? Gender, string? PhoneNumber, string? Email,
    int PrimaryCampusId, string PrimaryCampusName, int? DepartmentId, string? DepartmentName,
    string? TeacherAbbr, string? Position, bool IsActive,
    int? AccountId, string? AccountType, string? AccountStatus);

public record StaffRequest(
    string FullName, DateOnly? DateOfBirth, string? NationalId, string? Gender, string? PhoneNumber, string? Email,
    int PrimaryCampusId, int? DepartmentId, string? TeacherAbbr, string? Position);

public record StaffImportSkipped(int Row, string FullName, string Reason, string? PersonCode = null);

public record StaffImportResult(bool Accepted, int Created, IReadOnlyList<StaffImportSkipped> Skipped,
    IReadOnlyList<ImportError> Errors, int TotalErrors);

// PPL-04 Trách nhiệm
public record ResponsibilityRow(
    string Kind, int TargetId, string TargetName, string? TargetGroup,
    int? RecordId, int? StaffId, string? StaffName, DateOnly? StartDate,
    string? NextStaffName = null, DateOnly? NextStartDate = null,
    string? StaffCode = null, DateOnly? EndDate = null, string? EndedStaffName = null, DateOnly? EndedDate = null, string? TargetCode = null);

public record ResponsibilityAssignRequest(string Kind, int TargetId, int StaffId, DateOnly StartDate);
public record ResponsibilityEndRequest(string Kind, int RecordId, DateOnly EndDate);
public record ResponsibilityHistoryRow(int RecordId, int StaffId, string StaffName, DateOnly StartDate, DateOnly? EndDate, bool IsEffective);

// Import học sinh kèm ghi danh và phụ huynh (PPL-02, 4.3.16–4.3.18)
public record StudentImportSkipped(int Row, string FullName, string Reason, string? PersonCode = null);
public record StudentImportResult(bool Accepted, int Students, int Enrollments, int Parents, IReadOnlyList<StudentImportSkipped> Skipped,
    IReadOnlyList<ImportError> Errors, int TotalErrors);

// PPL-02 Danh bạ học sinh (4.3.12–4.3.15)
public record StudentDto(
    int StudentId, string PersonCode, string FullName, DateOnly? DateOfBirth, string NationalId, string? Gender, string? PhoneNumber, string? Address,
    string Status, string? Note, int? CampusId, string? CampusName, int? ClassId, string? ClassName, string? GuardianName, string? GuardianPhone);

public record StudentRequest(string FullName, DateOnly? DateOfBirth, string? NationalId, string? Gender, string? PhoneNumber, string? Address, string? Note);

// PPL-03 Danh bạ phụ huynh (4.3.22–4.3.26)
public record GuardianDto(
    int ParentId, string PersonCode, string FullName, string? PhoneNumber, string? Email, string? Address,
    int ChildCount, string? ChildrenSummary, int? AccountId, string? AccountStatus);

public record GuardianRequest(string FullName, string? PhoneNumber, string? Email, string? Address);

public record GuardianChildDto(
    int StudentId, string PersonCode, string FullName, string? ClassName, string? CampusName, string StudentStatus, string? Relationship, bool IsPrimary);

/// <summary>Đổi phụ huynh đại diện: chọn phụ huynh có sẵn (ExistingParentId) hoặc khai mới (NewGuardian).</summary>
public record ChangeGuardianRequest(int? ExistingParentId, GuardianRequest? NewGuardian, string? Relationship);

/// <summary>Chuyển trường (SchoolTransfer) hoặc tốt nghiệp (Graduated) cho một hoặc nhiều học sinh (4.3.19–4.3.20).</summary>
public record StudentLeaveRequest(List<int> StudentIds, string Reason);
