using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Message
{
    public long MessageId { get; set; }

    public int ConversationId { get; set; }

    public int SenderPersonId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual Person SenderPerson { get; set; } = null!;
}
