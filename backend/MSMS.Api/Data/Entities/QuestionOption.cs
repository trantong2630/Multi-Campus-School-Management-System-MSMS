using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class QuestionOption
{
    public int OptionId { get; set; }

    public int QuestionId { get; set; }

    public string OptionLabel { get; set; } = null!;

    public string Content { get; set; } = null!;

    public bool IsCorrect { get; set; }

    public virtual Question Question { get; set; } = null!;

    public virtual ICollection<SubmissionAnswerOption> SubmissionAnswerOption { get; set; } = new List<SubmissionAnswerOption>();
}
