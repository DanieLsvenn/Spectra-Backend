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

    /// <summary>
    /// Stock quantity for this specific frame-color variant
    /// </summary>
    public int? StockQuantity { get; set; }

    /// <summary>
    /// Extra cost for this specific frame-color combination (case by case)
    /// </summary>
    public double? ColorExtraCost { get; set; }

    public virtual Frame Frame { get; set; }

    public virtual Color Color { get; set; }
}
