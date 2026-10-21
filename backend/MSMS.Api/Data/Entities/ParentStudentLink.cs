using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class ParentStudentLink
{
    public int LinkId { get; set; }

    public int ParentId { get; set; }

    public int StudentId { get; set; }

    public string? Relationship { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Person Parent { get; set; } = null!;

    public virtual StudentProfile Student { get; set; } = null!;
}
