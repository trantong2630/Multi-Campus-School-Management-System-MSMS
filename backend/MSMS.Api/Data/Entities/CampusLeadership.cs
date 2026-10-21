using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class CampusLeadership
{
    public int Id { get; set; }

    public int CampusId { get; set; }

    public int StaffId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int? AssignedByPersonId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Person? AssignedByPerson { get; set; }

    public virtual Campus Campus { get; set; } = null!;

    public virtual StaffProfile Staff { get; set; } = null!;
}
