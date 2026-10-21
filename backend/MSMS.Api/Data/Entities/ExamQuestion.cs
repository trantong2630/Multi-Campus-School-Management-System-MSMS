using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class ExamQuestion
{
    public int ExamQuestionId { get; set; }

    public int ExamId { get; set; }

    public int QuestionId { get; set; }

    public short OrderNo { get; set; }

    public decimal Points { get; set; }

    public virtual Exam Exam { get; set; } = null!;

    public virtual Question Question { get; set; } = null!;

    public virtual ICollection<SubmissionAnswer> SubmissionAnswer { get; set; } = new List<SubmissionAnswer>();
}
