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
        /// Gets all available local shipping methods and their fees
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
            var fee = _shippingService.CalculateShippingFee(request.ShippingMethod, request.OrderSubtotal, request.ShippingAddress);
            var zone = _shippingService.DetermineShippingZone(request.ShippingAddress);
            return Ok(new
            {
                ShippingMethod = request.ShippingMethod,
                OrderSubtotal = request.OrderSubtotal,
                ShippingFee = fee,
                ShippingZone = zone,
                Total = request.OrderSubtotal + fee
            });
        }

        /// <summary>
        /// Check if GoShip is in sandbox mode
        /// </summary>
        [HttpGet("goship/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetGoShipStatus()
        {
            return Ok(new { isSandbox = _shippingService.IsSandbox });
        }

        /// <summary>
        /// Get shipping rates from GoShip for the given addresses and parcel details
        /// </summary>
        [HttpPost("goship/rates")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> GetGoShipRates([FromBody] GoShipRateApiRequest request)
        {
            var rateRequest = new GoShipRateRequest
            {
                AddressFrom = request.AddressFrom,
                AddressTo = request.AddressTo,
                Parcel = request.Parcel
            };

            var result = await _shippingService.GetRatesAsync(rateRequest);
            if (result == null)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse
                {
                    ErrorCode = "GOSHIP_ERROR",
                    Message = "Failed to retrieve rates from GoShip"
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Create a shipment on GoShip and optionally link it to an order or complaint
        /// </summary>
        [HttpPost("goship/shipments")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> CreateGoShipShipment([FromBody] CreateGoShipShipmentApiRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RateId))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "RateId is required. Call GET /goship/rates first."
                });
            }

            var shipmentRequest = new GoShipShipmentRequest
            {
                Rate = request.RateId,
                AddressFrom = request.AddressFrom,
                AddressTo = request.AddressTo,
                Parcel = request.Parcel
            };

            // If an orderId is provided, create shipment and assign tracking to the order
            if (request.OrderId.HasValue)
            {
                var order = await _shippingService.CreateShipmentForOrderAsync(request.OrderId.Value, shipmentRequest);
                if (order == null)
                {
                    return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse
                    {
                        ErrorCode = "GOSHIP_ERROR",
                        Message = "Failed to create shipment on GoShip or order not found"
                    });
                }

                return Ok(new
                {
                    OrderId = order.OrderId,
                    TrackingNumber = order.TrackingNumber,
                    ShippingCarrier = order.ShippingCarrier,
                    ShippedAt = order.ShippedAt,
                    Status = order.Status
                });
            }

            // If a complaintId is provided, create shipment and assign tracking to the complaint
            if (request.ComplaintId.HasValue)
            {
                var complaint = await _shippingService.CreateShipmentForComplaintAsync(request.ComplaintId.Value, shipmentRequest);
                if (complaint == null)
                {
                    return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse
                    {
                        ErrorCode = "GOSHIP_ERROR",
                        Message = "Failed to create shipment on GoShip or complaint not found"
                    });
                }

                return Ok(new
                {
                    ComplaintId = complaint.RequestId,
                    TrackingNumber = complaint.ReturnTrackingNumber,
                    ShippingCarrier = complaint.ReturnShippingCarrier
                });
            }

            // Otherwise just create the shipment without linking to an order
            var result = await _shippingService.CreateShipmentAsync(shipmentRequest);
            if (result == null)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse
                {
                    ErrorCode = "GOSHIP_ERROR",
                    Message = "Failed to create shipment on GoShip"
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Get shipment tracking details from GoShip
        /// </summary>
        [HttpGet("goship/shipments/{shipmentId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGoShipShipment(string shipmentId)
        {
            var result = await _shippingService.GetShipmentAsync(shipmentId);
            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "SHIPMENT_NOT_FOUND",
                    Message = "Shipment not found on GoShip"
                });
            }

            return Ok(result);
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

        /// <summary>
        /// Get all GoShip cities (for location picker)
        /// </summary>
        [HttpGet("goship/cities")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetGoShipCities()
        {
            var cities = await _shippingService.GetCitiesAsync();
            return Ok(cities);
        }

        /// <summary>
        /// Get districts for a GoShip city
        /// </summary>
        [HttpGet("goship/cities/{cityId}/districts")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetGoShipDistricts(string cityId)
        {
            var districts = await _shippingService.GetDistrictsAsync(cityId);
            return Ok(districts);
        }

        /// <summary>
        /// Get wards for a GoShip district
        /// </summary>
        [HttpGet("goship/districts/{districtId}/wards")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetGoShipWards(string districtId)
        {
            var wards = await _shippingService.GetWardsAsync(districtId);
            return Ok(wards);
        }
    }
}
