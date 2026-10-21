using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class DepartmentHead
{
    public int Id { get; set; }

    public int DepartmentId { get; set; }

    public int StaffId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int? AssignedByPersonId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Person? AssignedByPerson { get; set; }

    public virtual SubjectDepartment Department { get; set; } = null!;

    public virtual StaffProfile Staff { get; set; } = null!;
}
