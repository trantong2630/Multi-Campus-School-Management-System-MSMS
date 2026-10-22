using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

/// <summary>Danh sách học sinh được giao bài, chụp lúc bài chuyển sang Đã giao (BR-21).</summary>
public partial class AssignmentRecipient
{
    public int AssignmentId { get; set; }

    public int StudentId { get; set; }

    public virtual Assignment Assignment { get; set; } = null!;

    public virtual StudentProfile Student { get; set; } = null!;
}
