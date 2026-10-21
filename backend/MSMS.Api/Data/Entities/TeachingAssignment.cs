using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class TeachingAssignment
{
    public int Id { get; set; }

    public int ClassId { get; set; }

    public int SubjectId { get; set; }

    public int TeacherId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int? AssignedByPersonId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Person? AssignedByPerson { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual Subject Subject { get; set; } = null!;

    public virtual StaffProfile Teacher { get; set; } = null!;
}
