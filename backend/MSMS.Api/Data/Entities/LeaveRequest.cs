using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class LeaveRequest
{
    public int LeaveRequestId { get; set; }

    public int StudentId { get; set; }

    public int ParentId { get; set; }

    public int ClassId { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public string SessionOfDay { get; set; } = null!;

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public int? DecidedByPersonId { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionNote { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual Person? DecidedByPerson { get; set; }

    public virtual Person Parent { get; set; } = null!;

    public virtual StudentProfile Student { get; set; } = null!;
}
