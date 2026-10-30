using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Subject
{
    public int SubjectId { get; set; }

    public int DepartmentId { get; set; }

    public string Name { get; set; } = null!;

    public string Abbreviation { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual SubjectDepartment Department { get; set; } = null!;

    public virtual ICollection<Assignment> Assignment { get; set; } = new List<Assignment>();

    public virtual ICollection<Question> Question { get; set; } = new List<Question>();

    public virtual ICollection<TeachingSession> TeachingSession { get; set; } = new List<TeachingSession>();

    public virtual ICollection<TimetableEntry> TimetableEntry { get; set; } = new List<TimetableEntry>();
}
