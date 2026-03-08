#nullable disable
using System;
using System.Collections.Generic;

namespace Repositories.Models;

public partial class FrameColor
{
    public Guid FrameColorId { get; set; }

    public Guid? FrameId { get; set; }

    public Guid? ColorId { get; set; }

    public bool? IsDefault { get; set; }

    public virtual Frame Frame { get; set; }

    public virtual Color Color { get; set; }
}
