#nullable disable
using System;
using System.Collections.Generic;

namespace Repositories.Models;

public partial class PreorderStatusLog
{
    public Guid LogId { get; set; }

    public Guid? PreorderId { get; set; }

    public string PreviousStatus { get; set; }

    public string NewStatus { get; set; }

    public string Message { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Preorder Preorder { get; set; }

    public virtual User CreatedByUser { get; set; }
}
