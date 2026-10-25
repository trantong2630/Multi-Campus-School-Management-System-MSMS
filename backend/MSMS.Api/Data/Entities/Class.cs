using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Class
{
    public int ClassId { get; set; }

    public int CampusId { get; set; }

    public int AcademicYearId { get; set; }

    public string Name { get; set; } = null!;

    public byte GradeLevel { get; set; }

    public bool IsActive { get; set; }

    public int? CopiedFromClassId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AcademicYear AcademicYear { get; set; } = null!;

    public virtual ICollection<Assignment> Assignment { get; set; } = new List<Assignment>();

    public virtual Campus Campus { get; set; } = null!;

    public virtual Class? CopiedFromClass { get; set; }

    public virtual ICollection<Enrollment> Enrollment { get; set; } = new List<Enrollment>();

    public virtual ICollection<HomeroomAssignment> HomeroomAssignment { get; set; } = new List<HomeroomAssignment>();

    public virtual ICollection<Class> InverseCopiedFromClass { get; set; } = new List<Class>();

    public virtual ICollection<LeaveRequest> LeaveRequest { get; set; } = new List<LeaveRequest>();

    public virtual ICollection<TeachingAssignment> TeachingAssignment { get; set; } = new List<TeachingAssignment>();

    public virtual ICollection<TeachingSession> TeachingSession { get; set; } = new List<TeachingSession>();

    public virtual ICollection<TimetableEntry> TimetableEntry { get; set; } = new List<TimetableEntry>();

    public virtual ICollection<TimetableVersionClass> TimetableVersionClass { get; set; } = new List<TimetableVersionClass>();
}
