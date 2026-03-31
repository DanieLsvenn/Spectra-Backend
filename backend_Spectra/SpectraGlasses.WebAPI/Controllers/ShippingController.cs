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
        private readonly IGhnService _ghnService;

        public ShippingController(IShippingService shippingService, IGhnService ghnService)
        {
            _shippingService = shippingService;
            _ghnService = ghnService;
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
        /// Check Ahamove connection status (sandbox or production)
        /// </summary>
        [HttpGet("ahamove/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetAhamoveStatus()
        {
            return Ok(new
            {
                isSandbox = _shippingService.IsSandbox,
                warehouse = _shippingService.GetWarehousePathPoint()
            });
        }

        /// <summary>
        /// Estimate delivery fees from Ahamove for multiple service types.
        /// The warehouse (pickup address) is configured server-side; only destination is required.
        /// </summary>
        [HttpPost("ahamove/estimate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> EstimateAhamoveFees([FromBody] AhamoveEstimateApiRequest request)
        {
            if (request.DestinationLat == 0 && request.DestinationLng == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Destination lat/lng is required"
                });
            }

            var warehouse = _shippingService.GetWarehousePathPoint();

            // Build Ahamove estimate request
            var estimateReq = new AhamoveEstimateRequest
            {
                Path = new List<AhamovePathPoint>
                {
                    warehouse,
                    new AhamovePathPoint
                    {
                        Lat = request.DestinationLat,
                        Lng = request.DestinationLng,
                        Address = request.DestinationAddress ?? "",
                        Name = request.RecipientName ?? "",
                        Mobile = request.RecipientPhone ?? "",
                        Cod = request.Cod,
                        ItemValue = request.ItemValue
                    }
                },
                GroupServices = request.GroupServices ?? new List<AhamoveGroupServiceRequest>
                {
                    new() { Id = "BIKE", GroupRequests = new List<AhamoveGroupRequestItem>() },
                    new() { Id = "ECO", GroupRequests = new List<AhamoveGroupRequestItem>() },
                    new() { Id = "SAMEDAY", GroupRequests = new List<AhamoveGroupRequestItem>() }
                },
                PaymentMethod = request.PaymentMethod ?? "BALANCE",
                Items = request.Items,
                PackageDetail = request.PackageDetail ?? new List<AhamovePackageDetail>
                {
                    new() { Weight = 0.5, Description = "Kính mắt Spectra" }
                }
            };

            var results = await _shippingService.EstimateFeesAsync(estimateReq);
            return Ok(results.Select(r => new
            {
                serviceId = r.ServiceId,
                serviceName = AhamoveServiceNames.GetName(r.ServiceId),
                distance = r.Data?.Distance,
                duration = r.Data?.Duration,
                totalFee = r.Data?.TotalFee,
                totalPrice = r.Data?.TotalPrice,
                distanceFee = r.Data?.DistanceFee,
                discount = r.Data?.Discount
            }));
        }

        /// <summary>
        /// Create an Ahamove delivery order. Warehouse is the pickup; destination is from request body.
        /// Can optionally link to an internal order or complaint.
        /// </summary>
        [HttpPost("ahamove/orders")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> CreateAhamoveOrder([FromBody] CreateAhamoveOrderApiRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.GroupServiceId))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "GroupServiceId is required (e.g. BIKE, ECO, SAMEDAY)"
                });
            }

            var warehouse = _shippingService.GetWarehousePathPoint();

            var ahamoveReq = new AhamoveCreateOrderRequest
            {
                Path = new List<AhamovePathPoint>
                {
                    warehouse,
                    new AhamovePathPoint
                    {
                        Lat = request.DestinationLat,
                        Lng = request.DestinationLng,
                        Address = request.DestinationAddress ?? "",
                        Name = request.RecipientName ?? "",
                        Mobile = request.RecipientPhone ?? "",
                        Remarks = request.Remarks,
                        Cod = request.Cod,
                        ItemValue = request.ItemValue
                    }
                },
                GroupServiceId = request.GroupServiceId,
                PaymentMethod = request.PaymentMethod ?? "BALANCE",
                Items = request.Items,
                PackageDetail = request.PackageDetail ?? new List<AhamovePackageDetail>
                {
                    new() { Weight = 0.5, Description = "Kính mắt Spectra" }
                },
                Remarks = request.Remarks
            };

            // If an orderId is provided, create order and assign tracking
            if (request.OrderId.HasValue)
            {
                var order = await _shippingService.CreateShipmentForOrderAsync(request.OrderId.Value, ahamoveReq);
                if (order == null)
                {
                    return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse
                    {
                        ErrorCode = "AHAMOVE_ERROR",
                        Message = "Failed to create delivery on Ahamove or order not found"
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

            // If a complaintId is provided, create shipment for complaint
            if (request.ComplaintId.HasValue)
            {
                var complaint = await _shippingService.CreateShipmentForComplaintAsync(request.ComplaintId.Value, ahamoveReq);
                if (complaint == null)
                {
                    return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse
                    {
                        ErrorCode = "AHAMOVE_ERROR",
                        Message = "Failed to create delivery on Ahamove or complaint not found"
                    });
                }

                return Ok(new
                {
                    ComplaintId = complaint.RequestId,
                    TrackingNumber = complaint.ReturnTrackingNumber,
                    ShippingCarrier = complaint.ReturnShippingCarrier
                });
            }

            // Standalone order (no internal linking)
            var result = await _shippingService.CreateOrderAsync(ahamoveReq);
            if (result == null)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse
                {
                    ErrorCode = "AHAMOVE_ERROR",
                    Message = "Failed to create order on Ahamove"
                });
            }

            return Ok(new
            {
                ahamoveOrderId = result.OrderId,
                status = result.Status,
                sharedLink = result.SharedLink,
                order = result.Order
            });
        }

        /// <summary>
        /// Get Ahamove order detail / tracking info
        /// </summary>
        [HttpGet("ahamove/orders/{ahamoveOrderId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAhamoveOrder(string ahamoveOrderId)
        {
            var result = await _shippingService.GetOrderDetailAsync(ahamoveOrderId);
            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "ORDER_NOT_FOUND",
                    Message = "Ahamove order not found"
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Cancel an Ahamove order (only possible in IDLE/ASSIGNING/ACCEPTED/CONFIRMING/PAYING statuses)
        /// </summary>
        [HttpDelete("ahamove/orders/{ahamoveOrderId}")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CancelAhamoveOrder(string ahamoveOrderId, [FromBody] AhamoveCancelRequest? request)
        {
            var comment = request?.Comment ?? "Cancelled by admin";
            var success = await _shippingService.CancelOrderAsync(ahamoveOrderId, comment);
            if (!success)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "CANCEL_FAILED",
                    Message = "Failed to cancel Ahamove order. It may have already been picked up or delivered."
                });
            }

            return Ok(new { message = "Order cancelled successfully" });
        }

        /// <summary>
        /// Assigns a tracking number to an order (Manager only, for manual tracking)
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

        #region GHN (Giao Hàng Nhanh) Endpoints

        /// <summary>
        /// Check GHN connection status (sandbox or production)
        /// </summary>
        [HttpGet("ghn/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetGhnStatus()
        {
            var warehouse = _ghnService.GetWarehouseInfo();
            return Ok(new
            {
                isSandbox = _ghnService.IsSandbox,
                warehouse = warehouse
            });
        }

        /// <summary>
        /// Get all Vietnamese provinces for GHN address selection
        /// </summary>
        [HttpGet("ghn/provinces")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetGhnProvinces()
        {
            var provinces = await _ghnService.GetProvincesAsync();
            return Ok(provinces);
        }

        /// <summary>
        /// Get districts by province ID for GHN address selection
        /// </summary>
        [HttpGet("ghn/districts/{provinceId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetGhnDistricts(int provinceId)
        {
            var districts = await _ghnService.GetDistrictsAsync(provinceId);
            return Ok(districts);
        }

        /// <summary>
        /// Get wards by district ID for GHN address selection
        /// </summary>
        [HttpGet("ghn/wards/{districtId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetGhnWards(int districtId)
        {
            var wards = await _ghnService.GetWardsAsync(districtId);
            return Ok(wards);
        }

        /// <summary>
        /// Get available GHN shipping services for a route
        /// </summary>
        [HttpPost("ghn/services")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetGhnServices([FromBody] GhnServicesApiRequest request)
        {
            var warehouse = _ghnService.GetWarehouseInfo();
            var services = await _ghnService.GetAvailableServicesAsync(warehouse.DistrictId, request.ToDistrictId);
            return Ok(services.Select(s => new
            {
                serviceId = s.ServiceId,
                serviceTypeId = s.ServiceTypeId,
                shortName = s.ShortName,
                serviceName = GhnServiceNames.GetName(s.ServiceTypeId)
            }));
        }

        /// <summary>
        /// Calculate GHN shipping fee for a specific service
        /// </summary>
        [HttpPost("ghn/calculate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CalculateGhnFee([FromBody] GhnCalculateFeeApiRequest request)
        {
            if (request.ToDistrictId == 0 || string.IsNullOrEmpty(request.ToWardCode))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "ToDistrictId and ToWardCode are required"
                });
            }

            var feeRequest = new GhnCalculateFeeRequest
            {
                ServiceId = request.ServiceId,
                ServiceTypeId = request.ServiceTypeId,
                ToDistrictId = request.ToDistrictId,
                ToWardCode = request.ToWardCode,
                InsuranceValue = request.InsuranceValue,
                Weight = request.Weight,
                Length = request.Length,
                Width = request.Width,
                Height = request.Height
            };

            var result = await _ghnService.CalculateFeeAsync(feeRequest);
            if (result == null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "GHN_ERROR",
                    Message = "Could not calculate shipping fee"
                });
            }

            return Ok(new
            {
                total = result.Total,
                serviceFee = result.ServiceFee,
                insuranceFee = result.InsuranceFee,
                couponValue = result.CouponValue
            });
        }

        /// <summary>
        /// Create a GHN shipping order. Can optionally link to an internal order or complaint.
        /// </summary>
        [HttpPost("ghn/orders")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> CreateGhnOrder([FromBody] GhnCreateOrderApiRequest request)
        {
            if (request.ToDistrictId == 0 || string.IsNullOrEmpty(request.ToWardCode))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "ToDistrictId and ToWardCode are required"
                });
            }

            if (string.IsNullOrEmpty(request.ToName) || string.IsNullOrEmpty(request.ToPhone))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "ToName and ToPhone are required"
                });
            }

            var ghnRequest = new GhnCreateOrderRequest
            {
                ServiceId = request.ServiceId,
                ServiceTypeId = request.ServiceTypeId,
                ToName = request.ToName,
                ToPhone = request.ToPhone,
                ToAddress = request.ToAddress,
                ToWardCode = request.ToWardCode,
                ToDistrictId = request.ToDistrictId,
                CodAmount = request.CodAmount,
                InsuranceValue = request.InsuranceValue,
                Weight = request.Weight,
                Length = request.Length,
                Width = request.Width,
                Height = request.Height,
                Note = request.Note,
                Content = request.Content ?? "Kính mắt Spectra",
                RequiredNote = request.RequiredNote,
                Items = request.Items ?? new List<GhnOrderItem>
                {
                    new() { Name = "Kính mắt Spectra", Quantity = 1, Price = request.InsuranceValue }
                },
                ClientOrderCode = request.OrderId?.ToString()
            };

            // If an orderId is provided, create order and assign tracking
            if (request.OrderId.HasValue)
            {
                var order = await _ghnService.CreateShipmentForOrderAsync(request.OrderId.Value, ghnRequest);
                if (order == null)
                {
                    return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse
                    {
                        ErrorCode = "GHN_ERROR",
                        Message = "Failed to create delivery on GHN or order not found"
                    });
                }

                return Ok(new
                {
                    orderId = order.OrderId,
                    trackingNumber = order.TrackingNumber,
                    shippingCarrier = order.ShippingCarrier,
                    shippedAt = order.ShippedAt,
                    estimatedDeliveryDate = order.EstimatedDeliveryDate,
                    status = order.Status
                });
            }

            // If a complaintId is provided, create shipment for complaint
            if (request.ComplaintId.HasValue)
            {
                var complaint = await _ghnService.CreateShipmentForComplaintAsync(request.ComplaintId.Value, ghnRequest);
                if (complaint == null)
                {
                    return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse
                    {
                        ErrorCode = "GHN_ERROR",
                        Message = "Failed to create delivery on GHN or complaint not found"
                    });
                }

                return Ok(new
                {
                    complaintId = complaint.RequestId,
                    trackingNumber = complaint.ReturnTrackingNumber,
                    shippingCarrier = complaint.ReturnShippingCarrier
                });
            }

            // Standalone order (no internal linking)
            var result = await _ghnService.CreateOrderAsync(ghnRequest);
            if (result == null)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse
                {
                    ErrorCode = "GHN_ERROR",
                    Message = "Failed to create order on GHN"
                });
            }

            return Ok(new
            {
                orderCode = result.OrderCode,
                sortCode = result.SortCode,
                totalFee = result.TotalFee,
                expectedDeliveryTime = result.ExpectedDeliveryTime
            });
        }

        /// <summary>
        /// Get GHN order detail / tracking info
        /// </summary>
        [HttpGet("ghn/orders/{orderCode}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGhnOrder(string orderCode)
        {
            var result = await _ghnService.GetOrderDetailAsync(orderCode);
            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "ORDER_NOT_FOUND",
                    Message = "GHN order not found"
                });
            }

            return Ok(new
            {
                orderCode = result.OrderCode,
                status = result.Status,
                statusName = GhnStatusNames.GetName(result.Status),
                toName = result.ToName,
                toPhone = result.ToPhone,
                toAddress = result.ToAddress,
                codAmount = result.CodAmount,
                leadTime = result.LeadTime,
                orderDate = result.OrderDate,
                finishDate = result.FinishDate,
                log = result.Log?.Select(l => new
                {
                    status = l.Status,
                    statusName = GhnStatusNames.GetName(l.Status),
                    updatedDate = l.UpdatedDate
                })
            });
        }

        /// <summary>
        /// Cancel a GHN order (only possible before pickup)
        /// </summary>
        [HttpDelete("ghn/orders/{orderCode}")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CancelGhnOrder(string orderCode)
        {
            var success = await _ghnService.CancelOrderAsync(orderCode);
            if (!success)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "CANCEL_FAILED",
                    Message = "Failed to cancel GHN order. It may have already been picked up."
                });
            }

            return Ok(new { message = "GHN order cancelled successfully" });
        }

        /// <summary>
        /// [SANDBOX ONLY] Switch GHN order status for testing/demo purposes.
        /// Valid statuses: ready_to_pick, picking, picked, storing, transporting, delivering, delivered, delivery_fail, return, returned
        /// </summary>
        [HttpPost("ghn/orders/{orderCode}/switch-status")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SwitchGhnOrderStatus(string orderCode, [FromBody] GhnSwitchStatusRequest request)
        {
            if (!_ghnService.IsSandbox)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "NOT_SANDBOX",
                    Message = "This feature is only available in GHN sandbox/dev environment."
                });
            }

            var validStatuses = new[]
            {
                "ready_to_pick", "picking", "picked", "storing", "transporting",
                "sorting", "delivering", "delivered", "delivery_fail",
                "waiting_to_return", "return", "returning", "returned"
            };

            if (!validStatuses.Contains(request.Status?.ToLower()))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "INVALID_STATUS",
                    Message = $"Invalid status. Valid values: {string.Join(", ", validStatuses)}"
                });
            }

            var (success, errorMessage) = await _ghnService.SwitchOrderStatusAsync(orderCode, request.Status!.ToLower());
            if (!success)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "SWITCH_FAILED",
                    Message = errorMessage ?? "Failed to switch GHN order status."
                });
            }

            return Ok(new { message = $"GHN order status switched to '{request.Status}' successfully" });
        }

        #endregion
    }
}