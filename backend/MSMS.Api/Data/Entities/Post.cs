using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class Post
{
    public int PostId { get; set; }

    public int AuthorPersonId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string Visibility { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? HiddenByPersonId { get; set; }

    public DateTime? HiddenAt { get; set; }

    public string? HiddenReason { get; set; }

    public virtual Person AuthorPerson { get; set; } = null!;

    public virtual Person? HiddenByPerson { get; set; }

    public virtual ICollection<PostImage> PostImage { get; set; } = new List<PostImage>();
}
