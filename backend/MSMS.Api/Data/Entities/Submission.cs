using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Submission
{
    public long SubmissionId { get; set; }

    public int AssignmentId { get; set; }

    public int StudentId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? LastSavedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public decimal? Score { get; set; }

    public short? CorrectCount { get; set; }

    public virtual AiFeedback? AiFeedback { get; set; }

    public virtual Assignment Assignment { get; set; } = null!;

    public virtual StudentProfile Student { get; set; } = null!;

    public virtual ICollection<SubmissionAnswer> SubmissionAnswer { get; set; } = new List<SubmissionAnswer>();
}
