using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Question
{
    public int QuestionId { get; set; }

    public int SubjectId { get; set; }

    public byte? GradeLevel { get; set; }

    public string? Topic { get; set; }

    public string Content { get; set; } = null!;

    public string? Explanation { get; set; }

    public string Status { get; set; } = null!;

    public bool IsAiGenerated { get; set; }

    public bool IsHidden { get; set; }

    public int CreatedByPersonId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Person CreatedByPerson { get; set; } = null!;

    public virtual ICollection<AssignmentQuestion> AssignmentQuestion { get; set; } = new List<AssignmentQuestion>();

    public virtual ICollection<QuestionOption> QuestionOption { get; set; } = new List<QuestionOption>();

    public virtual Subject Subject { get; set; } = null!;
}
