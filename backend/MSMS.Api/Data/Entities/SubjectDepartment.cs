using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class SubjectDepartment
{
    public int DepartmentId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<DepartmentHead> DepartmentHead { get; set; } = new List<DepartmentHead>();

    public virtual ICollection<StaffProfile> StaffProfile { get; set; } = new List<StaffProfile>();

    public virtual ICollection<Subject> Subject { get; set; } = new List<Subject>();
}
