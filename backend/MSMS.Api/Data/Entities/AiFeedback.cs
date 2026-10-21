using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class AiFeedback
{
    public long FeedbackId { get; set; }

    public int AssignmentId { get; set; }

    public int? StudentId { get; set; }

    public string Status { get; set; } = null!;

    public string? AiContent { get; set; }

    public string? EditedContent { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? GeneratedAt { get; set; }

    public int? ApprovedByPersonId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public virtual Person? ApprovedByPerson { get; set; }

    public virtual Assignment Assignment { get; set; } = null!;

    public virtual StudentProfile? Student { get; set; }
}
