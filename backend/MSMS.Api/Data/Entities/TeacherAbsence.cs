using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class TeacherAbsence
{
    public int AbsenceId { get; set; }

    public int TeacherId { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    /// <summary>Các tiết vắng, cách nhau bằng dấu phẩy (mã khung giờ). Trống nghĩa là vắng cả ngày.</summary>
    public string? SlotIds { get; set; }

    public string? Reason { get; set; }

    public int? DeclaredByPersonId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Lúc tổ trưởng gửi khai báo cho hiệu phó duyệt. Trống nghĩa là tổ trưởng còn đang soạn đề xuất; từ lúc gửi, đề xuất bị khóa cho tới khi hiệu phó duyệt hoặc trả lại.</summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>Lý do hiệu phó trả khai báo về cho tổ trưởng sửa; xóa khi tổ trưởng gửi lại.</summary>
    public string? ReturnReason { get; set; }

    public virtual Person? DeclaredByPerson { get; set; }

    public virtual StaffProfile Teacher { get; set; } = null!;

    public virtual ICollection<TeachingSession> TeachingSession { get; set; } = new List<TeachingSession>();
}
