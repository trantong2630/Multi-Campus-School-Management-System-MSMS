using System;
using System.Collections.Generic;

namespace MSMS.Api.Data.Entities;

public partial class PostImage
{
    public int PostImageId { get; set; }

    public int PostId { get; set; }

    public string FilePath { get; set; } = null!;

    public byte OrderNo { get; set; }

    public int? FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual Post Post { get; set; } = null!;
}
