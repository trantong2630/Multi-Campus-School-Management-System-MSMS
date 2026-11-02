using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class SubmissionAnswerOption
{
    public long AnswerId { get; set; }

    public int OptionId { get; set; }

    public virtual SubmissionAnswer Answer { get; set; } = null!;

    public virtual QuestionOption Option { get; set; } = null!;
}
