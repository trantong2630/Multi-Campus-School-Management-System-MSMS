using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Person
{
    public int PersonId { get; set; }

    public string PersonType { get; set; } = null!;

    public string PersonCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public string? NationalId { get; set; }

    public string? Gender { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? AvatarPath { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Account? Account { get; set; }

    public virtual ICollection<AiFeedback> AiFeedback { get; set; } = new List<AiFeedback>();

    public virtual ICollection<Assignment> AssignmentAssignedByPerson { get; set; } = new List<Assignment>();

    public virtual ICollection<Assignment> AssignmentScorePublishedByPerson { get; set; } = new List<Assignment>();

    public virtual ICollection<AttendanceRecord> AttendanceRecordRecordedByPerson { get; set; } = new List<AttendanceRecord>();

    public virtual ICollection<AttendanceRecord> AttendanceRecordUpdatedByPerson { get; set; } = new List<AttendanceRecord>();

    public virtual ICollection<AuditLog> AuditLog { get; set; } = new List<AuditLog>();

    public virtual ICollection<CampusLeadership> CampusLeadership { get; set; } = new List<CampusLeadership>();

    public virtual ICollection<Conversation> Conversation { get; set; } = new List<Conversation>();

    public virtual ICollection<DepartmentHead> DepartmentHead { get; set; } = new List<DepartmentHead>();

    public virtual ICollection<Exam> Exam { get; set; } = new List<Exam>();

    public virtual ICollection<Holiday> Holiday { get; set; } = new List<Holiday>();

    public virtual ICollection<HomeroomAssignment> HomeroomAssignment { get; set; } = new List<HomeroomAssignment>();

    public virtual ICollection<LeaveRequest> LeaveRequestDecidedByPerson { get; set; } = new List<LeaveRequest>();

    public virtual ICollection<LeaveRequest> LeaveRequestParent { get; set; } = new List<LeaveRequest>();

    public virtual ICollection<Message> Message { get; set; } = new List<Message>();

    public virtual ICollection<Notification> Notification { get; set; } = new List<Notification>();

    public virtual ICollection<ParentStudentLink> ParentStudentLink { get; set; } = new List<ParentStudentLink>();

    public virtual ICollection<Post> PostAuthorPerson { get; set; } = new List<Post>();

    public virtual ICollection<Post> PostHiddenByPerson { get; set; } = new List<Post>();

    public virtual ICollection<Question> Question { get; set; } = new List<Question>();

    public virtual ICollection<SchoolProfile> SchoolProfile { get; set; } = new List<SchoolProfile>();

    public virtual StaffProfile? StaffProfile { get; set; }

    public virtual StudentProfile? StudentProfile { get; set; }

    public virtual ICollection<TeacherAbsence> TeacherAbsence { get; set; } = new List<TeacherAbsence>();

    public virtual ICollection<TeachingAssignment> TeachingAssignment { get; set; } = new List<TeachingAssignment>();

    public virtual ICollection<TeachingSession> TeachingSessionCreatedByPerson { get; set; } = new List<TeachingSession>();

    public virtual ICollection<TeachingSession> TeachingSessionUpdatedByPerson { get; set; } = new List<TeachingSession>();

    public virtual ICollection<TimetableVersion> TimetableVersionImportedByPerson { get; set; } = new List<TimetableVersion>();

    public virtual ICollection<TimetableVersion> TimetableVersionPublishedByPerson { get; set; } = new List<TimetableVersion>();
}
