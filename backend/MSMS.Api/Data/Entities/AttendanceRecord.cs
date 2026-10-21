using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class AttendanceRecord
{
    public long AttendanceId { get; set; }

    public long SessionId { get; set; }

    public int StudentId { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public bool IsBackfilled { get; set; }

    public int RecordedByPersonId { get; set; }

    public DateTime RecordedAt { get; set; }

    public int? UpdatedByPersonId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Person RecordedByPerson { get; set; } = null!;

    public virtual TeachingSession Session { get; set; } = null!;

    public virtual StudentProfile Student { get; set; } = null!;

    public virtual Person? UpdatedByPerson { get; set; }
}
