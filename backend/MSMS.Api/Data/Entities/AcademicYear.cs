using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class AcademicYear
{
    public int AcademicYearId { get; set; }

    public string Name { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsCurrent { get; set; }

    public virtual ICollection<Class> Class { get; set; } = new List<Class>();

    public virtual ICollection<Holiday> Holiday { get; set; } = new List<Holiday>();

    public virtual ICollection<Semester> Semester { get; set; } = new List<Semester>();
}
