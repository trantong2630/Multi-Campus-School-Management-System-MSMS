using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Exam
{
    public int ExamId { get; set; }

    public int SubjectId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public int OwnerPersonId { get; set; }

    public bool IsLocked { get; set; }

    public int? CopiedFromExamId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Assignment> Assignment { get; set; } = new List<Assignment>();

    public virtual Exam? CopiedFromExam { get; set; }

    public virtual ICollection<ExamQuestion> ExamQuestion { get; set; } = new List<ExamQuestion>();

    public virtual ICollection<Exam> InverseCopiedFromExam { get; set; } = new List<Exam>();

    public virtual Person OwnerPerson { get; set; } = null!;

    public virtual Subject Subject { get; set; } = null!;
}
