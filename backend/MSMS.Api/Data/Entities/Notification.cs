using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Notification
{
    public long NotificationId { get; set; }

    public int RecipientPersonId { get; set; }

    public string NotificationType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Body { get; set; }

    public string? RefEntityType { get; set; }

    public long? RefEntityId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Person RecipientPerson { get; set; } = null!;
}
