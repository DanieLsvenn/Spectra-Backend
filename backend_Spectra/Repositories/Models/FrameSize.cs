#nullable disable
using System;

namespace Repositories.Models;

public partial class FrameSize
{
    public Guid FrameSizeId { get; set; }

    public Guid? FrameId { get; set; }

    public string Size { get; set; }

    public bool? IsDefault { get; set; }

    public virtual Frame Frame { get; set; }
}
