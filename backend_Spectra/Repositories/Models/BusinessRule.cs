#nullable disable
using System;

namespace Repositories.Models;

public partial class BusinessRule
{
    public Guid RuleId { get; set; }

    public string RuleKey { get; set; }

    public string RuleValue { get; set; }

    public string Description { get; set; }

    public string Category { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string UpdatedBy { get; set; }
}
