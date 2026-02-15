using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _configuration;

        public PaymentsController(IPaymentService paymentService, IConfiguration configuration)
        {
            _paymentService = paymentService;
            _configuration = configuration;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        private string GetClientIpAddress()
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1")
            {
                ipAddress = "127.0.0.1";
            }
            return ipAddress;
        }

        #region Customer Endpoints

        /// <summary>
        /// Creates a new payment and returns VNPay payment URL
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
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

            // Validate only one foreign key is provided
            if (request.OrderId.HasValue && request.PreorderId.HasValue)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Payment must be linked to either an order OR a preorder, not both"
                });
            }

            if (!request.OrderId.HasValue && !request.PreorderId.HasValue)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Payment must be linked to an order or a preorder"
                });
            }

            // Validate payment method
            var validMethods = new[] { "vnpay", "cash", "bank_transfer" };
            if (!validMethods.Contains(request.PaymentMethod.ToLower()))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = $"Invalid payment method. Allowed values: {string.Join(", ", validMethods)}"
                });
            }

            // Create payment
            Repositories.Models.Payment? payment;
            string error;

            if (request.OrderId.HasValue)
            {
                (payment, error) = await _paymentService.CreatePaymentForOrderAsync(request.OrderId.Value, request.PaymentMethod);
            }
            else
            {
                (payment, error) = await _paymentService.CreatePaymentForPreorderAsync(request.PreorderId!.Value, request.PaymentMethod);
            }

            if (payment == null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "PAYMENT_CREATION_FAILED",
                    Message = error
                });
            }

            var response = new PaymentResponse
            {
                PaymentId = payment.PaymentId,
                OrderId = payment.OrderId,
                PreorderId = payment.PreorderId,
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod,
                PaymentStatus = payment.PaymentStatus,
                PaidAt = payment.PaidAt
            };

            // Generate VNPay URL if payment method is vnpay
            if (request.PaymentMethod.ToLower() == "vnpay")
            {
                var returnUrl = _configuration["VnPay:ReturnUrl"] ?? $"{Request.Scheme}://{Request.Host}/api/payments/vnpay-return";

                var vnpayRequest = new VnPayPaymentRequest
                {
                    PaymentId = payment.PaymentId,
                    Amount = payment.Amount ?? 0,
                    OrderInfo = request.OrderId.HasValue 
                        ? $"Payment for Order {request.OrderId}" 
                        : $"Payment for Preorder {request.PreorderId}",
                    IpAddress = GetClientIpAddress(),
                    ReturnUrl = returnUrl
                };

                var vnpayResult = _paymentService.CreateVnPayPaymentUrl(vnpayRequest);

                if (vnpayResult.Success)
                {
                    response.PaymentUrl = vnpayResult.PaymentUrl;
                }
                else
                {
                    return BadRequest(new ErrorResponse
                    {
                        ErrorCode = "VNPAY_ERROR",
                        Message = vnpayResult.Message
                    });
                }
            }

            return CreatedAtAction(nameof(GetPaymentById), new { id = payment.PaymentId }, response);
        }

        /// <summary>
        /// Gets current user's payments
        /// </summary>
        [HttpGet("my")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyPayments([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
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

            var result = await _paymentService.GetPaymentsByUserAsync(userId, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Gets a specific payment by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPaymentById(Guid id)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(id);

            if (payment == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "PAYMENT_NOT_FOUND",
                    Message = "Payment not found"
                });
            }

            return Ok(payment);
        }

        #endregion

        #region VNPay Callback Endpoints

        /// <summary>
        /// VNPay return URL handler
        /// </summary>
        [HttpGet("vnpay-return")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VnPayReturn([FromQuery] VnPayReturnRequest request)
        {
            // Convert query parameters to dictionary
            var vnpayData = new Dictionary<string, string>();
            foreach (var query in Request.Query)
            {
                vnpayData[query.Key] = query.Value.ToString();
            }

            var callbackResult = _paymentService.ProcessVnPayCallback(vnpayData);

            if (!callbackResult.Success)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "PAYMENT_FAILED",
                    Message = callbackResult.Message
                });
            }

            // Complete the payment
            var payment = await _paymentService.CompleteVnPayPaymentAsync(
                callbackResult.PaymentId,
                callbackResult.TransactionId);

            if (payment == null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "PAYMENT_COMPLETION_FAILED",
                    Message = "Failed to complete payment"
                });
            }

            // Return success response (or redirect to frontend success page)
            return Ok(new
            {
                Success = true,
                Message = "Payment completed successfully",
                PaymentId = payment.PaymentId,
                TransactionId = callbackResult.TransactionId,
                Amount = payment.Amount,
                PaidAt = payment.PaidAt
            });
        }

        /// <summary>
        /// VNPay IPN (Instant Payment Notification) handler
        /// </summary>
        [HttpPost("vnpay-ipn")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> VnPayIpn()
        {
            // Read IPN data from form
            var vnpayData = new Dictionary<string, string>();
            foreach (var query in Request.Query)
            {
                vnpayData[query.Key] = query.Value.ToString();
            }

            var callbackResult = _paymentService.ProcessVnPayCallback(vnpayData);

            if (!callbackResult.Success)
            {
                // VNPay expects specific response format
                return Ok(new { RspCode = "97", Message = "Invalid signature" });
            }

            // Check if payment exists
            var payment = await _paymentService.GetPaymentByIdAsync(callbackResult.PaymentId);
            if (payment == null)
            {
                return Ok(new { RspCode = "01", Message = "Order not found" });
            }

            // Check if payment already processed
            if (payment.PaymentStatus?.ToLower() == "completed")
            {
                return Ok(new { RspCode = "02", Message = "Order already confirmed" });
            }

            // Complete the payment
            if (callbackResult.ResponseCode == "00")
            {
                await _paymentService.CompleteVnPayPaymentAsync(
                    callbackResult.PaymentId,
                    callbackResult.TransactionId);
            }
            else
            {
                await _paymentService.UpdatePaymentStatusAsync(callbackResult.PaymentId, "failed");
            }

            return Ok(new { RspCode = "00", Message = "Confirm Success" });
        }

        #endregion

        #region Staff/Manager Endpoints

        /// <summary>
        /// Updates payment status (Staff/Manager/Admin)
        /// </summary>
        [HttpPut("{id:guid}/status")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdatePaymentStatus(Guid id, [FromBody] UpdateOrderStatusRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Status is required"
                });
            }

            var validStatuses = new[] { "pending", "processing", "completed", "failed", "cancelled", "refunded" };
            if (!validStatuses.Contains(request.Status.ToLower()))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = $"Invalid status. Allowed values: {string.Join(", ", validStatuses)}"
                });
            }

            var result = await _paymentService.UpdatePaymentStatusAsync(id, request.Status);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "UPDATE_FAILED",
                    Message = "Payment not found or status update not allowed"
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Gets payments for a specific order (Staff/Manager/Admin)
        /// </summary>
        [HttpGet("order/{orderId:guid}")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPaymentsByOrder(Guid orderId)
        {
            var payments = await _paymentService.GetPaymentsByOrderAsync(orderId);
            return Ok(payments);
        }

        /// <summary>
        /// Gets payments for a specific preorder (Staff/Manager/Admin)
        /// </summary>
        [HttpGet("preorder/{preorderId:guid}")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPaymentsByPreorder(Guid preorderId)
        {
            var payments = await _paymentService.GetPaymentsByPreorderAsync(preorderId);
            return Ok(payments);
        }

        #endregion
    }
}
