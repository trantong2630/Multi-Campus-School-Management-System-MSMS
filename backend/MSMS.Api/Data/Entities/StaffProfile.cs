using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class StaffProfile
{
    public int StaffId { get; set; }

    public int PrimaryCampusId { get; set; }

    public int? DepartmentId { get; set; }

    public bool IsActive { get; set; }

    public string? TeacherAbbr { get; set; }

    public string? Position { get; set; }

    public DateOnly? HireDate { get; set; }

    public virtual ICollection<CampusLeadership> CampusLeadership { get; set; } = new List<CampusLeadership>();

    public virtual ICollection<Conversation> Conversation { get; set; } = new List<Conversation>();

    public virtual SubjectDepartment? Department { get; set; }

    public virtual ICollection<DepartmentHead> DepartmentHead { get; set; } = new List<DepartmentHead>();

    public virtual ICollection<HomeroomAssignment> HomeroomAssignment { get; set; } = new List<HomeroomAssignment>();

    public virtual Campus PrimaryCampus { get; set; } = null!;

    public virtual Person Staff { get; set; } = null!;

    public virtual ICollection<TeacherAbsence> TeacherAbsence { get; set; } = new List<TeacherAbsence>();

    public virtual ICollection<TeachingAssignment> TeachingAssignment { get; set; } = new List<TeachingAssignment>();

    public virtual ICollection<TeachingSession> TeachingSession { get; set; } = new List<TeachingSession>();

    public virtual ICollection<TimetableEntry> TimetableEntry { get; set; } = new List<TimetableEntry>();
}
