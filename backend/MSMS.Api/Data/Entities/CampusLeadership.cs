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

    /// <summary>Thời điểm người này thực sự mất quyền (thay người hoặc kết thúc có hiệu lực ngay). Chỉ để đọc lịch sử; quyền vẫn tính theo ngày.</summary>
    public DateTime? EndedAt { get; set; }

    public virtual Person? AssignedByPerson { get; set; }

    public virtual Campus Campus { get; set; } = null!;

    public virtual StaffProfile Staff { get; set; } = null!;
}
