using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repositories.Basic;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;
using System.Security.Claims;

namespace SpectraGlasses.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PingController : ControllerBase
    {
        private const string BuildTag = "2026-04-01-v10-enrich-detach";

        private readonly GenericRepository<Order> _orderRepo;
        private readonly GenericRepository<OrderItem> _orderItemRepo;
        private readonly IOrderService _orderService;
        private readonly IShippingService _shippingService;

        public PingController(
            GenericRepository<Order> orderRepo,
            GenericRepository<OrderItem> orderItemRepo,
            IOrderService orderService,
            IShippingService shippingService)
        {
            _orderRepo = orderRepo;
            _orderItemRepo = orderItemRepo;
            _orderService = orderService;
            _shippingService = shippingService;
        }

        [HttpGet]
        public IActionResult Ping()
        {
            return Ok(new
            {
                message  = "pong",
                buildTag = BuildTag,
                serverTime = DateTime.UtcNow.ToString("o")
            });
        }

        /// <summary>
        /// Diagnostic: Raw insert of Order + OrderItem, then reload from DB.
        /// Returns step-by-step values so we can see exactly where data is lost.
        /// </summary>
        [HttpGet("diagnostic-insert")]
        [Authorize(Roles = "customer")]
        public async Task<IActionResult> DiagnosticInsert()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("No userId in JWT");

            var orderId = Guid.NewGuid();
            var itemId  = Guid.NewGuid();

            // Build a plain Order object
            var order = new Order
            {
                OrderId         = orderId,
                UserId          = userId,
                Status          = "diagnostic",
                CreatedAt       = DateTime.UtcNow,
                TotalAmount     = 0.01,
                ShippingAddress = "diagnostic-test",
                ShippingMethod  = "standard"
            };

            // Capture values BEFORE insert
            var step1_userId  = order.UserId;
            var step1_orderId = order.OrderId;

            // Clear tracker & insert
            _orderRepo.ClearTracker();
            var created = await _orderRepo.CreateAsync(order);

            // Capture values AFTER insert (same object reference)
            var step2_userId  = created.UserId;
            var step2_orderId = created.OrderId;

            // Now insert an OrderItem
            var item = new OrderItem
            {
                OrderItemId     = itemId,
                OrderId         = created.OrderId,
                FrameId         = Guid.Parse("86b0388d-53bf-4f9b-8b02-6ce2094b0607"),
                SelectedColorId = Guid.Parse("5a8fb58b-54e3-44f0-80fb-9f4f84f707d0"),
                Quantity        = 1,
                UnitPrice       = 99.99
            };

            var step3_itemOrderId = item.OrderId;
            _orderItemRepo.ClearTracker();
            var createdItem = await _orderItemRepo.CreateAsync(item);
            var step4_itemOrderId = createdItem.OrderId;

            // Reload order from DB (fresh query)
            _orderRepo.ClearTracker();
            var reloaded = (await _orderRepo.SearchAsyncInclude(
                o => o.OrderId == orderId,
                o => o.OrderItems)).FirstOrDefault();

            return Ok(new
            {
                buildTag          = BuildTag,
                jwtUserId         = userId,
                step1_beforeInsert = new { orderId = step1_orderId, userId = step1_userId },
                step2_afterInsert  = new { orderId = step2_orderId, userId = step2_userId },
                step3_itemBeforeInsert = new { orderItemId = itemId, orderId = step3_itemOrderId },
                step4_itemAfterInsert  = new { orderItemId = createdItem.OrderItemId, orderId = step4_itemOrderId },
                step5_reloadedFromDb = new
                {
                    orderId    = reloaded?.OrderId,
                    userId     = reloaded?.UserId,
                    status     = reloaded?.Status,
                    itemCount  = reloaded?.OrderItems?.Count ?? -1,
                    firstItemId = reloaded?.OrderItems?.FirstOrDefault()?.OrderItemId
                }
            });
        }

        /// <summary>
        /// Diagnostic: Calls the REAL OrderService.ValidateOrderItemsAsync + CreateOrderAsync,
        /// the exact same code path as POST /api/Orders, with step-by-step logging.
        /// </summary>
        [HttpGet("diagnostic-fullflow")]
        [Authorize(Roles = "customer")]
        public async Task<IActionResult> DiagnosticFullFlow()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("No userId in JWT");

            var steps = new List<object>();

            // Step 1: Build order items (same as controller does)
            var orderItems = new List<OrderItem>
            {
                new OrderItem
                {
                    FrameId         = Guid.Parse("86b0388d-53bf-4f9b-8b02-6ce2094b0607"),
                    SelectedColorId = Guid.Parse("5a8fb58b-54e3-44f0-80fb-9f4f84f707d0"),
                    Quantity        = 1
                }
            };
            steps.Add(new { step = "1_items_built", itemCount = orderItems.Count });

            // Step 2: Validate (same as controller does)
            var validation = await _orderService.ValidateOrderItemsAsync(orderItems, userId);
            steps.Add(new { step = "2_validated", isValid = validation.IsValid, errors = validation.Errors });

            if (!validation.IsValid)
                return Ok(new { buildTag = BuildTag, result = "VALIDATION_FAILED", steps });

            // Step 3: Build the Order object (same as controller does)
            var order = new Order
            {
                UserId          = userId,
                ShippingAddress = "diag-fullflow-test",
                ShippingMethod  = "standard",
                Notes           = "v6 fullflow diagnostic"
            };

            steps.Add(new { step = "3_order_built", userId = order.UserId, userIsNull = order.User == null });

            // Step 4: Call CreateOrderAsync (the REAL service method)
            Order createdOrder;
            try
            {
                createdOrder = await _orderService.CreateOrderAsync(order, orderItems);
            }
            catch (Exception ex)
            {
                steps.Add(new { step = "4_EXCEPTION", message = ex.Message, inner = ex.InnerException?.Message, stack = ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace.Length)) });
                return Ok(new { buildTag = BuildTag, result = "EXCEPTION", steps });
            }

            steps.Add(new
            {
                step       = "4_created",
                orderId    = createdOrder.OrderId,
                userId     = createdOrder.UserId,
                totalAmount = createdOrder.TotalAmount,
                status     = createdOrder.Status,
                itemCount  = createdOrder.OrderItems?.Count ?? -1
            });

            // Step 5: Reload independently to confirm DB state
            _orderRepo.ClearTracker();
            var reloaded = (await _orderRepo.SearchAsyncInclude(
                o => o.OrderId == createdOrder.OrderId,
                o => o.OrderItems,
                o => o.User)).FirstOrDefault();

            steps.Add(new
            {
                step        = "5_reloaded_from_db",
                orderId     = reloaded?.OrderId,
                userId      = reloaded?.UserId,
                status      = reloaded?.Status,
                itemCount   = reloaded?.OrderItems?.Count ?? -1,
                userName    = reloaded?.User?.FullName
            });

            // Step 6: Exactly replicate what OrdersController does AFTER CreateOrderAsync
            var shippingFee = _shippingService.CalculateShippingFee(
                createdOrder.ShippingMethod, createdOrder.TotalAmount ?? 0, createdOrder.ShippingAddress);
            createdOrder.ShippingFee = shippingFee;
            createdOrder.TotalAmount = (createdOrder.TotalAmount ?? 0) + shippingFee;
            await _orderService.UpdateOrderShippingAsync(
                createdOrder.OrderId, createdOrder.ShippingMethod, shippingFee, createdOrder.TotalAmount ?? 0);

            steps.Add(new
            {
                step             = "6_after_shipping_update",
                shippingFee      = shippingFee,
                totalWithShip    = createdOrder.TotalAmount,
                createdOrderUserId = createdOrder.UserId,
                createdOrderItems  = createdOrder.OrderItems?.Count ?? -1
            });

            // Step 7: FINAL reload from DB to see if shipping update destroyed data
            _orderRepo.ClearTracker();
            var finalReload = (await _orderRepo.SearchAsyncInclude(
                o => o.OrderId == createdOrder.OrderId,
                o => o.OrderItems,
                o => o.User)).FirstOrDefault();

            steps.Add(new
            {
                step        = "7_final_db_state",
                orderId     = finalReload?.OrderId,
                userId      = finalReload?.UserId,
                status      = finalReload?.Status,
                itemCount   = finalReload?.OrderItems?.Count ?? -1,
                totalAmount = finalReload?.TotalAmount,
                shippingFee = finalReload?.ShippingFee,
                userName    = finalReload?.User?.FullName
            });

            return Ok(new { buildTag = BuildTag, result = "OK", steps });
        }

        /// <summary>
        /// Diagnostic POST: Exact same code as OrdersV2Controller.CreateOrder
        /// to isolate whether POST body parsing causes the issue.
        /// </summary>
        [HttpPost("diagnostic-post")]
        [Authorize(Roles = "customer")]
        public async Task<IActionResult> DiagnosticPost([FromBody] CreateOrderRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("No userId in JWT");

            var steps = new List<object>();
            steps.Add(new { step = "0_jwt", userId });

            // === EXACT COPY of OrdersV2Controller.CreateOrder ===
            if (string.IsNullOrWhiteSpace(request.ShippingAddress))
                return BadRequest("ShippingAddress required");

            if (request.Items == null || !request.Items.Any())
                return BadRequest("Items required");

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

            steps.Add(new { step = "1_items_mapped", count = orderItems.Count, 
                firstFrameId = orderItems.First().FrameId, 
                firstColorId = orderItems.First().SelectedColorId });

            var validationResult = await _orderService.ValidateOrderItemsAsync(orderItems, userId);
            steps.Add(new { step = "2_validated", isValid = validationResult.IsValid, errors = validationResult.Errors });

            if (!validationResult.IsValid)
                return Ok(new { buildTag = BuildTag, result = "VALIDATION_FAILED", steps });

            var order = new Order
            {
                UserId = userId,
                ShippingAddress = request.ShippingAddress,
                ShippingMethod = request.ShippingMethod ?? "standard",
                Notes = request.Notes
            };

            steps.Add(new { step = "3_order_built", userId = order.UserId, shippingAddr = order.ShippingAddress });

            Order createdOrder;
            try
            {
                createdOrder = await _orderService.CreateOrderAsync(order, orderItems);
            }
            catch (Exception ex)
            {
                steps.Add(new { step = "4_EXCEPTION", message = ex.Message, inner = ex.InnerException?.Message });
                return Ok(new { buildTag = BuildTag, result = "EXCEPTION", steps });
            }

            steps.Add(new
            {
                step = "4_created",
                orderId = createdOrder.OrderId,
                userId = createdOrder.UserId,
                totalAmount = createdOrder.TotalAmount,
                status = createdOrder.Status,
                itemCount = createdOrder.OrderItems?.Count ?? -1
            });

            // Shipping fee (same as OrdersV2)
            var shippingFee = _shippingService.CalculateShippingFee(
                createdOrder.ShippingMethod, createdOrder.TotalAmount ?? 0, createdOrder.ShippingAddress);
            createdOrder.ShippingFee = shippingFee;
            createdOrder.TotalAmount = (createdOrder.TotalAmount ?? 0) + shippingFee;
            await _orderService.UpdateOrderShippingAsync(
                createdOrder.OrderId, createdOrder.ShippingMethod, shippingFee, createdOrder.TotalAmount ?? 0);

            steps.Add(new { step = "5_shipping", shippingFee, total = createdOrder.TotalAmount });

            // Independent reload
            _orderRepo.ClearTracker();
            var reloaded = (await _orderRepo.SearchAsyncInclude(
                o => o.OrderId == createdOrder.OrderId,
                o => o.OrderItems,
                o => o.User)).FirstOrDefault();

            steps.Add(new
            {
                step = "6_reloaded",
                orderId = reloaded?.OrderId,
                userId = reloaded?.UserId,
                itemCount = reloaded?.OrderItems?.Count ?? -1,
                userName = reloaded?.User?.FullName
            });

            return Ok(new { buildTag = BuildTag, result = "OK", steps });
        }
    }
}
