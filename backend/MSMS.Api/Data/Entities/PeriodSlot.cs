using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class PeriodSlot
{
    public int PeriodSlotId { get; set; }

    public string SessionOfDay { get; set; } = null!;

    public byte PeriodNo { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<TeachingSession> TeachingSession { get; set; } = new List<TeachingSession>();

    public virtual ICollection<TimetableEntry> TimetableEntry { get; set; } = new List<TimetableEntry>();
}
