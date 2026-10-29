using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class SubmissionAnswer
{
    public long AnswerId { get; set; }

    public long SubmissionId { get; set; }

    public int ExamQuestionId { get; set; }

    public bool? IsCorrect { get; set; }

    public decimal? EarnedPoints { get; set; }

    public virtual ExamQuestion ExamQuestion { get; set; } = null!;

    public virtual Submission Submission { get; set; } = null!;

    public virtual ICollection<SubmissionAnswerOption> SubmissionAnswerOption { get; set; } = new List<SubmissionAnswerOption>();
}
