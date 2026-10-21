using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class TimetableEntry
{
    public int EntryId { get; set; }

    public int VersionId { get; set; }

    public int ClassId { get; set; }

    public byte DayOfWeek { get; set; }

    public int PeriodSlotId { get; set; }

    public int SubjectId { get; set; }

    public int TeacherId { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual PeriodSlot PeriodSlot { get; set; } = null!;

    public virtual Subject Subject { get; set; } = null!;

    public virtual StaffProfile Teacher { get; set; } = null!;

    public virtual TimetableVersion Version { get; set; } = null!;
}
