using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class SchoolProfile
{
    public int SchoolProfileId { get; set; }

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? LogoPath { get; set; }

    public int? UpdatedByPersonId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Person? UpdatedByPerson { get; set; }
}
