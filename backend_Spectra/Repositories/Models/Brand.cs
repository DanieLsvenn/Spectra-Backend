#nullable disable
using System;
using System.Collections.Generic;

namespace Repositories.Models;

public partial class Brand
{
    public Guid BrandId { get; set; }

    public string BrandName { get; set; }

    public string Status { get; set; }

    public virtual ICollection<Frame> Frames { get; set; } = new List<Frame>();

    public virtual ICollection<LensType> LensTypes { get; set; } = new List<LensType>();

    public virtual ICollection<LensIndex> LensIndices { get; set; } = new List<LensIndex>();
}
