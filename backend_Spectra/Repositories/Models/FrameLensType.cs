#nullable disable
using System;

namespace Repositories.Models;

public partial class FrameLensType
{
    public Guid FrameLensTypeId { get; set; }

    public Guid? FrameId { get; set; }

    public Guid? LensTypeId { get; set; }

    public virtual Frame Frame { get; set; }

    public virtual LensType LensType { get; set; }
}
