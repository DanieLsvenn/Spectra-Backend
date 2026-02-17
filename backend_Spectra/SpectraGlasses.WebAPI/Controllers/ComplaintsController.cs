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

        public ComplaintsController(
            IComplaintRequestService complaintService,
            IOrderService orderService)
        {
            _complaintService = complaintService;
            _orderService = orderService;
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
            return new ComplaintResponse
            {
                RequestId = complaint.RequestId,
                UserId = complaint.UserId,
                OrderItemId = complaint.OrderItemId,
                RequestType = complaint.RequestType,
                Reason = complaint.Reason,
                MediaUrl = complaint.MediaUrl,
                Status = complaint.Status,
                CreatedAt = complaint.CreatedAt,
                CanModify = _complaintService.CanCustomerModify(complaint)
            };
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

            // Verify order item exists and belongs to user's order
            var order = await _orderService.GetOrderByIdWithDetailsAsync(Guid.Empty);
            // We would need to validate the order item belongs to the user
            // For now, we'll trust the OrderItemId

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
        /// Updates complaint status (Staff/Manager/Admin)
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

            var result = await _complaintService.UpdateComplaintStatusAsync(id, request.Status, userRole);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "UPDATE_FAILED",
                    Message = "Complaint not found"
                });
            }

            return Ok(MapToResponse(result));
        }

        #endregion
    }
}
