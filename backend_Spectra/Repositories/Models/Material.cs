#nullable disable
using System;
using System.Collections.Generic;

namespace Repositories.Models;

public partial class Material
{
    public Guid MaterialId { get; set; }

    public string MaterialName { get; set; }

    public string Status { get; set; }

    public virtual ICollection<Frame> Frames { get; set; } = new List<Frame>();

    public virtual ICollection<LensType> LensTypes { get; set; } = new List<LensType>();
}
