namespace MSMS.Api.Common.Auth;

/// <summary>Phạm vi dữ liệu tại ngày hiện tại; không thay thế policy của từng hành vi (BR-01, BR-02).</summary>
public sealed record AccountScope(
    DateOnly AsOfDate,
    bool IsSchoolWide,
    IReadOnlyList<int> LedCampusIds,
    IReadOnlyList<int> HeadDepartmentIds,
    IReadOnlyList<int> HomeroomClassIds,
    IReadOnlyList<SubjectScope> SubjectResponsibilities,
    IReadOnlyList<EnrollmentScope> Enrollments,
    IReadOnlyList<int> LinkedStudentIds);

public sealed record SubjectScope(int ClassId, int SubjectId, int SemesterId);

public sealed record EnrollmentScope(int StudentId, int EnrollmentId, int ClassId, int CampusId, int AcademicYearId);
