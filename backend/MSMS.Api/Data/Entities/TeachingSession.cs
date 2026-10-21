using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class TeachingSession
{
    public long SessionId { get; set; }

    public int ClassId { get; set; }

    public int SubjectId { get; set; }

    public int TeacherId { get; set; }

    public DateOnly SessionDate { get; set; }

    public int PeriodSlotId { get; set; }

    public string Status { get; set; } = null!;

    public string Source { get; set; } = null!;

    public int? VersionId { get; set; }

    public long? OriginalSessionId { get; set; }

    public int? TeacherAbsenceId { get; set; }

    public string? ChangeNote { get; set; }

    public int? CreatedByPersonId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? UpdatedByPersonId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AttendanceRecord> AttendanceRecord { get; set; } = new List<AttendanceRecord>();

    public virtual Class Class { get; set; } = null!;

    public virtual Person? CreatedByPerson { get; set; }

    public virtual ICollection<TeachingSession> InverseOriginalSession { get; set; } = new List<TeachingSession>();

    public virtual TeachingSession? OriginalSession { get; set; }

    public virtual PeriodSlot PeriodSlot { get; set; } = null!;

    public virtual Subject Subject { get; set; } = null!;

    public virtual StaffProfile Teacher { get; set; } = null!;

    public virtual TeacherAbsence? TeacherAbsence { get; set; }

    public virtual Person? UpdatedByPerson { get; set; }

    public virtual TimetableVersion? Version { get; set; }
}
