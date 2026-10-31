namespace MSMS.Api.Modules.Sch.Dtos;

public record ActiveRequest(bool IsActive);

// SCH-01 Phân hiệu
public record CampusDto(int CampusId, string Code, string Name, string? Address, string? PhoneNumber, bool IsActive, int ActiveClassCount);
public record CampusRequest(string Code, string Name, string? Address, string? PhoneNumber);

// SCH-02 Tổ chuyên môn & Môn học
public record DepartmentDto(int DepartmentId, string Name, string? Description, bool IsActive, int SubjectCount);
public record DepartmentRequest(string Name, string? Description);
public record SubjectDto(int SubjectId, int DepartmentId, string DepartmentName, string Name, string Abbreviation, bool IsActive);
public record SubjectRequest(int DepartmentId, string Name, string Abbreviation);

// SCH-03 Năm học, Học kỳ, Ngày nghỉ
public record SemesterDto(int SemesterId, byte SemesterNo, string Name, DateOnly StartDate, DateOnly EndDate);
public record AcademicYearDto(int AcademicYearId, string Name, DateOnly StartDate, DateOnly EndDate, bool IsCurrent, IReadOnlyList<SemesterDto> Semesters,
    DateOnly Semester1LatestEnd, DateOnly Semester2LatestEnd);
public record AcademicYearRequest(string Name, DateOnly StartDate, DateOnly EndDate);
public record SemesterRange(DateOnly StartDate, DateOnly EndDate);
public record SemestersRequest(SemesterRange Semester1, SemesterRange Semester2);
public record HolidayDto(int HolidayId, int AcademicYearId, int? CampusId, string? CampusName, DateOnly HolidayDate, string? Reason, bool IsAdHoc);
public record HolidayRequest(int AcademicYearId, int? CampusId, DateOnly HolidayDate, string? Reason);

// SCH-04 Khung giờ tiết
public record PeriodSlotDto(int PeriodSlotId, string SessionOfDay, byte PeriodNo, TimeOnly StartTime, TimeOnly EndTime, bool IsActive);
public record PeriodSlotRequest(string SessionOfDay, byte PeriodNo, TimeOnly StartTime, TimeOnly EndTime);
public record PeriodSlotTimesRequest(TimeOnly StartTime, TimeOnly EndTime);

// SCH-05 Lớp
public record ClassDto(int ClassId, int CampusId, string CampusName, int AcademicYearId, string AcademicYearName,
    string Name, byte GradeLevel, bool IsActive, int StudentCount, string? HomeroomName);
public record ClassRequest(int CampusId, int AcademicYearId, string Name, byte GradeLevel);
public record ClassUpdateRequest(string Name, byte GradeLevel);

// SCH-07 Thông tin trường
public record SchoolProfileDto(string? Name, string? Address, string? PhoneNumber, string? Email, string? Website, bool HasLogo, long? LogoVersion);
public record SchoolProfileRequest(string Name, string? Address, string? PhoneNumber, string? Email, string? Website);

// SCH-05 Ghi danh
public record RosterItemDto(int EnrollmentId, int StudentId, string PersonCode, string FullName, DateOnly? DateOfBirth,
    string? Gender, DateOnly StartDate, DateOnly? EndDate, string? Reason);
public record RosterDto(int ClassId, string ClassName, DateOnly Date, IReadOnlyList<RosterItemDto> Students);
public record StudentCandidateDto(int StudentId, string PersonCode, string FullName, DateOnly? DateOfBirth);
public record EnrollRequest(int StudentId, DateOnly StartDate);
public record TransferRequest(int ToClassId, DateOnly EffectiveDate);
public record CloseEnrollmentRequest(string Reason);

// SCH-06 Kết chuyển năm học
public record RolloverTargetDto(int ClassId, string Name, byte GradeLevel, bool IsActive);
public record RolloverRowDto(int SourceClassId, string SourceName, int CampusId, string CampusName, byte GradeLevel,
    int StudentCount, int AlreadyEnrolled, string SuggestedName, byte? SuggestedGrade, RolloverTargetDto? Target);
public record RolloverClassItem(int SourceClassId, string Name, byte GradeLevel);
public record RolloverCreateRequest(int FromYearId, int ToYearId, List<RolloverClassItem> Items);
public record RolloverCreateResult(int Created, IReadOnlyList<ClassDto> Classes);
public record RolloverMovePair(int SourceClassId, int TargetClassId);
public record RolloverMoveRequest(List<RolloverMovePair> Pairs);
public record RolloverMoveItemResult(int SourceClassId, int TargetClassId, int Moved, int Skipped);
public record RolloverMoveResult(int Moved, int Skipped, IReadOnlyList<RolloverMoveItemResult> Items);
