#nullable disable
using System;
using System.Collections.Generic;

namespace Repositories.Models;

public partial class Color
{
    public Guid ColorId { get; set; }

    public string ColorName { get; set; }

    public string HexCode { get; set; }

    public string Status { get; set; }

    public virtual ICollection<FrameColor> FrameColors { get; set; } = new List<FrameColor>();

    public virtual ICollection<LensType> LensTypes { get; set; } = new List<LensType>();

    public virtual ICollection<LensIndex> LensIndices { get; set; } = new List<LensIndex>();
}
