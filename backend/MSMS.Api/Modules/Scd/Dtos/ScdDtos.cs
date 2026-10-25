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
/// <summary>InConflict: ô trùng giáo viên (BR-13). Edited: ô khác với nội dung file import. Cả hai chỉ có nghĩa ở bản Nháp.</summary>
public record GridCellDto(int SubjectId, string SubjectAbbr, string SubjectName, int TeacherId, string TeacherAbbr, string TeacherName,
    bool InConflict, bool Edited);
public record GridRowDto(int DayOfWeek, int PeriodSlotId, string SessionOfDay, byte PeriodNo, IReadOnlyList<GridCellDto?> Cells);
public record VersionGridDto(int VersionId, string Status, string CampusName, string SemesterName,
    IReadOnlyList<GridClassDto> Classes, IReadOnlyList<GridRowDto> Rows, IReadOnlyList<TeacherConflictDto> Conflicts, bool CanReset);

// ---- Sửa bản Nháp ----
public record SubjectOptionDto(int SubjectId, string Abbr, string Name);
public record DraftOptionsDto(IReadOnlyList<SubjectOptionDto> Subjects, IReadOnlyList<TeacherOptionDto> Teachers);
public record MoveEntryRequest(int ClassId, int FromDayOfWeek, int FromPeriodSlotId, int ToDayOfWeek, int ToPeriodSlotId);
public record AddEntryRequest(int ClassId, int DayOfWeek, int PeriodSlotId, int SubjectId, int TeacherId);
public record ChangeTeacherRequest(int ClassId, int DayOfWeek, int PeriodSlotId, int TeacherId);

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
    IReadOnlyList<TeacherConflictDto> Conflicts, IReadOnlyList<string> BlockingIssues, IReadOnlyList<string> ClassesWithoutLessons,
    bool CanPublish);

public record PublishRequest(bool ConfirmWarnings);
public record PublishResultDto(int VersionId, int DeletedCount, int CreatedCount, int NotifiedCount);

// ---- Lịch dạy ----
public record SessionCellDto(long SessionId, int SubjectId, string SubjectName, string SubjectAbbr, string? TeacherName, string? TeacherAbbr, string Status, string Source);
public record DaySlotRow(int PeriodSlotId, string SessionOfDay, byte PeriodNo, TimeOnly StartTime, TimeOnly EndTime, IReadOnlyList<SessionCellDto?> Cells);
public record DayScheduleDto(DateOnly Date, string? HolidayReason, bool IsHoliday, IReadOnlyList<GridClassDto> Classes, IReadOnlyList<DaySlotRow> Rows);
public record MySessionDto(long SessionId, DateOnly Date, int PeriodSlotId, string SessionOfDay, byte PeriodNo, TimeOnly StartTime, TimeOnly EndTime,
    string ClassName, string CampusName, int SubjectId, string SubjectName, string SubjectAbbr, string Status, string Source);
public record TeacherOptionDto(int TeacherId, string Name, string? Abbr);
public record ViewSessionDto(long SessionId, DateOnly Date, int PeriodSlotId, string SessionOfDay, byte PeriodNo, TimeOnly StartTime, TimeOnly EndTime,
    string ClassName, string CampusName, int SubjectId, string SubjectName, string SubjectAbbr, string? TeacherName, string Status, string Source);

// ---- SCD-04: Phụ trách chuyên môn (tính từ thời khóa biểu đã phát hành, BR-14) ----
public record ResponsibilityItemDto(int SubjectId, string SubjectAbbr, string SubjectName, int ClassId, string ClassName, int PeriodsPerWeek);
public record ResponsibilityTeacherDto(int TeacherId, string TeacherName, string? TeacherAbbr, int TotalPeriods, IReadOnlyList<ResponsibilityItemDto> Items);
public record ResponsibilityDto(string? SemesterName, IReadOnlyList<ResponsibilityTeacherDto> Teachers);
