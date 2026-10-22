using MSMS.Api.Common.Excel;

namespace MSMS.Api.Modules.Scd.Dtos;

public record CampusOptionDto(int CampusId, string Name);

// ---- Phiên bản & import ----
public record TimetableVersionDto(
    int VersionId, int CampusId, string CampusName, int SemesterId, string SemesterName, string AcademicYearName,
    string Status, string? SourceFileName, DateTime ImportedAt, string? ImportedByName, DateTime? PublishedAt,
    int? DeletedSessionCount, int? CreatedSessionCount, int EntryCount);

public record TimetableImportResult(bool Accepted, int? VersionId, int EntryCount, IReadOnlyList<ImportError> Errors, int TotalErrors);

public record GridClassDto(int ClassId, string Name);
public record GridCellDto(string SubjectAbbr, string SubjectName, string TeacherAbbr, string TeacherName);
public record GridRowDto(int DayOfWeek, string SessionOfDay, byte PeriodNo, IReadOnlyList<GridCellDto?> Cells);
public record VersionGridDto(int VersionId, string Status, string CampusName, string SemesterName,
    IReadOnlyList<GridClassDto> Classes, IReadOnlyList<GridRowDto> Rows);

// ---- Xem trước & phát hành ----
public record PreviewClassRow(string ClassName, int DeleteCount, int CreateCount);
public record PublishWarnings(int ManualEditedCount, int MakeUpCount, int PendingCount)
{
    public bool NeedsConfirmation => ManualEditedCount + MakeUpCount + PendingCount > 0;
}
public record TeacherConflictDto(
    string TeacherName, string TeacherAbbr, int DayOfWeek, string SessionOfDay, byte PeriodNo, string NewClassName,
    string OccupiedCampusName, string OccupiedClassName, string OccupiedSubject, string? OccupiedViceName,
    int SessionCount, DateOnly FirstDate, DateOnly LastDate);

public record PublishPreviewDto(
    int VersionId, string CampusName, string SemesterName, DateOnly FromDate, DateOnly ToDate,
    int DeleteCount, int CreateCount, IReadOnlyList<PreviewClassRow> ByClass, PublishWarnings Warnings,
    IReadOnlyList<TeacherConflictDto> Conflicts, IReadOnlyList<string> BlockingIssues, bool CanPublish);

public record PublishRequest(bool ConfirmWarnings);
public record PublishResultDto(int VersionId, int DeletedCount, int CreatedCount, int NotifiedTeachers);

// ---- Lịch dạy ----
public record SessionCellDto(long SessionId, string SubjectName, string SubjectAbbr, string? TeacherName, string? TeacherAbbr, string Status, string Source);
public record DaySlotRow(int PeriodSlotId, string SessionOfDay, byte PeriodNo, TimeOnly StartTime, TimeOnly EndTime, IReadOnlyList<SessionCellDto?> Cells);
public record DayScheduleDto(DateOnly Date, string? HolidayReason, bool IsHoliday, IReadOnlyList<GridClassDto> Classes, IReadOnlyList<DaySlotRow> Rows);
public record MySessionDto(long SessionId, DateOnly Date, string SessionOfDay, byte PeriodNo, TimeOnly StartTime, TimeOnly EndTime,
    string ClassName, string CampusName, string SubjectName, string Status, string Source);
