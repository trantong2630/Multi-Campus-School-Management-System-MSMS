using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class TimetableVersion
{
    public int VersionId { get; set; }

    public int CampusId { get; set; }

    public int SemesterId { get; set; }

    public string Status { get; set; } = null!;

    public string? SourceFileName { get; set; }

    public int? ImportedByPersonId { get; set; }

    public DateTime ImportedAt { get; set; }

    public int? PublishedByPersonId { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int? DeletedSessionCount { get; set; }

    public int? CreatedSessionCount { get; set; }

    public virtual Campus Campus { get; set; } = null!;

    public virtual Person? ImportedByPerson { get; set; }

    public virtual Person? PublishedByPerson { get; set; }

    public virtual Semester Semester { get; set; } = null!;

    public virtual ICollection<TeachingSession> TeachingSession { get; set; } = new List<TeachingSession>();

    public virtual ICollection<TimetableEntry> TimetableEntry { get; set; } = new List<TimetableEntry>();
}
