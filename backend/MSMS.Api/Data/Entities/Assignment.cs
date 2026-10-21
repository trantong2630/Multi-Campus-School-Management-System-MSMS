using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Assignment
{
    public int AssignmentId { get; set; }

    public int ExamId { get; set; }

    public int ClassId { get; set; }

    public string Title { get; set; } = null!;

    public string? Instruction { get; set; }

    public DateTime OpenAt { get; set; }

    public DateTime DueAt { get; set; }

    public string Status { get; set; } = null!;

    public int AssignedByPersonId { get; set; }

    public DateTime? AssignedAt { get; set; }

    public int? ScorePublishedByPersonId { get; set; }

    public DateTime? ScorePublishedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AiFeedback> AiFeedback { get; set; } = new List<AiFeedback>();

    public virtual Person AssignedByPerson { get; set; } = null!;

    public virtual Class Class { get; set; } = null!;

    public virtual Exam Exam { get; set; } = null!;

    public virtual Person? ScorePublishedByPerson { get; set; }

    public virtual ICollection<Submission> Submission { get; set; } = new List<Submission>();
}
