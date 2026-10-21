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

    public virtual ICollection<SubmissionAnswer> SubmissionAnswer { get; set; } = new List<SubmissionAnswer>();
}
