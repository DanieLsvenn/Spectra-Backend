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

    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Phone { get; set; }
    }

    public class GoogleLoginRequest
    {
        public string IdToken { get; set; } = string.Empty;
    }

    public class CreateFrameRequest
    {
        public string FrameName { get; set; } = string.Empty;
        public Guid? BrandId { get; set; }
        public Guid? MaterialId { get; set; }
        public Guid? ShapeId { get; set; }
        public int? LensWidth { get; set; }
        public int? BridgeWidth { get; set; }
        public int? FrameWidth { get; set; }
        public int? TempleLength { get; set; }
        public string? Size { get; set; }
        public double? BasePrice { get; set; }
        public int? StockQuantity { get; set; }
        public int? ReorderLevel { get; set; }
        /// <summary>
        /// Colors with per-variant stock quantities
        /// </summary>
        public List<FrameColorVariantRequest>? ColorVariants { get; set; }
        /// <summary>
        /// Supported lens type IDs for this frame
        /// </summary>
        public List<Guid>? SupportedLensTypeIds { get; set; }
        /// <summary>
        /// Minimum supported sphere (Rx) value
        /// </summary>
        public double? MinRx { get; set; }
        /// <summary>
        /// Maximum supported sphere (Rx) value
        /// </summary>
        public double? MaxRx { get; set; }
        /// <summary>
        /// Minimum supported pupillary distance
        /// </summary>
        public int? MinPd { get; set; }
        /// <summary>
        /// Maximum supported pupillary distance
        /// </summary>
        public int? MaxPd { get; set; }
    }

    public class UpdateFrameRequest
    {
        public string? FrameName { get; set; }
        public Guid? BrandId { get; set; }
        public Guid? MaterialId { get; set; }
        public Guid? ShapeId { get; set; }
        public int? LensWidth { get; set; }
        public int? BridgeWidth { get; set; }
        public int? FrameWidth { get; set; }
        public int? TempleLength { get; set; }
        public string? Size { get; set; }
        public double? BasePrice { get; set; }
        public string? Status { get; set; }
        public int? StockQuantity { get; set; }
        public int? ReorderLevel { get; set; }
        /// <summary>
        /// Colors with per-variant stock quantities. If provided, replaces all existing color variants.
        /// </summary>
        public List<FrameColorVariantRequest>? ColorVariants { get; set; }
        /// <summary>
        /// Supported lens type IDs. If provided, replaces all existing supported lens types.
        /// </summary>
        public List<Guid>? SupportedLensTypeIds { get; set; }
        public double? MinRx { get; set; }
        public double? MaxRx { get; set; }
        public int? MinPd { get; set; }
        public int? MaxPd { get; set; }
    }

    /// <summary>
    /// Represents a color variant with its stock quantity
    /// </summary>
    public class FrameColorVariantRequest
    {
        public Guid ColorId { get; set; }
        public int StockQuantity { get; set; }
    }

    public class FrameValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class UpdateStockRequest
    {
        public int Quantity { get; set; }
        public int? ReorderLevel { get; set; }
    }

    #region LensType Models

    public class CreateLensTypeRequest
    {
        public string LensSpecification { get; set; } = string.Empty;
        public bool? RequiresPrescription { get; set; }
        public double? BasePrice { get; set; }
    }

    public class UpdateLensTypeRequest
    {
        public string? LensSpecification { get; set; }
        public bool? RequiresPrescription { get; set; }
        public double? BasePrice { get; set; }
    }

    #endregion

    #region LensFeature Models

    public class CreateLensFeatureRequest
    {
        public string FeatureSpecification { get; set; } = string.Empty;
        public double? ExtraPrice { get; set; }
    }

    public class UpdateLensFeatureRequest
    {
        public string? FeatureSpecification { get; set; }
        public double? ExtraPrice { get; set; }
    }

    public class PriceCalculationRequest
    {
        public double BasePrice { get; set; }
        public Guid? LensFeatureId { get; set; }
        public Guid? LensTypeId { get; set; }
        public Guid? LensIndexId { get; set; }
    }

    public class PriceCalculationResponse
    {
        public double BasePrice { get; set; }
        public double FeatureExtraPrice { get; set; }
        public double LensTypeExtraPrice { get; set; }
        public double LensIndexExtraPrice { get; set; }
        public double TotalPrice { get; set; }
    }

    #endregion

    #region Order Models

    public class CreateOrderRequest
    {
        public string ShippingAddress { get; set; } = string.Empty;
        public string? ShippingMethod { get; set; }
        public List<CreateOrderItemRequest> Items { get; set; } = new();
    }

    public class CreateOrderItemRequest
    {
        public Guid FrameId { get; set; }
        public Guid? LensTypeId { get; set; }
        public Guid? FeatureId { get; set; }
        public Guid? LensIndexId { get; set; }
        public Guid? PrescriptionId { get; set; }
        public int Quantity { get; set; } = 1;
        public Guid? SelectedColorId { get; set; }
        public string? SelectedSize { get; set; }
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
        public Guid? CampaignId { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public List<CreatePreorderItemRequest> Items { get; set; } = new();
    }

    public class CreatePreorderItemRequest
    {
        public Guid FrameId { get; set; }
        public Guid? LensTypeId { get; set; }
        public Guid? FeatureId { get; set; }
        public Guid? LensIndexId { get; set; }
        public Guid? PrescriptionId { get; set; }
        public int Quantity { get; set; } = 1;
        public Guid? SelectedColorId { get; set; }
        public string? SelectedSize { get; set; }
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

    #region Prescription Models

    public class CreatePrescriptionRequest
    {
        // Right eye (OD - Oculus Dexter)
        public double? SphereRight { get; set; }
        public double? CylinderRight { get; set; }
        public int? AxisRight { get; set; }
        public double? AddRight { get; set; }

        // Left eye (OS - Oculus Sinister)
        public double? SphereLeft { get; set; }
        public double? CylinderLeft { get; set; }
        public int? AxisLeft { get; set; }
        public double? AddLeft { get; set; }

        // Both eyes
        public int? PupillaryDistance { get; set; }

        // Doctor/Clinic info
        public string? DoctorName { get; set; }
        public string? ClinicName { get; set; }

        // Validity
        public DateTime? ExpirationDate { get; set; }
    }

    public class UpdatePrescriptionRequest
    {
        public double? SphereRight { get; set; }
        public double? CylinderRight { get; set; }
        public int? AxisRight { get; set; }
        public double? AddRight { get; set; }
        public double? SphereLeft { get; set; }
        public double? CylinderLeft { get; set; }
        public int? AxisLeft { get; set; }
        public double? AddLeft { get; set; }
        public int? PupillaryDistance { get; set; }
        public string? DoctorName { get; set; }
        public string? ClinicName { get; set; }
        public DateTime? ExpirationDate { get; set; }
    }

    public class PrescriptionResponse
    {
        public Guid PrescriptionId { get; set; }
        public Guid? UserId { get; set; }
        
        // Right eye
        public double? SphereRight { get; set; }
        public double? CylinderRight { get; set; }
        public int? AxisRight { get; set; }
        public double? AddRight { get; set; }
        
        // Left eye
        public double? SphereLeft { get; set; }
        public double? CylinderLeft { get; set; }
        public int? AxisLeft { get; set; }
        public double? AddLeft { get; set; }
        
        // Both eyes
        public int? PupillaryDistance { get; set; }
        
        // Doctor/Clinic
        public string? DoctorName { get; set; }
        public string? ClinicName { get; set; }
        
        // Status
        public DateTime? ExpirationDate { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool IsExpired { get; set; }
        public int DaysUntilExpiration { get; set; }
    }

    #endregion

    #region Complaint Request Models

    public class CreateComplaintRequest
    {
        public Guid OrderItemId { get; set; }
        public string RequestType { get; set; } = string.Empty; // return, exchange, refund, complaint, warranty
        public string Reason { get; set; } = string.Empty;
        public string? MediaUrl { get; set; }
    }

    public class UpdateComplaintRequest
    {
        public string? RequestType { get; set; }
        public string? Reason { get; set; }
        public string? MediaUrl { get; set; }
    }

    public class UpdateComplaintStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class ComplaintResponse
    {
        public Guid RequestId { get; set; }
        public Guid? UserId { get; set; }
        public Guid? OrderItemId { get; set; }
        public string? RequestType { get; set; }
        public string? Reason { get; set; }
        public string? MediaUrl { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public bool CanModify { get; set; }
    }

    #endregion

    #region User Management Models

    public class CreateUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string Role { get; set; } = "customer";
    }

    public class UpdateUserRequest
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
    }

    public class UpdateUserStatusRequest
    {
        public string Status { get; set; } = string.Empty; // active, inactive, suspended
    }

    public class UpdateUserRoleRequest
    {
        public string Role { get; set; } = string.Empty; // customer, staff, manager, admin
    }

    public class UserResponse
    {
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class UserSearchRequest
    {
        public string? SearchTerm { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    #endregion

    #region Frame Media Models

    public class AddFrameMediaRequest
    {
        public Guid FrameId { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string MediaType { get; set; } = "image";
        public Guid? ColorId { get; set; }
    }

    public class AddMultipleFrameMediaRequest
    {
        public Guid FrameId { get; set; }
        public List<MediaItemRequest> MediaItems { get; set; } = new();
    }

    public class MediaItemRequest
    {
        public string MediaUrl { get; set; } = string.Empty;
        public string MediaType { get; set; } = "image";
        public Guid? ColorId { get; set; }
    }

    public class UpdateFrameMediaRequest
    {
        public string? MediaUrl { get; set; }
        public string? MediaType { get; set; }
        public Guid? ColorId { get; set; }
    }

    public class FrameMediaResponse
    {
        public Guid MediaId { get; set; }
        public Guid? FrameId { get; set; }
        public string? MediaUrl { get; set; }
        public string? MediaType { get; set; }
        public Guid? ColorId { get; set; }
        public string? ColorName { get; set; }
        public string? HexCode { get; set; }
    }

    public class ImageUploadResponse
    {
        public bool Success { get; set; }
        public string Url { get; set; } = string.Empty;
        public string PublicId { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    public class FrameMediaUploadResponse
    {
        public Guid MediaId { get; set; }
        public Guid? FrameId { get; set; }
        public string? MediaUrl { get; set; }
        public string? MediaType { get; set; }
        public Guid? ColorId { get; set; }
        public string? PublicId { get; set; }
    }

    #endregion

    #region Brand Models

    public class CreateBrandRequest
    {
        public string BrandName { get; set; } = string.Empty;
    }

    public class UpdateBrandRequest
    {
        public string? BrandName { get; set; }
    }

    #endregion

    #region Material Models

    public class CreateMaterialRequest
    {
        public string MaterialName { get; set; } = string.Empty;
    }

    public class UpdateMaterialRequest
    {
        public string? MaterialName { get; set; }
    }

    #endregion

    #region Shape Models

    public class CreateShapeRequest
    {
        public string ShapeName { get; set; } = string.Empty;
    }

    public class UpdateShapeRequest
    {
        public string? ShapeName { get; set; }
    }

    #endregion

    #region Color Models

    public class CreateColorRequest
    {
        public string ColorName { get; set; } = string.Empty;
        public string? HexCode { get; set; }
    }

    public class UpdateColorRequest
    {
        public string? ColorName { get; set; }
        public string? HexCode { get; set; }
    }

    #endregion

    #region LensIndex Models

    public class CreateLensIndexRequest
    {
        public double IndexValue { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public double AdditionalPrice { get; set; }
        public double? MinPrescription { get; set; }
        public double? MaxPrescription { get; set; }
        public Guid? BrandId { get; set; }
        public Guid? ColorId { get; set; }
    }

    public class UpdateLensIndexRequest
    {
        public double? IndexValue { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public double? AdditionalPrice { get; set; }
        public double? MinPrescription { get; set; }
        public double? MaxPrescription { get; set; }
        public Guid? BrandId { get; set; }
        public Guid? ColorId { get; set; }
    }

    #endregion

    #region ProductReview Models

    public class CreateReviewRequest
    {
        public Guid FrameId { get; set; }
        public Guid? OrderItemId { get; set; }
        public int Rating { get; set; }
        public string? Title { get; set; }
        public string? Comment { get; set; }
    }

    public class UpdateReviewRequest
    {
        public int? Rating { get; set; }
        public string? Title { get; set; }
        public string? Comment { get; set; }
    }

    #endregion

    #region PreorderCampaign Models

    public class CreateCampaignRequest
    {
        public string CampaignName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? MaxSlots { get; set; }
        public DateTime? EstimatedDeliveryDate { get; set; }
        public List<CampaignFrameRequest> Frames { get; set; } = new();
    }

    public class CampaignFrameRequest
    {
        public Guid FrameId { get; set; }
        public double? CampaignPrice { get; set; }
        public int MaxQuantityPerOrder { get; set; } = 2;
    }

    public class UpdateCampaignRequest
    {
        public string? CampaignName { get; set; }
        public string? Description { get; set; }
        public int? MaxSlots { get; set; }
        public DateTime? EstimatedDeliveryDate { get; set; }
    }

    #endregion

    #region Shipping Models

    public class CalculateShippingRequest
    {
        public string ShippingMethod { get; set; } = "standard";
        public double OrderSubtotal { get; set; }
    }

    public class AssignTrackingRequest
    {
        public string TrackingNumber { get; set; } = string.Empty;
        public string Carrier { get; set; } = string.Empty;
    }

    public class GoShipAddressApiModel
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string Ward { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    public class GoShipParcelApiModel
    {
        public int Cod { get; set; }
        public int Weight { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Length { get; set; }
        public string Metadata { get; set; } = string.Empty;
    }

    public class GoShipRateApiRequest
    {
        public Services.GlassesService.GoShipAddress AddressFrom { get; set; } = new();
        public Services.GlassesService.GoShipAddress AddressTo { get; set; } = new();
        public Services.GlassesService.GoShipParcel Parcel { get; set; } = new();
    }

    public class CreateGoShipShipmentApiRequest
    {
        public string RateId { get; set; } = string.Empty;
        public Guid? OrderId { get; set; }
        public Services.GlassesService.GoShipAddress AddressFrom { get; set; } = new();
        public Services.GlassesService.GoShipAddress AddressTo { get; set; } = new();
        public Services.GlassesService.GoShipParcel Parcel { get; set; } = new();
    }

    #endregion
}
