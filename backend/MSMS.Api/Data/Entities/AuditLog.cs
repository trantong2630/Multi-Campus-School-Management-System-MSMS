using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class AuditLog
{
    public long AuditId { get; set; }

    public string Category { get; set; } = null!;

    public string Action { get; set; } = null!;

    public int ActorPersonId { get; set; }

    public string? TargetIds { get; set; }

    public string? Detail { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Person ActorPerson { get; set; } = null!;
}
