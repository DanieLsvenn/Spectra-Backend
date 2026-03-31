using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repositories.Basic;
using Repositories.Models;

namespace Services.GlassesService
{
    #region GHN DTOs

    /// <summary>
    /// GHN API response wrapper - all GHN responses follow this structure
    /// </summary>
    public class GhnApiResponse<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    /// <summary>
    /// Request body for getting available GHN services
    /// </summary>
    public class GhnAvailableServicesRequest
    {
        [JsonPropertyName("shop_id")]
        public int ShopId { get; set; }

        [JsonPropertyName("from_district")]
        public int FromDistrict { get; set; }

        [JsonPropertyName("to_district")]
        public int ToDistrict { get; set; }
    }

    /// <summary>
    /// GHN service info returned from available-services endpoint
    /// </summary>
    public class GhnServiceInfo
    {
        [JsonPropertyName("service_id")]
        public int ServiceId { get; set; }

        [JsonPropertyName("short_name")]
        public string? ShortName { get; set; }

        [JsonPropertyName("service_type_id")]
        public int ServiceTypeId { get; set; }
    }

    /// <summary>
    /// Request body for calculating shipping fee
    /// </summary>
    public class GhnCalculateFeeRequest
    {
        [JsonPropertyName("service_id")]
        public int ServiceId { get; set; }

        [JsonPropertyName("service_type_id")]
        public int ServiceTypeId { get; set; }

        [JsonPropertyName("insurance_value")]
        public int InsuranceValue { get; set; }

        [JsonPropertyName("coupon")]
        public string? Coupon { get; set; }

        [JsonPropertyName("from_district_id")]
        public int FromDistrictId { get; set; }

        [JsonPropertyName("from_ward_code")]
        public string? FromWardCode { get; set; }

        [JsonPropertyName("to_district_id")]
        public int ToDistrictId { get; set; }

        [JsonPropertyName("to_ward_code")]
        public string? ToWardCode { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; } = 10;

        [JsonPropertyName("length")]
        public int Length { get; set; } = 15;

        [JsonPropertyName("weight")]
        public int Weight { get; set; } = 200;

        [JsonPropertyName("width")]
        public int Width { get; set; } = 10;
    }

    /// <summary>
    /// Response data from fee calculation
    /// </summary>
    public class GhnFeeData
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("service_fee")]
        public int ServiceFee { get; set; }

        [JsonPropertyName("insurance_fee")]
        public int InsuranceFee { get; set; }

        [JsonPropertyName("pick_station_fee")]
        public int PickStationFee { get; set; }

        [JsonPropertyName("coupon_value")]
        public int CouponValue { get; set; }

        [JsonPropertyName("r2s_fee")]
        public int R2sFee { get; set; }
    }

    /// <summary>
    /// Item in a GHN order
    /// </summary>
    public class GhnOrderItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;

        [JsonPropertyName("price")]
        public int Price { get; set; }

        [JsonPropertyName("length")]
        public int? Length { get; set; }

        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("weight")]
        public int? Weight { get; set; }

        [JsonPropertyName("category")]
        public GhnItemCategory? Category { get; set; }
    }

    public class GhnItemCategory
    {
        [JsonPropertyName("level1")]
        public string? Level1 { get; set; }
    }

    /// <summary>
    /// Request body for creating a GHN order
    /// </summary>
    public class GhnCreateOrderRequest
    {
        [JsonPropertyName("payment_type_id")]
        public int PaymentTypeId { get; set; } = 2; // 1: Shop pays, 2: Buyer pays

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("required_note")]
        public string RequiredNote { get; set; } = "CHOTHUHANG"; // CHOTHUHANG, CHOXEMHANGKHONGTHU, KHONGCHOXEMHANG

        [JsonPropertyName("return_phone")]
        public string? ReturnPhone { get; set; }

        [JsonPropertyName("return_address")]
        public string? ReturnAddress { get; set; }

        [JsonPropertyName("return_district_id")]
        public int? ReturnDistrictId { get; set; }

        [JsonPropertyName("return_ward_code")]
        public string? ReturnWardCode { get; set; }

        [JsonPropertyName("client_order_code")]
        public string? ClientOrderCode { get; set; }

        [JsonPropertyName("to_name")]
        public string ToName { get; set; } = string.Empty;

        [JsonPropertyName("to_phone")]
        public string ToPhone { get; set; } = string.Empty;

        [JsonPropertyName("to_address")]
        public string ToAddress { get; set; } = string.Empty;

        [JsonPropertyName("to_ward_code")]
        public string ToWardCode { get; set; } = string.Empty;

        [JsonPropertyName("to_district_id")]
        public int ToDistrictId { get; set; }

        [JsonPropertyName("cod_amount")]
        public int CodAmount { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("weight")]
        public int Weight { get; set; } = 200; // grams

        [JsonPropertyName("length")]
        public int Length { get; set; } = 15;

        [JsonPropertyName("width")]
        public int Width { get; set; } = 10;

        [JsonPropertyName("height")]
        public int Height { get; set; } = 10;

        [JsonPropertyName("pick_station_id")]
        public int? PickStationId { get; set; }

        [JsonPropertyName("deliver_station_id")]
        public int? DeliverStationId { get; set; }

        [JsonPropertyName("insurance_value")]
        public int InsuranceValue { get; set; }

        [JsonPropertyName("service_id")]
        public int ServiceId { get; set; }

        [JsonPropertyName("service_type_id")]
        public int ServiceTypeId { get; set; } = 2; // 2 = Standard, 5 = Express

        [JsonPropertyName("coupon")]
        public string? Coupon { get; set; }

        [JsonPropertyName("pick_shift")]
        public List<int>? PickShift { get; set; }

        [JsonPropertyName("items")]
        public List<GhnOrderItem>? Items { get; set; }
    }

    /// <summary>
    /// Response data from order creation
    /// </summary>
    public class GhnCreateOrderData
    {
        [JsonPropertyName("order_code")]
        public string OrderCode { get; set; } = string.Empty;

        [JsonPropertyName("sort_code")]
        public string? SortCode { get; set; }

        [JsonPropertyName("trans_type")]
        public string? TransType { get; set; }

        [JsonPropertyName("ward_encode")]
        public string? WardEncode { get; set; }

        [JsonPropertyName("district_encode")]
        public string? DistrictEncode { get; set; }

        [JsonPropertyName("fee")]
        public GhnOrderFee? Fee { get; set; }

        [JsonPropertyName("total_fee")]
        public int TotalFee { get; set; }

        [JsonPropertyName("expected_delivery_time")]
        public DateTime? ExpectedDeliveryTime { get; set; }
    }

    public class GhnOrderFee
    {
        [JsonPropertyName("main_service")]
        public int MainService { get; set; }

        [JsonPropertyName("insurance")]
        public int Insurance { get; set; }

        [JsonPropertyName("cod_fee")]
        public int CodFee { get; set; }

        [JsonPropertyName("station_do")]
        public int StationDo { get; set; }

        [JsonPropertyName("station_pu")]
        public int StationPu { get; set; }

        [JsonPropertyName("return")]
        public int Return { get; set; }

        [JsonPropertyName("r2s")]
        public int R2s { get; set; }

        [JsonPropertyName("coupon")]
        public int Coupon { get; set; }
    }

    /// <summary>
    /// Request body for getting order detail
    /// </summary>
    public class GhnOrderDetailRequest
    {
        [JsonPropertyName("order_code")]
        public string OrderCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Full order detail response
    /// </summary>
    public class GhnOrderDetail
    {
        [JsonPropertyName("order_code")]
        public string OrderCode { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("client_order_code")]
        public string? ClientOrderCode { get; set; }

        [JsonPropertyName("to_name")]
        public string? ToName { get; set; }

        [JsonPropertyName("to_phone")]
        public string? ToPhone { get; set; }

        [JsonPropertyName("to_address")]
        public string? ToAddress { get; set; }

        [JsonPropertyName("to_ward_code")]
        public string? ToWardCode { get; set; }

        [JsonPropertyName("to_district_id")]
        public int ToDistrictId { get; set; }

        [JsonPropertyName("weight")]
        public int Weight { get; set; }

        [JsonPropertyName("length")]
        public int Length { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("cod_amount")]
        public int CodAmount { get; set; }

        [JsonPropertyName("insurance_value")]
        public int InsuranceValue { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }

        [JsonPropertyName("leadtime")]
        public DateTime? LeadTime { get; set; }

        [JsonPropertyName("order_date")]
        public DateTime? OrderDate { get; set; }

        [JsonPropertyName("finish_date")]
        public DateTime? FinishDate { get; set; }

        [JsonPropertyName("created_date")]
        public DateTime? CreatedDate { get; set; }

        [JsonPropertyName("updated_date")]
        public DateTime? UpdatedDate { get; set; }

        [JsonPropertyName("log")]
        public List<GhnOrderLog>? Log { get; set; }
    }

    public class GhnOrderLog
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("updated_date")]
        public DateTime? UpdatedDate { get; set; }
    }

    /// <summary>
    /// Request body for canceling order
    /// </summary>
    public class GhnCancelOrderRequest
    {
        [JsonPropertyName("order_codes")]
        public List<string> OrderCodes { get; set; } = [];
    }

    /// <summary>
    /// Province/city in GHN system
    /// </summary>
    public class GhnProvince
    {
        [JsonPropertyName("ProvinceID")]
        public int ProvinceId { get; set; }

        [JsonPropertyName("ProvinceName")]
        public string? ProvinceName { get; set; }

        [JsonPropertyName("Code")]
        public string? Code { get; set; }
    }

    /// <summary>
    /// District in GHN system
    /// </summary>
    public class GhnDistrict
    {
        [JsonPropertyName("DistrictID")]
        public int DistrictId { get; set; }

        [JsonPropertyName("ProvinceID")]
        public int ProvinceId { get; set; }

        [JsonPropertyName("DistrictName")]
        public string? DistrictName { get; set; }

        [JsonPropertyName("Code")]
        public string? Code { get; set; }

        [JsonPropertyName("Type")]
        public int Type { get; set; }

        [JsonPropertyName("SupportType")]
        public int SupportType { get; set; }
    }

    /// <summary>
    /// Ward in GHN system
    /// </summary>
    public class GhnWard
    {
        [JsonPropertyName("WardCode")]
        public string? WardCode { get; set; }

        [JsonPropertyName("DistrictID")]
        public int DistrictId { get; set; }

        [JsonPropertyName("WardName")]
        public string? WardName { get; set; }
    }

    /// <summary>
    /// Warehouse/shop info for GHN
    /// </summary>
    public class GhnWarehouseInfo
    {
        public int ShopId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int DistrictId { get; set; }
        public string WardCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service type names mapping
    /// </summary>
    public static class GhnServiceNames
    {
        private static readonly Dictionary<int, string> Names = new()
        {
            { 2, "Giao hàng tiêu chuẩn" },
            { 5, "Giao hàng nhanh" },
            { 1, "Giao hàng siêu tốc" },
        };

        public static string GetName(int serviceTypeId)
        {
            return Names.TryGetValue(serviceTypeId, out var name) ? name : $"Dịch vụ #{serviceTypeId}";
        }
    }

    /// <summary>
    /// GHN order status mapping to Vietnamese
    /// </summary>
    public static class GhnStatusNames
    {
        private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ready_to_pick", "Chờ lấy hàng" },
            { "picking", "Đang lấy hàng" },
            { "cancel", "Đã huỷ" },
            { "money_collect_picking", "Đã lấy hàng, đang thu tiền" },
            { "picked", "Đã lấy hàng" },
            { "storing", "Hàng đang ở kho" },
            { "transporting", "Đang vận chuyển" },
            { "sorting", "Đang phân loại" },
            { "delivering", "Đang giao hàng" },
            { "money_collect_delivering", "Đang giao, đang thu tiền" },
            { "delivered", "Giao thành công" },
            { "delivery_fail", "Giao thất bại" },
            { "waiting_to_return", "Chờ trả hàng" },
            { "return", "Trả hàng" },
            { "return_transporting", "Đang vận chuyển trả hàng" },
            { "return_sorting", "Đang phân loại trả hàng" },
            { "returning", "Đang trả hàng" },
            { "return_fail", "Trả hàng thất bại" },
            { "returned", "Đã trả hàng" },
            { "exception", "Ngoại lệ" },
            { "damage", "Hàng bị hư hỏng" },
            { "lost", "Hàng bị mất" },
        };

        public static string GetName(string? status)
        {
            if (string.IsNullOrEmpty(status)) return "Không xác định";
            return Names.TryGetValue(status, out var name) ? name : status;
        }
    }

    #endregion

    public interface IGhnService
    {
        /// <summary>Check if using GHN staging/sandbox environment</summary>
        bool IsSandbox { get; }

        /// <summary>Get configured warehouse info</summary>
        GhnWarehouseInfo GetWarehouseInfo();

        /// <summary>Get all Vietnamese provinces</summary>
        Task<List<GhnProvince>> GetProvincesAsync();

        /// <summary>Get districts by province ID</summary>
        Task<List<GhnDistrict>> GetDistrictsAsync(int provinceId);

        /// <summary>Get wards by district ID</summary>
        Task<List<GhnWard>> GetWardsAsync(int districtId);

        /// <summary>Get available shipping services for a route</summary>
        Task<List<GhnServiceInfo>> GetAvailableServicesAsync(int fromDistrictId, int toDistrictId);

        /// <summary>Calculate shipping fee for a specific service</summary>
        Task<GhnFeeData?> CalculateFeeAsync(GhnCalculateFeeRequest request);

        /// <summary>Create a GHN shipping order</summary>
        Task<GhnCreateOrderData?> CreateOrderAsync(GhnCreateOrderRequest request);

        /// <summary>Get order detail/tracking info</summary>
        Task<GhnOrderDetail?> GetOrderDetailAsync(string orderCode);

        /// <summary>Cancel a GHN order</summary>
        Task<bool> CancelOrderAsync(string orderCode);

        /// <summary>Create shipment and assign tracking to an internal order</summary>
        Task<Order?> CreateShipmentForOrderAsync(Guid orderId, GhnCreateOrderRequest request);

        /// <summary>Create shipment for complaint return</summary>
        Task<ComplaintRequest?> CreateShipmentForComplaintAsync(Guid complaintId, GhnCreateOrderRequest request);

        /// <summary>
        /// [SANDBOX ONLY] Switch order status for testing purposes.
        /// Valid statuses: ready_to_pick, picking, picked, storing, transporting, delivering, delivered, delivery_fail, return, returned
        /// </summary>
        Task<(bool Success, string? ErrorMessage)> SwitchOrderStatusAsync(string orderCode, string status);
    }

    public class GhnService : IGhnService
    {
        private readonly GenericRepository<Order> _orderRepository;
        private readonly GenericRepository<ComplaintRequest> _complaintRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GhnService> _logger;

        // GHN configuration
        private readonly string _ghnToken;
        private readonly int _ghnShopId;
        private readonly string _ghnBaseUrl;
        private readonly GhnWarehouseInfo _warehouseInfo;

        private const string DefaultCarrier = "GHN";

        public bool IsSandbox => _ghnBaseUrl.Contains("dev", StringComparison.OrdinalIgnoreCase);

        public GhnService(
            GenericRepository<Order> orderRepository,
            GenericRepository<ComplaintRequest> complaintRepository,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GhnService> logger)
        {
            _orderRepository = orderRepository;
            _complaintRepository = complaintRepository;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            // Load GHN configuration
            _ghnToken = configuration["Ghn:Token"] ?? string.Empty;
            _ghnShopId = int.TryParse(configuration["Ghn:ShopId"], out var shopId) ? shopId : 0;
            _ghnBaseUrl = configuration["Ghn:BaseUrl"] ?? "https://dev-online-gateway.ghn.vn";

            _warehouseInfo = new GhnWarehouseInfo
            {
                ShopId = _ghnShopId,
                Name = configuration["Ghn:WarehouseName"] ?? "Spectra Glasses Warehouse",
                Phone = configuration["Ghn:WarehousePhone"] ?? "0912518309",
                Address = configuration["Ghn:WarehouseAddress"] ?? "25 đường 5, khu dân cư Đông Dương, Long Trường, Thủ Đức, HCM",
                DistrictId = int.TryParse(configuration["Ghn:WarehouseDistrictId"], out var districtId) ? districtId : 3695, // Thủ Đức default
                WardCode = configuration["Ghn:WarehouseWardCode"] ?? "90768" // Long Trường default
            };
        }

        public GhnWarehouseInfo GetWarehouseInfo() => _warehouseInfo;

        private HttpClient CreateGhnClient()
        {
            var client = _httpClientFactory.CreateClient("GHN");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Token", _ghnToken);
            client.DefaultRequestHeaders.Add("ShopId", _ghnShopId.ToString());
            return client;
        }

        #region Address APIs

        public async Task<List<GhnProvince>> GetProvincesAsync()
        {
            try
            {
                var client = CreateGhnClient();
                var response = await client.GetAsync($"{_ghnBaseUrl}/shiip/public-api/master-data/province");
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GHN get provinces failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    return [];
                }

                var result = JsonSerializer.Deserialize<GhnApiResponse<List<GhnProvince>>>(body);
                return result?.Data ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GHN get provinces error");
                return [];
            }
        }

        public async Task<List<GhnDistrict>> GetDistrictsAsync(int provinceId)
        {
            try
            {
                var client = CreateGhnClient();
                var response = await client.GetAsync($"{_ghnBaseUrl}/shiip/public-api/master-data/district?province_id={provinceId}");
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GHN get districts failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    return [];
                }

                var result = JsonSerializer.Deserialize<GhnApiResponse<List<GhnDistrict>>>(body);
                return result?.Data ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GHN get districts error");
                return [];
            }
        }

        public async Task<List<GhnWard>> GetWardsAsync(int districtId)
        {
            try
            {
                var client = CreateGhnClient();
                var response = await client.GetAsync($"{_ghnBaseUrl}/shiip/public-api/master-data/ward?district_id={districtId}");
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GHN get wards failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    return [];
                }

                var result = JsonSerializer.Deserialize<GhnApiResponse<List<GhnWard>>>(body);
                return result?.Data ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GHN get wards error");
                return [];
            }
        }

        #endregion

        #region Service & Fee APIs

        public async Task<List<GhnServiceInfo>> GetAvailableServicesAsync(int fromDistrictId, int toDistrictId)
        {
            try
            {
                var client = CreateGhnClient();
                var request = new GhnAvailableServicesRequest
                {
                    ShopId = _ghnShopId,
                    FromDistrict = fromDistrictId,
                    ToDistrict = toDistrictId
                };

                var response = await client.PostAsJsonAsync($"{_ghnBaseUrl}/shiip/public-api/v2/shipping-order/available-services", request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GHN get services failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    return [];
                }

                var result = JsonSerializer.Deserialize<GhnApiResponse<List<GhnServiceInfo>>>(body);
                return result?.Data ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GHN get services error");
                return [];
            }
        }

        public async Task<GhnFeeData?> CalculateFeeAsync(GhnCalculateFeeRequest request)
        {
            try
            {
                // Auto-fill warehouse info if not provided
                if (request.FromDistrictId == 0)
                    request.FromDistrictId = _warehouseInfo.DistrictId;
                if (string.IsNullOrEmpty(request.FromWardCode))
                    request.FromWardCode = _warehouseInfo.WardCode;

                var client = CreateGhnClient();
                var response = await client.PostAsJsonAsync($"{_ghnBaseUrl}/shiip/public-api/v2/shipping-order/fee", request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GHN calculate fee failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    return null;
                }

                var result = JsonSerializer.Deserialize<GhnApiResponse<GhnFeeData>>(body);
                return result?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GHN calculate fee error");
                return null;
            }
        }

        #endregion

        #region Order APIs

        public async Task<GhnCreateOrderData?> CreateOrderAsync(GhnCreateOrderRequest request)
        {
            try
            {
                var client = CreateGhnClient();
                var response = await client.PostAsJsonAsync($"{_ghnBaseUrl}/shiip/public-api/v2/shipping-order/create", request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GHN create order failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    return null;
                }

                var result = JsonSerializer.Deserialize<GhnApiResponse<GhnCreateOrderData>>(body);
                return result?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GHN create order error");
                return null;
            }
        }

        public async Task<GhnOrderDetail?> GetOrderDetailAsync(string orderCode)
        {
            try
            {
                var client = CreateGhnClient();
                var request = new GhnOrderDetailRequest { OrderCode = orderCode };
                var response = await client.PostAsJsonAsync($"{_ghnBaseUrl}/shiip/public-api/v2/shipping-order/detail", request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GHN get order detail failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    return null;
                }

                var result = JsonSerializer.Deserialize<GhnApiResponse<GhnOrderDetail>>(body);
                return result?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GHN get order detail error");
                return null;
            }
        }

        public async Task<bool> CancelOrderAsync(string orderCode)
        {
            try
            {
                var client = CreateGhnClient();
                var request = new GhnCancelOrderRequest { OrderCodes = [orderCode] };
                var response = await client.PostAsJsonAsync($"{_ghnBaseUrl}/shiip/public-api/v2/switch-status/cancel", request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GHN cancel order failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GHN cancel order error");
                return false;
            }
        }

        #endregion

        #region Integration with Internal Orders

        public async Task<Order?> CreateShipmentForOrderAsync(Guid orderId, GhnCreateOrderRequest request)
        {
            var createResult = await CreateOrderAsync(request);
            if (createResult == null || string.IsNullOrEmpty(createResult.OrderCode))
                return null;

            // Get the order and update it
            var orders = await _orderRepository.SearchAsync(o => o.OrderId == orderId);
            var order = orders.FirstOrDefault();
            if (order == null) return null;

            order.TrackingNumber = createResult.OrderCode;
            order.ShippingCarrier = $"GHN ({GhnServiceNames.GetName(request.ServiceTypeId)})";
            order.ShippedAt = TimeHelper.Now;
            order.EstimatedDeliveryDate = createResult.ExpectedDeliveryTime ?? TimeHelper.Now.AddDays(request.ServiceTypeId == 5 ? 3 : 7);

            if (order.Status?.ToLower() == "processing")
            {
                order.Status = "shipped";
            }

            return await _orderRepository.UpdateAsync(order);
        }

        public async Task<ComplaintRequest?> CreateShipmentForComplaintAsync(Guid complaintId, GhnCreateOrderRequest request)
        {
            var complaints = await _complaintRepository.SearchAsync(c => c.RequestId == complaintId);
            var complaint = complaints.FirstOrDefault();
            if (complaint == null) return null;

            var createResult = await CreateOrderAsync(request);
            if (createResult == null || string.IsNullOrEmpty(createResult.OrderCode))
                return null;

            complaint.ReturnTrackingNumber = createResult.OrderCode;
            complaint.ReturnShippingCarrier = $"GHN ({GhnServiceNames.GetName(request.ServiceTypeId)})";
            return await _complaintRepository.UpdateAsync(complaint);
        }

        /// <summary>
        /// [SANDBOX ONLY] Switch GHN order status for testing/demo purposes.
        /// This endpoint only works in GHN's dev environment.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> SwitchOrderStatusAsync(string orderCode, string status)
        {
            if (!IsSandbox)
            {
                return (false, "Chức năng này chỉ khả dụng trong môi trường GHN Sandbox/Dev.");
            }

            try
            {
                var client = CreateGhnClient();
                
                // GHN API uses order_codes (array) not order_code (single value)
                var json = JsonSerializer.Serialize(new
                {
                    order_codes = new[] { orderCode },
                    status = status
                });
                
                _logger.LogInformation("GHN switch-status request: {Url} - Body: {Body}", 
                    $"{_ghnBaseUrl}/shiip/public-api/v2/switch-status/order", json);
                
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{_ghnBaseUrl}/shiip/public-api/v2/switch-status/order", content);

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("GHN switch-status response: {StatusCode} - {Content}", response.StatusCode, responseContent);

                var result = JsonSerializer.Deserialize<GhnApiResponse<object>>(responseContent);
                
                if (result?.Code == 200)
                {
                    _logger.LogInformation("Successfully switched GHN order {OrderCode} to status {Status}", orderCode, status);
                    return (true, null);
                }
                
                // Return GHN's error message
                var errorMsg = result?.Message ?? $"GHN API returned code {result?.Code}";
                _logger.LogWarning("GHN switch-status failed: {Message}", errorMsg);
                return (false, $"GHN: {errorMsg}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching GHN order status");
                return (false, $"Exception: {ex.Message}");
            }
        }

        #endregion
    }
}
