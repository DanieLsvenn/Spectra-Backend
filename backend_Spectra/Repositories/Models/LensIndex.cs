#nullable disable
using System;
using System.Collections.Generic;

namespace Repositories.Models;

public partial class LensIndex
{
    public Guid LensIndexId { get; set; }

    public double IndexValue { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public double AdditionalPrice { get; set; }

    public double? MinPrescription { get; set; }

    public double? MaxPrescription { get; set; }

    public Guid? BrandId { get; set; }

    public Guid? ColorId { get; set; }

    public string Status { get; set; }

    public virtual Brand Brand { get; set; }

    public virtual Color Color { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<PreorderItem> PreorderItems { get; set; } = new List<PreorderItem>();
}
