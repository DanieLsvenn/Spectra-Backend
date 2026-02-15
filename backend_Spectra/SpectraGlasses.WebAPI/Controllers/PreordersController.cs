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
    public class PreordersController : ControllerBase
    {
        private readonly IPreorderService _preorderService;

        public PreordersController(IPreorderService preorderService)
        {
            _preorderService = preorderService;
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

        #region Customer Endpoints

        /// <summary>
        /// Creates a new preorder (Customer only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreatePreorder([FromBody] CreatePreorderRequest request)
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

            if (request.Items == null || !request.Items.Any())
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Preorder must contain at least one item"
                });
            }

            // Convert request to preorder items
            var preorderItems = request.Items.Select(item => new PreorderItem
            {
                FrameId = item.FrameId,
                LensTypeId = item.LensTypeId,
                FeatureId = item.FeatureId,
                PrescriptionId = item.PrescriptionId,
                Quantity = item.Quantity,
                SelectedColor = item.SelectedColor
            }).ToList();

            // Validate preorder items
            var validationResult = await _preorderService.ValidatePreorderItemsAsync(preorderItems, userId);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = string.Join("; ", validationResult.Errors)
                });
            }

            // Create preorder
            var preorder = new Preorder
            {
                UserId = userId,
                ExpectedDate = request.ExpectedDate
            };

            var createdPreorder = await _preorderService.CreatePreorderAsync(preorder, preorderItems);

            return CreatedAtAction(nameof(GetPreorderById), new { id = createdPreorder.PreorderId }, createdPreorder);
        }

        /// <summary>
        /// Gets current user's preorders
        /// </summary>
        [HttpGet("my")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyPreorders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
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

            var result = await _preorderService.GetPreordersByUserAsync(userId, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Gets a specific preorder by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPreorderById(Guid id)
        {
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            var preorder = await _preorderService.GetPreorderByIdWithDetailsAsync(id);

            if (preorder == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "PREORDER_NOT_FOUND",
                    Message = "Preorder not found"
                });
            }

            // Customers can only view their own preorders
            if (userRole.ToLower() == "customer" && preorder.UserId != userId)
            {
                return Forbid();
            }

            return Ok(preorder);
        }

        /// <summary>
        /// Cancels a preorder (Customer - only if not paid)
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CancelPreorder(Guid id)
        {
            var userId = GetCurrentUserId();

            var preorder = await _preorderService.GetPreorderByIdAsync(id);

            if (preorder == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "PREORDER_NOT_FOUND",
                    Message = "Preorder not found"
                });
            }

            // Verify ownership
            if (preorder.UserId != userId)
            {
                return Forbid();
            }

            var result = await _preorderService.CancelPreorderAsync(id);

            if (!result)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "CANCEL_FAILED",
                    Message = "Cannot cancel preorder. It may have already been paid."
                });
            }

            return NoContent();
        }

        #endregion

        #region Staff/Manager Endpoints

        /// <summary>
        /// Gets all preorders (Staff/Manager/Admin)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllPreorders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _preorderService.GetAllPreordersAsync(page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Updates preorder status (Staff/Manager/Admin)
        /// </summary>
        [HttpPut("{id:guid}/status")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdatePreorderStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
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

            var validStatuses = new[] { "pending", "confirmed", "paid", "converted", "cancelled" };
            if (!validStatuses.Contains(request.Status.ToLower()))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = $"Invalid status. Allowed values: {string.Join(", ", validStatuses)}"
                });
            }

            var result = await _preorderService.UpdatePreorderStatusAsync(id, request.Status, userRole);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "UPDATE_FAILED",
                    Message = "Preorder not found or status update not allowed"
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Converts a preorder to an order (Staff/Manager/Admin)
        /// </summary>
        [HttpPost("{id:guid}/convert")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ConvertToOrder(Guid id, [FromBody] ConvertPreorderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ShippingAddress))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Shipping address is required"
                });
            }

            var canConvert = await _preorderService.CanConvertToOrderAsync(id);

            if (!canConvert)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "CONVERSION_FAILED",
                    Message = "Preorder cannot be converted. It must be in 'paid' or 'confirmed' status."
                });
            }

            var order = await _preorderService.ConvertPreorderToOrderAsync(id, request.ShippingAddress);

            if (order == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "CONVERSION_FAILED",
                    Message = "Failed to convert preorder to order"
                });
            }

            return Ok(order);
        }

        #endregion
    }
}
