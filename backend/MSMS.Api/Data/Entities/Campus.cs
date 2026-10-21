using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Campus
{
    public int CampusId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CampusLeadership> CampusLeadership { get; set; } = new List<CampusLeadership>();

    public virtual ICollection<Class> Class { get; set; } = new List<Class>();

    public virtual ICollection<Holiday> Holiday { get; set; } = new List<Holiday>();

    public virtual ICollection<StaffProfile> StaffProfile { get; set; } = new List<StaffProfile>();

    public virtual ICollection<TimetableVersion> TimetableVersion { get; set; } = new List<TimetableVersion>();
}
