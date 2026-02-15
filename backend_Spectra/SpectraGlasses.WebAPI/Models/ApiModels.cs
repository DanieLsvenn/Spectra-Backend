namespace SpectraGlasses.WebAPI.Models
{
    public class ErrorResponse
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Role { get; set; }
    }

    public class CreateFrameRequest
    {
        public string FrameName { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public string? Color { get; set; }
        public string? Material { get; set; }
        public int? LensWidth { get; set; }
        public int? BridgeWidth { get; set; }
        public int? FrameWidth { get; set; }
        public int? TempleLength { get; set; }
        public string? Shape { get; set; }
        public string? Size { get; set; }
        public double? BasePrice { get; set; }
    }

    public class UpdateFrameRequest
    {
        public string? FrameName { get; set; }
        public string? Brand { get; set; }
        public string? Color { get; set; }
        public string? Material { get; set; }
        public int? LensWidth { get; set; }
        public int? BridgeWidth { get; set; }
        public int? FrameWidth { get; set; }
        public int? TempleLength { get; set; }
        public string? Shape { get; set; }
        public string? Size { get; set; }
        public double? BasePrice { get; set; }
        public string? Status { get; set; }
    }

    public class FrameValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    #region LensType Models

    public class CreateLensTypeRequest
    {
        public string LensSpecification { get; set; } = string.Empty;
        public bool? RequiresPrescription { get; set; }
        public double? ExtraPrice { get; set; }
    }

    public class UpdateLensTypeRequest
    {
        public string? LensSpecification { get; set; }
        public bool? RequiresPrescription { get; set; }
        public double? ExtraPrice { get; set; }
    }

    #endregion

    #region LensFeature Models

    public class CreateLensFeatureRequest
    {
        public double? LensIndex { get; set; }
        public string FeatureSpecification { get; set; } = string.Empty;
        public double? ExtraPrice { get; set; }
    }

    public class UpdateLensFeatureRequest
    {
        public double? LensIndex { get; set; }
        public string? FeatureSpecification { get; set; }
        public double? ExtraPrice { get; set; }
    }

    public class PriceCalculationRequest
    {
        public double BasePrice { get; set; }
        public Guid? LensFeatureId { get; set; }
        public Guid? LensTypeId { get; set; }
    }

    public class PriceCalculationResponse
    {
        public double BasePrice { get; set; }
        public double FeatureExtraPrice { get; set; }
        public double LensTypeExtraPrice { get; set; }
        public double TotalPrice { get; set; }
    }

    #endregion

    #region Order Models

    public class CreateOrderRequest
    {
        public string ShippingAddress { get; set; } = string.Empty;
        public List<CreateOrderItemRequest> Items { get; set; } = new();
    }

    public class CreateOrderItemRequest
    {
        public Guid FrameId { get; set; }
        public Guid? LensTypeId { get; set; }
        public Guid? FeatureId { get; set; }
        public Guid? PrescriptionId { get; set; }
        public int Quantity { get; set; } = 1;
        public string? SelectedColor { get; set; }
    }

    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class OrderSummaryResponse
    {
        public Guid OrderId { get; set; }
        public Guid? UserId { get; set; }
        public double? TotalAmount { get; set; }
        public string? ShippingAddress { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int ItemCount { get; set; }
    }

    #endregion

    #region Preorder Models

    public class CreatePreorderRequest
    {
        public DateTime? ExpectedDate { get; set; }
        public List<CreatePreorderItemRequest> Items { get; set; } = new();
    }

    public class CreatePreorderItemRequest
    {
        public Guid FrameId { get; set; }
        public Guid? LensTypeId { get; set; }
        public Guid? FeatureId { get; set; }
        public Guid? PrescriptionId { get; set; }
        public int Quantity { get; set; } = 1;
        public string? SelectedColor { get; set; }
    }

    public class ConvertPreorderRequest
    {
        public string ShippingAddress { get; set; } = string.Empty;
    }

    #endregion

    #region Payment Models

    public class CreatePaymentRequest
    {
        public Guid? OrderId { get; set; }
        public Guid? PreorderId { get; set; }
        public string PaymentMethod { get; set; } = "vnpay";
    }

    public class VnPayReturnRequest
    {
        public string vnp_TxnRef { get; set; } = string.Empty;
        public string vnp_ResponseCode { get; set; } = string.Empty;
        public string vnp_TransactionNo { get; set; } = string.Empty;
        public string vnp_SecureHash { get; set; } = string.Empty;
        public string vnp_Amount { get; set; } = string.Empty;
        public string vnp_OrderInfo { get; set; } = string.Empty;
        public string vnp_PayDate { get; set; } = string.Empty;
        public string vnp_BankCode { get; set; } = string.Empty;
    }

    public class PaymentResponse
    {
        public Guid PaymentId { get; set; }
        public Guid? OrderId { get; set; }
        public Guid? PreorderId { get; set; }
        public double? Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentStatus { get; set; }
        public string? PaymentUrl { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    #endregion
}
