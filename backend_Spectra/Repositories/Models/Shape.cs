#nullable disable
using System;
using System.Collections.Generic;

namespace Repositories.Models;

public partial class Shape
{
    public Guid ShapeId { get; set; }

    public string ShapeName { get; set; }

    public string Status { get; set; }

    public virtual ICollection<Frame> Frames { get; set; } = new List<Frame>();
}
