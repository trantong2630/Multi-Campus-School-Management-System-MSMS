using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Holiday
{
    public int HolidayId { get; set; }

    public int AcademicYearId { get; set; }

    public int? CampusId { get; set; }

    public DateOnly HolidayDate { get; set; }

    public string? Reason { get; set; }

    public bool IsAdHoc { get; set; }

    public int? CreatedByPersonId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AcademicYear AcademicYear { get; set; } = null!;

    public virtual Campus? Campus { get; set; }

    public virtual Person? CreatedByPerson { get; set; }
}
