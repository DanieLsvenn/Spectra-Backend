using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ComplaintsController : ControllerBase
    {
        private readonly IComplaintRequestService _complaintService;
        private readonly IOrderService _orderService;
        private readonly ICloudinaryService _cloudinaryService;

        public ComplaintsController(
            IComplaintRequestService complaintService,
            IOrderService orderService,
            ICloudinaryService cloudinaryService)
        {
            _complaintService = complaintService;
            _orderService = orderService;
            _cloudinaryService = cloudinaryService;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        private ComplaintResponse MapToResponse(ComplaintRequest complaint)
        {
            var response = new ComplaintResponse
            {
                RequestId = complaint.RequestId,
                UserId = complaint.UserId,
                UserName = complaint.User?.FullName,
                UserPhone = complaint.User?.Phone,
                OrderItemId = complaint.OrderItemId,
                RequestType = complaint.RequestType,
                Reason = complaint.Reason,
                MediaUrl = complaint.MediaUrl,
                Status = complaint.Status,
                CreatedAt = complaint.CreatedAt,
                CanModify = _complaintService.CanCustomerModify(complaint),
                ExchangeOrderId = complaint.ExchangeOrderId,
                StaffNote = complaint.StaffNote,
                RefundAmount = complaint.RefundAmount,
                ReturnTrackingNumber = complaint.ReturnTrackingNumber,
                ReturnShippingCarrier = complaint.ReturnShippingCarrier,
                RefundedAt = complaint.RefundedAt,
                CancelledByCustomer = complaint.CancelledByCustomer
            };

            if (complaint.OrderItem != null)
            {
                response.OriginalItem = new OriginalOrderItemInfo
                {
                    OrderItemId = complaint.OrderItem.OrderItemId,
                    FrameId = complaint.OrderItem.FrameId,
                    FrameName = complaint.OrderItem.Frame?.FrameName,
                    UnitPrice = complaint.OrderItem.UnitPrice,
                    Quantity = complaint.OrderItem.Quantity,
                    SelectedSize = complaint.OrderItem.SelectedSize
                };
            }

            return response;
        }

        #region Customer Endpoints

        /// <summary>
        /// Submits a new complaint/return request
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(typeof(ComplaintResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintRequest request)
        {
            var userId = GetCurrentUserId();

            if (userId == Guid.Empty)
            {
                return Unauthorized(new ErrorResponse
                {
                    ErrorCode = "UNAUTHORIZED",
                    Message = "User not authenticated"
                });
            }

            // Validate request type
            if (!_complaintService.IsValidRequestType(request.RequestType))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid request type. Allowed: return, exchange, refund, complaint, warranty"
                });
            }

            // Validate reason
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Reason is required"
                });
            }

            // Verify order item exists and belongs to user's delivered order
            var (isValid, error) = await _complaintService.ValidateOrderItemOwnershipAsync(request.OrderItemId, userId);
            if (!isValid)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "OWNERSHIP_ERROR",
                    Message = error ?? "Order item validation failed"
                });
            }

            var complaint = new ComplaintRequest
            {
                UserId = userId,
                OrderItemId = request.OrderItemId,
                RequestType = request.RequestType.ToLower(),
                Reason = request.Reason,
                MediaUrl = request.MediaUrl
            };

            var createdComplaint = await _complaintService.CreateComplaintAsync(complaint);

            return CreatedAtAction(
                nameof(GetComplaintById),
                new { id = createdComplaint.RequestId },
                MapToResponse(createdComplaint)
            );
        }

        /// <summary>
        /// Uploads an image for a complaint to Cloudinary (Customer only)
        /// Returns the URL that can be used as mediaUrl when creating/updating a complaint
        /// </summary>
        [HttpPost("upload-image")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UploadComplaintImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "No file provided"
                });
            }

            const long maxFileSize = 10 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "File size exceeds maximum allowed size of 10MB"
                });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid file type. Allowed: jpg, jpeg, png, gif, webp"
                });
            }

            using var stream = file.OpenReadStream();
            var uploadResult = await _cloudinaryService.UploadImageAsync(stream, file.FileName, "spectra/complaints");

            if (!uploadResult.Success)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "UPLOAD_ERROR",
                    Message = uploadResult.Error
                });
            }

            return Ok(new ImageUploadResponse
            {
                Success = true,
                Url = uploadResult.Url,
                PublicId = uploadResult.PublicId
            });
        }

        /// <summary>
        /// Gets all complaints for the current user
        /// </summary>
        [HttpGet("my")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyComplaints([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userId = GetCurrentUserId();

            if (userId == Guid.Empty)
            {
                return Unauthorized(new ErrorResponse
                {
                    ErrorCode = "UNAUTHORIZED",
                    Message = "User not authenticated"
                });
            }

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _complaintService.GetComplaintsByUserAsync(userId, page, pageSize);

            var responseItems = result.Items.Select(MapToResponse).ToList();

            return Ok(new
            {
                result.TotalItems,
                result.TotalPages,
                result.CurrentPage,
                result.PageSize,
                Items = responseItems
            });
        }

        /// <summary>
        /// Gets a specific complaint by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize]
        [ProducesResponseType(typeof(ComplaintResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetComplaintById(Guid id)
        {
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            var complaint = await _complaintService.GetComplaintByIdWithDetailsAsync(id);

            if (complaint == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "COMPLAINT_NOT_FOUND",
                    Message = "Complaint not found"
                });
            }

            // Customers can only view their own complaints
            if (userRole.ToLower() == "customer" && complaint.UserId != userId)
            {
                return Forbid();
            }

            return Ok(MapToResponse(complaint));
        }

        /// <summary>
        /// Updates a complaint (only if pending)
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(typeof(ComplaintResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateComplaint(Guid id, [FromBody] UpdateComplaintRequest request)
        {
            var userId = GetCurrentUserId();

            if (userId == Guid.Empty)
            {
                return Unauthorized(new ErrorResponse
                {
                    ErrorCode = "UNAUTHORIZED",
                    Message = "User not authenticated"
                });
            }

            // Validate request type if provided
            if (!string.IsNullOrEmpty(request.RequestType) && !_complaintService.IsValidRequestType(request.RequestType))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid request type"
                });
            }

            var updatedComplaint = new ComplaintRequest
            {
                RequestType = request.RequestType,
                Reason = request.Reason,
                MediaUrl = request.MediaUrl
            };

            var result = await _complaintService.UpdateComplaintAsync(id, updatedComplaint, userId);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "UPDATE_FAILED",
                    Message = "Complaint not found, you don't have permission, or it can no longer be modified"
                });
            }

            return Ok(MapToResponse(result));
        }

        /// <summary>
        /// Customer cancels/withdraws their own complaint
        /// </summary>
        [HttpPut("{id:guid}/cancel")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(typeof(ComplaintResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CancelComplaintByCustomer(Guid id)
        {
            var userId = GetCurrentUserId();

            if (userId == Guid.Empty)
            {
                return Unauthorized(new ErrorResponse
                {
                    ErrorCode = "UNAUTHORIZED",
                    Message = "User not authenticated"
                });
            }

            var result = await _complaintService.CancelComplaintByCustomerAsync(id, userId);

            if (result == null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "CANCEL_FAILED",
                    Message = "Complaint not found, you are not the owner, or it cannot be cancelled at this stage"
                });
            }

            return Ok(MapToResponse(result));
        }

        #endregion

        #region Staff/Manager Endpoints

        /// <summary>
        /// Gets all complaints (Staff/Manager/Admin)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllComplaints([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _complaintService.GetAllComplaintsAsync(page, pageSize);

            var responseItems = result.Items.Select(MapToResponse).ToList();

            return Ok(new
            {
                result.TotalItems,
                result.TotalPages,
                result.CurrentPage,
                result.PageSize,
                Items = responseItems
            });
        }

        /// <summary>
        /// Gets complaints by status (Staff/Manager/Admin)
        /// </summary>
        [HttpGet("status/{status}")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetComplaintsByStatus(string status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (!_complaintService.IsValidStatus(status))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid status. Allowed: pending, under_review, approved, rejected, in_progress, resolved, cancelled"
                });
            }

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _complaintService.GetComplaintsByStatusAsync(status, page, pageSize);

            var responseItems = result.Items.Select(MapToResponse).ToList();

            return Ok(new
            {
                result.TotalItems,
                result.TotalPages,
                result.CurrentPage,
                result.PageSize,
                Items = responseItems
            });
        }

        /// <summary>
        /// Updates complaint status (Staff/Manager/Admin).
        /// Follows strict workflow: pending ? under_review ? approved/rejected ? in_progress ? resolved.
        /// </summary>
        [HttpPut("{id:guid}/status")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(typeof(ComplaintResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateComplaintStatus(Guid id, [FromBody] UpdateComplaintStatusRequest request)
        {
            var userRole = GetCurrentUserRole();

            if (string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Status is required"
                });
            }

            if (!_complaintService.IsValidStatus(request.Status))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid status. Allowed: pending, under_review, approved, rejected, in_progress, resolved, cancelled"
                });
            }

            // Check if the transition is valid before attempting update
            var existing = await _complaintService.GetComplaintByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "COMPLAINT_NOT_FOUND",
                    Message = "Complaint not found"
                });
            }

            var currentStatus = existing.Status?.ToLower() ?? "pending";
            if (!_complaintService.IsValidStatusTransition(currentStatus, request.Status.ToLower()))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "INVALID_TRANSITION",
                    Message = $"Cannot transition from '{currentStatus}' to '{request.Status}'. Check the allowed workflow transitions."
                });
            }

            // Block resolving exchange complaints if exchange order is not yet delivered
            if (request.Status.ToLower() == "resolved"
                && existing.RequestType?.ToLower() == "exchange"
                && existing.ExchangeOrderId.HasValue)
            {
                var detailedComplaint = await _complaintService.GetComplaintByIdWithDetailsAsync(id);
                var exchangeOrder = detailedComplaint?.ExchangeOrder;
                if (exchangeOrder == null || exchangeOrder.Status?.ToLower() != "delivered")
                {
                    return BadRequest(new ErrorResponse
                    {
                        ErrorCode = "EXCHANGE_NOT_DELIVERED",
                        Message = "Cannot resolve this exchange complaint until the replacement order has been delivered to the customer."
                    });
                }
            }

            var result = await _complaintService.UpdateComplaintStatusAsync(id, request.Status, userRole, request.StaffNote);

            if (result == null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "UPDATE_FAILED",
                    Message = "Failed to update complaint status"
                });
            }

            return Ok(MapToResponse(result));
        }

        /// <summary>
        /// Links an exchange complaint to a replacement order (Staff/Manager/Admin)
        /// </summary>
        [HttpPut("{id:guid}/exchange-order")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(typeof(ComplaintResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> LinkExchangeOrder(Guid id, [FromBody] LinkExchangeOrderRequest request)
        {
            if (request.ExchangeOrderId == Guid.Empty)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "ExchangeOrderId is required"
                });
            }

            var result = await _complaintService.LinkExchangeOrderAsync(id, request.ExchangeOrderId);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "LINK_FAILED",
                    Message = "Complaint not found, is not an exchange type, or the exchange order does not exist"
                });
            }

            return Ok(MapToResponse(result));
        }

        /// <summary>
        /// Processes a refund for return/refund complaints (Staff/Manager/Admin)
        /// </summary>
        [HttpPut("{id:guid}/process-refund")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(typeof(ComplaintResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ProcessRefund(Guid id, [FromBody] ProcessRefundRequest request)
        {
            if (request.RefundAmount <= 0)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Refund amount must be greater than 0"
                });
            }

            var result = await _complaintService.ProcessRefundAsync(id, request.RefundAmount);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "REFUND_FAILED",
                    Message = "Complaint not found, is not a return/refund type, or is not in an approved/in_progress status"
                });
            }

            return Ok(MapToResponse(result));
        }

        /// <summary>
        /// Sets the return tracking number for return/exchange/warranty complaints (Staff/Manager/Admin)
        /// </summary>
        [HttpPut("{id:guid}/return-tracking")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(typeof(ComplaintResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetReturnTracking(Guid id, [FromBody] SetReturnTrackingRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TrackingNumber))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Tracking number is required"
                });
            }

            var result = await _complaintService.SetReturnTrackingAsync(id, request.TrackingNumber);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "TRACKING_FAILED",
                    Message = "Complaint not found or is not a return/exchange/warranty type"
                });
            }

            return Ok(MapToResponse(result));
        }

        /// <summary>
        /// Creates an exchange order linked to an approved exchange complaint (Customer)
        /// </summary>
        [HttpPost("{id:guid}/create-exchange-order")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateExchangeOrder(Guid id, [FromBody] CreateExchangeOrderFromComplaintRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new ErrorResponse { ErrorCode = "UNAUTHORIZED", Message = "User not authenticated" });
            }

            // Load complaint with details
            var complaint = await _complaintService.GetComplaintByIdWithDetailsAsync(id);
            if (complaint == null || complaint.UserId != userId)
            {
                return NotFound(new ErrorResponse { ErrorCode = "NOT_FOUND", Message = "Complaint not found" });
            }

            if (complaint.RequestType?.ToLower() != "exchange")
            {
                return BadRequest(new ErrorResponse { ErrorCode = "INVALID_TYPE", Message = "Only exchange complaints can create exchange orders" });
            }

            var allowedStatuses = new[] { "approved", "in_progress" };
            if (!allowedStatuses.Contains(complaint.Status?.ToLower()))
            {
                return BadRequest(new ErrorResponse { ErrorCode = "INVALID_STATUS", Message = "Complaint must be approved or in progress to create an exchange order" });
            }

            if (complaint.ExchangeOrderId.HasValue)
            {
                return BadRequest(new ErrorResponse { ErrorCode = "ALREADY_LINKED", Message = "An exchange order already exists for this complaint" });
            }

            if (string.IsNullOrWhiteSpace(request.ShippingAddress) || request.Items == null || !request.Items.Any())
            {
                return BadRequest(new ErrorResponse { ErrorCode = "VALIDATION_ERROR", Message = "Shipping address and at least one item are required" });
            }

            var orderItems = request.Items.Select(item => new OrderItem
            {
                FrameId = item.FrameId,
                LensTypeId = item.LensTypeId,
                FeatureId = item.FeatureId,
                LensIndexId = item.LensIndexId,
                PrescriptionId = item.PrescriptionId,
                Quantity = item.Quantity,
                SelectedColorId = item.SelectedColorId,
                SelectedSize = item.SelectedSize
            }).ToList();

            var validationResult = await _orderService.ValidateOrderItemsAsync(orderItems, userId);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorResponse { ErrorCode = "VALIDATION_ERROR", Message = string.Join("; ", validationResult.Errors) });
            }

            var order = new Order
            {
                UserId = userId,
                ShippingAddress = request.ShippingAddress,
                ShippingMethod = "standard"
            };

            var createdOrder = await _orderService.CreateOrderAsync(order, orderItems);

            // Link the exchange order to the complaint
            await _complaintService.LinkExchangeOrderAsync(id, createdOrder.OrderId);

            // If not already in_progress, move to in_progress
            if (complaint.Status?.ToLower() == "approved")
            {
                await _complaintService.UpdateComplaintStatusAsync(id, "in_progress", "system", "Exchange order created by customer");
            }

            return CreatedAtAction(nameof(GetComplaintById), new { id = id }, new
            {
                ComplaintId = id,
                ExchangeOrderId = createdOrder.OrderId,
                ExchangeOrderTotal = createdOrder.TotalAmount,
                OriginalItemPrice = complaint.OrderItem?.UnitPrice,
                Message = "Exchange order created and linked to complaint"
            });
        }

        #endregion
    }
}
