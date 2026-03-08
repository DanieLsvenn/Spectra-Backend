#nullable disable
using System;
using System.Collections.Generic;

namespace Repositories.Models;

public partial class CampaignFrame
{
    public Guid CampaignFrameId { get; set; }

    public Guid? CampaignId { get; set; }

    public Guid? FrameId { get; set; }

    public double? CampaignPrice { get; set; }

    public int MaxQuantityPerOrder { get; set; }

    public virtual PreorderCampaign Campaign { get; set; }

    public virtual Frame Frame { get; set; }
}
