namespace MSMS.Api.Modules.Adm.Dtos;

/// <summary>Số liệu của một phân hiệu; hàng của bảng so sánh khi Principal xem toàn trường.</summary>
public sealed record DashboardCampusRow(
    int CampusId, string Name, int ClassCount, int StudentCount, int StaffCount, int SessionsToday);

public sealed record GradeCount(int GradeLevel, int ClassCount, int StudentCount);

/// <summary>ADM-01: số liệu Dashboard theo phạm vi. CampusId trống = toàn trường (chỉ Principal).</summary>
public sealed record DashboardDto(
    int? CampusId, string? AcademicYearName,
    int ClassCount, int StudentCount, int StaffCount, int SessionsToday,
    List<GradeCount> ByGrade, List<DashboardCampusRow> Campuses);
