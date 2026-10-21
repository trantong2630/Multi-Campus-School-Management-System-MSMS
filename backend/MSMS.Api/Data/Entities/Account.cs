using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Account
{
    public int AccountId { get; set; }

    public int PersonId { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string AccountType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<PasswordResetOtp> PasswordResetOtp { get; set; } = new List<PasswordResetOtp>();

    public virtual Person Person { get; set; } = null!;
}
