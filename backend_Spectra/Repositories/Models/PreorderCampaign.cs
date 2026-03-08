#nullable disable
using System;
using System.Collections.Generic;

namespace Repositories.Models;

public partial class PreorderCampaign
{
    public Guid CampaignId { get; set; }

    public string CampaignName { get; set; }

    public string Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int? MaxSlots { get; set; }

    public int CurrentSlots { get; set; }

    public string Status { get; set; }

    public DateTime? EstimatedDeliveryDate { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<CampaignFrame> CampaignFrames { get; set; } = new List<CampaignFrame>();

    public virtual ICollection<Preorder> Preorders { get; set; } = new List<Preorder>();
}
