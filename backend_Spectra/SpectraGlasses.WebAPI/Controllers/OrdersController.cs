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
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IShippingService _shippingService;

        public OrdersController(IOrderService orderService, IShippingService shippingService)
        {
            _orderService = orderService;
            _shippingService = shippingService;
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
        /// Creates a new order (Customer only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
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

            if (string.IsNullOrWhiteSpace(request.ShippingAddress))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Shipping address is required"
                });
            }

            if (request.Items == null || !request.Items.Any())
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Order must contain at least one item"
                });
            }

            // Convert request to order items
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

            // Validate order items
            var validationResult = await _orderService.ValidateOrderItemsAsync(orderItems, userId);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = string.Join("; ", validationResult.Errors)
                });
            }

            // Create order
            var order = new Order
            {
                UserId = userId,
                ShippingAddress = request.ShippingAddress,
                ShippingMethod = request.ShippingMethod ?? "standard"
            };

            var createdOrder = await _orderService.CreateOrderAsync(order, orderItems);

            // Calculate and apply shipping fee, then persist
            var shippingFee = _shippingService.CalculateShippingFee(
                createdOrder.ShippingMethod, createdOrder.TotalAmount ?? 0);
            createdOrder.ShippingFee = shippingFee;
            createdOrder.TotalAmount = (createdOrder.TotalAmount ?? 0) + shippingFee;
            await _orderService.UpdateOrderShippingAsync(createdOrder.OrderId, createdOrder.ShippingMethod, shippingFee, createdOrder.TotalAmount ?? 0);

            var summary = new OrderSummaryResponse
            {
                OrderId = createdOrder.OrderId,
                UserId = createdOrder.UserId,
                TotalAmount = createdOrder.TotalAmount,
                ShippingAddress = createdOrder.ShippingAddress,
                Status = createdOrder.Status,
                CreatedAt = createdOrder.CreatedAt,
                ItemCount = createdOrder.OrderItems?.Count ?? 0,
                ConvertedFromPreorderId = createdOrder.ConvertedFromPreorderId
            };

            return CreatedAtAction(nameof(GetOrderById), new { id = createdOrder.OrderId }, summary);
        }

        /// <summary>
        /// Gets current user's orders
        /// </summary>
        [HttpGet("my")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
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

            var result = await _orderService.GetOrdersByUserAsync(userId, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Gets a specific order by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOrderById(Guid id)
        {
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            var order = await _orderService.GetOrderByIdWithDetailsAsync(id);

            if (order == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "ORDER_NOT_FOUND",
                    Message = "Order not found"
                });
            }

            // Customers can only view their own orders
            if (userRole.ToLower() == "customer" && order.UserId != userId)
            {
                return Forbid();
            }

            return Ok(order);
        }

        /// <summary>
        /// Customer confirms they have received the delivered order
        /// </summary>
        [HttpPut("{id:guid}/confirm-delivery")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ConfirmDelivery(Guid id)
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

            var result = await _orderService.ConfirmDeliveryAsync(id, userId);

            if (result == null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "CONFIRM_FAILED",
                    Message = "Order not found, not delivered, or you are not the owner"
                });
            }

            return Ok(new { message = "Delivery confirmed", deliveryConfirmedAt = result.DeliveryConfirmedAt });
        }

        /// <summary>
        /// Customer cancels their own pending order
        /// </summary>
        [HttpPut("{id:guid}/cancel")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CustomerCancelOrder(Guid id)
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

            // Get the order to verify ownership and status
            var order = await _orderService.GetOrderByIdAsync(id);

            if (order == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "ORDER_NOT_FOUND",
                    Message = "Order not found"
                });
            }

            // Verify the order belongs to this customer
            if (order.UserId != userId)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "NOT_OWNER",
                    Message = "You can only cancel your own orders"
                });
            }

            // Only allow cancellation of pending orders
            if (order.Status?.ToLower() != "pending")
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "INVALID_STATUS",
                    Message = "Only pending orders can be cancelled. Please contact support for orders that have been confirmed."
                });
            }

            // Update order status to cancelled
            var result = await _orderService.CancelOrderByCustomerAsync(id, userId);

            if (result == null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "CANCEL_FAILED",
                    Message = "Failed to cancel order. Please try again or contact support."
                });
            }

            return Ok(new { message = "Order cancelled successfully", order = result });
        }

        #endregion

        #region Staff/Manager Endpoints

        /// <summary>
        /// Gets all orders (Staff/Manager/Admin)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _orderService.GetAllOrdersAsync(page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Updates order status (Staff/Manager/Admin)
        /// </summary>
        [HttpPut("{id:guid}/status")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            if (string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Status is required"
                });
            }

            var validStatuses = new[] { "pending", "confirmed", "processing", "shipped", "delivered", "cancelled" };
            if (!validStatuses.Contains(request.Status.ToLower()))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = $"Invalid status. Allowed values: {string.Join(", ", validStatuses)}"
                });
            }

            var result = await _orderService.UpdateOrderStatusAsync(id, request.Status, userRole, userId);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "UPDATE_FAILED",
                    Message = "Order not found or status transition not allowed for your role"
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Cancels an order (Manager/Admin only)
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CancelOrder(Guid id)
        {
            var userRole = GetCurrentUserRole();
            var userId = GetCurrentUserId();

            var result = await _orderService.UpdateOrderStatusAsync(id, "cancelled", userRole, userId);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "CANCEL_FAILED",
                    Message = "Order not found or cannot be cancelled"
                });
            }

            return Ok(result);
        }

        #endregion
    }
}
