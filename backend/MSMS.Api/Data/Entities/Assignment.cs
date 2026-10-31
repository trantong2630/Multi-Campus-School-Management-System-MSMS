using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Assignment
{
    public int AssignmentId { get; set; }

    public int SubjectId { get; set; }

    public int ClassId { get; set; }

    public string Title { get; set; } = null!;

    public string? Instruction { get; set; }

    public DateTime OpenAt { get; set; }

    public DateTime DueAt { get; set; }

    /// <summary>Thời lượng làm bài (phút), tính từ lúc học sinh bấm Bắt đầu. Null là không giới hạn (BR-21).</summary>
    public short? DurationMinutes { get; set; }

    public int AssignedByPersonId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<AssignmentQuestion> AssignmentQuestion { get; set; } = new List<AssignmentQuestion>();

    public virtual ICollection<AssignmentRecipient> AssignmentRecipient { get; set; } = new List<AssignmentRecipient>();

    public virtual Person AssignedByPerson { get; set; } = null!;

    public virtual Class Class { get; set; } = null!;

    public virtual Subject Subject { get; set; } = null!;

    public virtual ICollection<Submission> Submission { get; set; } = new List<Submission>();
}
