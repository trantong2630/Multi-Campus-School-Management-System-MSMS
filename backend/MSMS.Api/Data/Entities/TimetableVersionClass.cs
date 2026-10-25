using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

/// <summary>Các lớp có trong một phiên bản thời khóa biểu; Phát hành chỉ ghi đè những lớp này (BR-12, BR-14).</summary>
public partial class TimetableVersionClass
{
    public int VersionId { get; set; }

    public int ClassId { get; set; }

    public virtual Class Class { get; set; } = null!;

    public virtual TimetableVersion Version { get; set; } = null!;
}
