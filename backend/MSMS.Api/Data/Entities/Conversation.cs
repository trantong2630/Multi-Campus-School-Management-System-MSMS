using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Conversation
{
    public int ConversationId { get; set; }

    public int ParentId { get; set; }

    public int StaffId { get; set; }

    public int StudentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public virtual ICollection<Message> Message { get; set; } = new List<Message>();

    public virtual Person Parent { get; set; } = null!;

    public virtual StaffProfile Staff { get; set; } = null!;

    public virtual StudentProfile Student { get; set; } = null!;
}
