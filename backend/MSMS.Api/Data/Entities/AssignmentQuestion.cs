using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

/// <summary>Câu hỏi của một bài tập: thứ tự các câu, chụp lúc giao bài (BR-20).</summary>
public partial class AssignmentQuestion
{
    public int AssignmentQuestionId { get; set; }

    public int AssignmentId { get; set; }

    public int QuestionId { get; set; }

    public short OrderNo { get; set; }

    /// <summary>Điểm của câu trong bài này, chụp lúc giao bài. Điểm bài = tổng điểm câu đúng / tổng điểm tối đa × 10.</summary>
    public decimal Points { get; set; }

    public virtual Assignment Assignment { get; set; } = null!;

    public virtual Question Question { get; set; } = null!;

    public virtual ICollection<SubmissionAnswer> SubmissionAnswer { get; set; } = new List<SubmissionAnswer>();
}
