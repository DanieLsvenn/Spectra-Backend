namespace Repositories.Models;

public static class StatusConstants
{
    // General
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Disabled = "disabled";

    // Frame
    public const string Available = "available";
    public const string OutOfStock = "out_of_stock";

    // Order
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string Processing = "processing";
    public const string Shipped = "shipped";
    public const string Delivered = "delivered";
    public const string Cancelled = "cancelled";

    // Preorder
    public const string Paid = "paid";
    public const string Converted = "converted";

    // Preorder Campaign
    public const string Upcoming = "upcoming";
    public const string Ended = "ended";

    // Complaint
    public const string UnderReview = "under_review";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string InProgress = "in_progress";
    public const string Resolved = "resolved";
    public const string ReturnShipped = "return_shipped";
    public const string ReturnReceived = "return_received";
    public const string Refunded = "refunded";

    // Complaint Request Types
    public const string RequestReturn = "return";
    public const string RequestExchange = "exchange";
    public const string RequestRefund = "refund";
    public const string RequestComplaint = "complaint";
    public const string RequestWarranty = "warranty";

    // Product Review
    public const string Visible = "visible";
    public const string Hidden = "hidden";
    public const string Flagged = "flagged";
}
