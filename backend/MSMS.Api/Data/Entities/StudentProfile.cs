using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class StudentProfile
{
    public int StudentId { get; set; }

    public string Status { get; set; } = null!;

    public DateOnly? AdmissionDate { get; set; }

    public DateOnly? LeftDate { get; set; }

    public string? Note { get; set; }

    public virtual ICollection<AiFeedback> AiFeedback { get; set; } = new List<AiFeedback>();

    public virtual ICollection<AttendanceRecord> AttendanceRecord { get; set; } = new List<AttendanceRecord>();

    public virtual ICollection<Conversation> Conversation { get; set; } = new List<Conversation>();

    public virtual ICollection<Enrollment> Enrollment { get; set; } = new List<Enrollment>();

    public virtual ICollection<LeaveRequest> LeaveRequest { get; set; } = new List<LeaveRequest>();

    public virtual ICollection<ParentStudentLink> ParentStudentLink { get; set; } = new List<ParentStudentLink>();

    public virtual Person Student { get; set; } = null!;

    public virtual ICollection<AssignmentRecipient> AssignmentRecipient { get; set; } = new List<AssignmentRecipient>();

    public virtual ICollection<Submission> Submission { get; set; } = new List<Submission>();
}
