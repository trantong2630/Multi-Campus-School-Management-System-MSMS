using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class PasswordResetOtp
{
    public long OtpId { get; set; }

    public int AccountId { get; set; }

    public string OtpHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public byte AttemptCount { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
