using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Semester
{
    public int SemesterId { get; set; }

    public int AcademicYearId { get; set; }

    public byte SemesterNo { get; set; }

    public string Name { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public virtual AcademicYear AcademicYear { get; set; } = null!;

    public virtual ICollection<TimetableVersion> TimetableVersion { get; set; } = new List<TimetableVersion>();
}
