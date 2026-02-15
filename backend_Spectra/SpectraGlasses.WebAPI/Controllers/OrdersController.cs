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

        public OrdersController(IOrderService orderService)
        {
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
                PrescriptionId = item.PrescriptionId,
                Quantity = item.Quantity,
                SelectedColor = item.SelectedColor
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
                ShippingAddress = request.ShippingAddress
            };

            var createdOrder = await _orderService.CreateOrderAsync(order, orderItems);

            var summary = new OrderSummaryResponse
            {
                OrderId = createdOrder.OrderId,
                UserId = createdOrder.UserId,
                TotalAmount = createdOrder.TotalAmount,
                ShippingAddress = createdOrder.ShippingAddress,
                Status = createdOrder.Status,
                CreatedAt = createdOrder.CreatedAt,
                ItemCount = createdOrder.OrderItems?.Count ?? 0
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
