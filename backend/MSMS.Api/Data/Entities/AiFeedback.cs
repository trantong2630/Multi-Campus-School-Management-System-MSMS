using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

/// <summary>Phân tích AI cho một bài nộp, sinh tự động khi nộp bài (BR-23).</summary>
public partial class AiFeedback
{
    public long FeedbackId { get; set; }

    public long SubmissionId { get; set; }

    public string Status { get; set; } = null!;

    public string? AiContent { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? GeneratedAt { get; set; }

    public virtual Submission Submission { get; set; } = null!;
}
