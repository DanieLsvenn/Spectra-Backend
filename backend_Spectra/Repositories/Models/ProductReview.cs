#nullable disable
using System;
using System.Collections.Generic;

namespace Repositories.Models;

public partial class ProductReview
{
    public Guid ReviewId { get; set; }

    public Guid? UserId { get; set; }

    public Guid? FrameId { get; set; }

    public Guid? OrderItemId { get; set; }

    public int Rating { get; set; }

    public string Title { get; set; }

    public string Comment { get; set; }

    public string Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; }

    public virtual Frame Frame { get; set; }

    public virtual OrderItem OrderItem { get; set; }
}
