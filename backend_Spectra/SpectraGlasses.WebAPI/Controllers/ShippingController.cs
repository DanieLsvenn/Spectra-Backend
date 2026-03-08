using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShippingController : ControllerBase
    {
        private readonly IShippingService _shippingService;

        public ShippingController(IShippingService shippingService)
        {
            _shippingService = shippingService;
        }

        /// <summary>
        /// Gets all available shipping methods and their fees
        /// </summary>
        [HttpGet("methods")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetShippingMethods()
        {
            var methods = _shippingService.GetAvailableShippingMethods();
            return Ok(methods);
        }

        /// <summary>
        /// Calculates the shipping fee for a given method and order subtotal
        /// </summary>
        [HttpPost("calculate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult CalculateShippingFee([FromBody] CalculateShippingRequest request)
        {
            var fee = _shippingService.CalculateShippingFee(request.ShippingMethod, request.OrderSubtotal);
            return Ok(new
            {
                ShippingMethod = request.ShippingMethod,
                OrderSubtotal = request.OrderSubtotal,
                ShippingFee = fee,
                Total = request.OrderSubtotal + fee
            });
        }

        /// <summary>
        /// Assigns a tracking number to an order (Manager only)
        /// </summary>
        [HttpPatch("orders/{orderId:guid}/tracking")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignTracking(Guid orderId, [FromBody] AssignTrackingRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TrackingNumber))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Tracking number is required"
                });
            }

            if (string.IsNullOrWhiteSpace(request.Carrier))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Carrier is required"
                });
            }

            var result = await _shippingService.AssignTrackingNumberAsync(orderId, request.TrackingNumber, request.Carrier);
            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "ORDER_NOT_FOUND",
                    Message = "Order not found"
                });
            }

            return Ok(new
            {
                OrderId = result.OrderId,
                TrackingNumber = result.TrackingNumber,
                ShippingCarrier = result.ShippingCarrier,
                ShippedAt = result.ShippedAt,
                Status = result.Status
            });
        }
    }
}
