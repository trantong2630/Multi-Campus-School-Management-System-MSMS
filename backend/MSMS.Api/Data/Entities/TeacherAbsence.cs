using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class TeacherAbsence
{
    public int AbsenceId { get; set; }

    public int TeacherId { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public string SessionOfDay { get; set; } = null!;

    public string? Reason { get; set; }

    public int? DeclaredByPersonId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Person? DeclaredByPerson { get; set; }

    public virtual StaffProfile Teacher { get; set; } = null!;

    public virtual ICollection<TeachingSession> TeachingSession { get; set; } = new List<TeachingSession>();
}
