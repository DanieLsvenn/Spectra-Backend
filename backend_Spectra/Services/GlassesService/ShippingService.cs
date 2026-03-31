using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repositories.Basic;
using Repositories.Models;

namespace Services.GlassesService
{
    #region DTOs

    public class ShippingMethodInfo
    {
        public string Method { get; set; } = string.Empty;
        public double Fee { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    // ── Ahamove DTOs ──

    public class AhamovePathPoint
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lng")]
        public double Lng { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("short_address")]
        public string? ShortAddress { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("mobile")]
        public string Mobile { get; set; } = string.Empty;

        [JsonPropertyName("remarks")]
        public string? Remarks { get; set; }

        [JsonPropertyName("cod")]
        public int? Cod { get; set; }

        [JsonPropertyName("item_value")]
        public long? ItemValue { get; set; }

        [JsonPropertyName("tracking_number")]
        public string? TrackingNumber { get; set; }
    }

    public class AhamoveGroupServiceRequest
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("group_requests")]
        public List<AhamoveGroupRequestItem> GroupRequests { get; set; } = [];
    }

    public class AhamoveGroupRequestItem
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("num")]
        public int? Num { get; set; }
    }

    public class AhamoveItem
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("num")]
        public int Num { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public int Price { get; set; }
    }

    public class AhamovePackageDetail
    {
        [JsonPropertyName("weight")]
        public double Weight { get; set; }

        [JsonPropertyName("length")]
        public double? Length { get; set; }

        [JsonPropertyName("width")]
        public double? Width { get; set; }

        [JsonPropertyName("height")]
        public double? Height { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Request body for POST /v3/orders/estimates (multi-service estimation)
    /// </summary>
    public class AhamoveEstimateRequest
    {
        [JsonPropertyName("order_time")]
        public double OrderTime { get; set; } = 0;

        [JsonPropertyName("path")]
        public List<AhamovePathPoint> Path { get; set; } = [];

        [JsonPropertyName("group_services")]
        public List<AhamoveGroupServiceRequest> GroupServices { get; set; } = [];

        [JsonPropertyName("payment_method")]
        public string PaymentMethod { get; set; } = "BALANCE";

        [JsonPropertyName("remarks")]
        public string? Remarks { get; set; }

        [JsonPropertyName("items")]
        public List<AhamoveItem>? Items { get; set; }

        [JsonPropertyName("package_detail")]
        public List<AhamovePackageDetail>? PackageDetail { get; set; }
    }

    /// <summary>
    /// Request body for POST /v3/orders (single-service order creation)
    /// </summary>
    public class AhamoveCreateOrderRequest
    {
        [JsonPropertyName("order_time")]
        public double OrderTime { get; set; } = 0;

        [JsonPropertyName("path")]
        public List<AhamovePathPoint> Path { get; set; } = [];

        [JsonPropertyName("group_service_id")]
        public string GroupServiceId { get; set; } = string.Empty;

        [JsonPropertyName("group_requests")]
        public List<AhamoveGroupRequestItem> GroupRequests { get; set; } = [];

        [JsonPropertyName("payment_method")]
        public string PaymentMethod { get; set; } = "BALANCE";

        [JsonPropertyName("remarks")]
        public string? Remarks { get; set; }

        [JsonPropertyName("items")]
        public List<AhamoveItem>? Items { get; set; }

        [JsonPropertyName("package_detail")]
        public List<AhamovePackageDetail>? PackageDetail { get; set; }
    }

    public class AhamoveEstimateResultData
    {
        [JsonPropertyName("distance")]
        public double Distance { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("distance_fee")]
        public int DistanceFee { get; set; }

        [JsonPropertyName("request_fee")]
        public int RequestFee { get; set; }

        [JsonPropertyName("stop_fee")]
        public int StopFee { get; set; }

        [JsonPropertyName("vat_fee")]
        public int VatFee { get; set; }

        [JsonPropertyName("discount")]
        public int Discount { get; set; }

        [JsonPropertyName("total_fee")]
        public int TotalFee { get; set; }

        [JsonPropertyName("total_price")]
        public int TotalPrice { get; set; }
    }

    public class AhamoveEstimateResult
    {
        [JsonPropertyName("service_id")]
        public string ServiceId { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public AhamoveEstimateResultData? Data { get; set; }
    }

    public class AhamoveCreateOrderResponse
    {
        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("shared_link")]
        public string SharedLink { get; set; } = string.Empty;

        [JsonPropertyName("order")]
        public AhamoveOrderDetail? Order { get; set; }
    }

    public class AhamoveOrderDetail
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("service_id")]
        public string ServiceId { get; set; } = string.Empty;

        [JsonPropertyName("city_id")]
        public string CityId { get; set; } = string.Empty;

        [JsonPropertyName("distance")]
        public double Distance { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("total_fee")]
        public int TotalFee { get; set; }

        [JsonPropertyName("total_pay")]
        public int TotalPay { get; set; }

        [JsonPropertyName("total_price")]
        public int TotalPrice { get; set; }

        [JsonPropertyName("create_time")]
        public double? CreateTime { get; set; }

        [JsonPropertyName("accept_time")]
        public double? AcceptTime { get; set; }

        [JsonPropertyName("complete_time")]
        public double? CompleteTime { get; set; }

        [JsonPropertyName("cancel_time")]
        public double? CancelTime { get; set; }

        [JsonPropertyName("supplier_id")]
        public string? SupplierId { get; set; }

        [JsonPropertyName("supplier_name")]
        public string? SupplierName { get; set; }

        [JsonPropertyName("shared_link")]
        public string? SharedLink { get; set; }

        [JsonPropertyName("path")]
        public List<AhamovePathPoint>? Path { get; set; }
    }

    public class AhamoveCancelRequest
    {
        [JsonPropertyName("comment")]
        public string Comment { get; set; } = string.Empty;
    }

    public class AhamoveTokenResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }

    public class AhamoveErrorResponse
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    // Friendly service name map
    public static class AhamoveServiceNames
    {
        private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
        {
            { "BIKE", "Xe máy - Giao ngay" },
            { "ECO", "Tiết kiệm" },
            { "SAMEDAY", "Giao trong ngày" },
            { "2H", "Giao trong 2 giờ" },
            { "2H-PUBLIC", "Giao trong 2 giờ (public)" },
            { "TRUCK-500", "Xe tải 500kg" },
            { "TRUCK-1000", "Xe tải 1 tấn" },
            { "TRUCK-2000", "Xe tải 2 tấn" },
            { "VAN-500", "Xe Van 500kg" },
        };

        public static string GetName(string serviceId)
        {
            // service_id = "SGN-BIKE" -> strip city prefix to get group name
            var groupId = serviceId;
            var dash = serviceId.IndexOf('-');
            if (dash > 0 && dash < serviceId.Length - 1)
            {
                groupId = serviceId[(dash + 1)..];
            }
            return Names.TryGetValue(groupId, out var name) ? name : serviceId;
        }
    }

    #endregion

    public interface IShippingService
    {
        // Local helpers
        double CalculateShippingFee(string shippingMethod, double orderSubtotal, string? shippingAddress = null);
        List<ShippingMethodInfo> GetAvailableShippingMethods();
        Task<Order?> AssignTrackingNumberAsync(Guid orderId, string trackingNumber, string carrier);
        string DetermineShippingZone(string? shippingAddress);

        // Ahamove integration
        Task<List<AhamoveEstimateResult>> EstimateFeesAsync(AhamoveEstimateRequest request);
        Task<AhamoveCreateOrderResponse?> CreateOrderAsync(AhamoveCreateOrderRequest request);
        Task<AhamoveOrderDetail?> GetOrderDetailAsync(string ahamoveOrderId);
        Task<bool> CancelOrderAsync(string ahamoveOrderId, string comment);
        Task<Order?> CreateShipmentForOrderAsync(Guid orderId, AhamoveCreateOrderRequest request);
        Task<ComplaintRequest?> CreateShipmentForComplaintAsync(Guid complaintId, AhamoveCreateOrderRequest request);

        // Warehouse info (for frontend)
        AhamovePathPoint GetWarehousePathPoint();
        bool IsSandbox { get; }
    }

    public class ShippingService : IShippingService
    {
        private readonly GenericRepository<Order> _orderRepository;
        private readonly GenericRepository<ComplaintRequest> _complaintRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ShippingService> _logger;

        // Ahamove configuration
        private readonly string _ahamoveApiKey;
        private readonly string _ahamoveMobile;
        private readonly string _ahamoveBaseUrl;
        private readonly AhamovePathPoint _warehousePoint;
        private string? _cachedToken;
        private DateTime _tokenExpiry = DateTime.MinValue;
        private readonly SemaphoreSlim _tokenLock = new(1, 1);

        public bool IsSandbox => _ahamoveBaseUrl.Contains("stg", StringComparison.OrdinalIgnoreCase);

        private const string DefaultCarrier = "Ahamove";

        // ── Distance-based shipping fee rules (in VND) ──
        private const double BASE_FEE_VND = 20000;
        private const double BASE_DISTANCE_KM = 3;
        private const double PER_KM_FEE_VND = 5000;
        private const double MAX_FEE_VND = 150000;
        private const double FREE_SHIPPING_THRESHOLD_VND = 1500000;
        private const double USD_TO_VND_RATE = 25400;

        private static readonly Dictionary<string, double> ZoneEstimatedDistanceKm = new()
        {
            { "same_city", 10 },
            { "southern", 80 },
            { "central", 600 },
            { "northern", 1500 }
        };

        private static readonly Dictionary<string, (double multiplier, string desc)> ShippingMethods = new()
        {
            { "standard", (1.0, "Giao h\u00e0ng ti\u00eau chu\u1ea9n (5-7 ng\u00e0y l\u00e0m vi\u1ec7c)") },
            { "express", (1.5, "Giao h\u00e0ng nhanh (2-3 ng\u00e0y l\u00e0m vi\u1ec7c)") }
        };

        private static readonly Dictionary<string, string> CityZoneMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "h\u1ed3 ch\u00ed minh", "same_city" }, { "ho chi minh", "same_city" }, { "hcm", "same_city" },
            { "th\u1ee7 \u0111\u1ee9c", "same_city" }, { "thu duc", "same_city" },
            { "b\u00ecnh d\u01b0\u01a1ng", "southern" }, { "binh duong", "southern" },
            { "\u0111\u1ed3ng nai", "southern" }, { "dong nai", "southern" },
            { "long an", "southern" }, { "b\u00e0 r\u1ecba", "southern" }, { "ba ria", "southern" },
            { "v\u0169ng t\u00e0u", "southern" }, { "vung tau", "southern" },
            { "t\u00e2y ninh", "southern" }, { "tay ninh", "southern" },
            { "b\u00ecnh ph\u01b0\u1edbc", "southern" }, { "binh phuoc", "southern" },
            { "ti\u1ec1n giang", "southern" }, { "tien giang", "southern" },
            { "b\u1ebfn tre", "southern" }, { "ben tre", "southern" },
            { "v\u0129nh long", "southern" }, { "vinh long", "southern" },
            { "c\u1ea7n th\u01a1", "southern" }, { "can tho", "southern" },
            { "an giang", "southern" }, { "ki\u00ean giang", "southern" }, { "kien giang", "southern" },
            { "\u0111\u1ed3ng th\u00e1p", "southern" }, { "dong thap", "southern" },
            { "s\u00f3c tr\u0103ng", "southern" }, { "soc trang", "southern" },
            { "tr\u00e0 vinh", "southern" }, { "tra vinh", "southern" },
            { "b\u1ea1c li\u00eau", "southern" }, { "bac lieu", "southern" },
            { "c\u00e0 mau", "southern" }, { "ca mau", "southern" },
            { "h\u1eadu giang", "southern" }, { "hau giang", "southern" },
            { "l\u00e2m \u0111\u1ed3ng", "southern" }, { "lam dong", "southern" },
            { "\u0111\u00e0 l\u1ea1t", "southern" }, { "da lat", "southern" },
            { "ninh thu\u1eadn", "southern" }, { "ninh thuan", "southern" },
            { "b\u00ecnh thu\u1eadn", "southern" }, { "binh thuan", "southern" },
            { "\u0111\u00e0 n\u1eb5ng", "central" }, { "da nang", "central" },
            { "hu\u1ebf", "central" }, { "hue", "central" },
            { "th\u1eeba thi\u00ean", "central" }, { "thua thien", "central" },
            { "qu\u1ea3ng nam", "central" }, { "quang nam", "central" },
            { "qu\u1ea3ng ng\u00e3i", "central" }, { "quang ngai", "central" },
            { "b\u00ecnh \u0111\u1ecbnh", "central" }, { "binh dinh", "central" },
            { "ph\u00fa y\u00ean", "central" }, { "phu yen", "central" },
            { "kh\u00e1nh h\u00f2a", "central" }, { "khanh hoa", "central" },
            { "nha trang", "central" },
            { "gia lai", "central" }, { "kon tum", "central" },
            { "\u0111\u1eafk l\u1eafk", "central" }, { "dak lak", "central" },
            { "\u0111\u1eafk n\u00f4ng", "central" }, { "dak nong", "central" },
            { "qu\u1ea3ng b\u00ecnh", "central" }, { "quang binh", "central" },
            { "qu\u1ea3ng tr\u1ecb", "central" }, { "quang tri", "central" },
            { "h\u00e0 t\u0129nh", "central" }, { "ha tinh", "central" },
            { "ngh\u1ec7 an", "central" }, { "nghe an", "central" },
            { "thanh h\u00f3a", "central" }, { "thanh hoa", "central" },
            { "h\u00e0 n\u1ed9i", "northern" }, { "ha noi", "northern" }, { "hanoi", "northern" },
            { "h\u1ea3i ph\u00f2ng", "northern" }, { "hai phong", "northern" },
            { "nam \u0111\u1ecbnh", "northern" }, { "nam dinh", "northern" },
            { "ninh b\u00ecnh", "northern" }, { "ninh binh", "northern" },
            { "h\u1ea3i d\u01b0\u01a1ng", "northern" }, { "hai duong", "northern" },
            { "h\u01b0ng y\u00ean", "northern" }, { "hung yen", "northern" },
            { "th\u00e1i b\u00ecnh", "northern" }, { "thai binh", "northern" },
            { "b\u1eafc ninh", "northern" }, { "bac ninh", "northern" },
            { "b\u1eafc giang", "northern" }, { "bac giang", "northern" },
            { "ph\u00fa th\u1ecd", "northern" }, { "phu tho", "northern" },
            { "v\u0129nh ph\u00fac", "northern" }, { "vinh phuc", "northern" },
            { "qu\u1ea3ng ninh", "northern" }, { "quang ninh", "northern" },
            { "l\u1ea1ng s\u01a1n", "northern" }, { "lang son", "northern" },
            { "cao b\u1eb1ng", "northern" }, { "cao bang", "northern" },
            { "b\u1eafc k\u1ea1n", "northern" }, { "bac kan", "northern" },
            { "th\u00e1i nguy\u00ean", "northern" }, { "thai nguyen", "northern" },
            { "tuy\u00ean quang", "northern" }, { "tuyen quang", "northern" },
            { "h\u00e0 giang", "northern" }, { "ha giang", "northern" },
            { "l\u00e0o cai", "northern" }, { "lao cai", "northern" },
            { "y\u00ean b\u00e1i", "northern" }, { "yen bai", "northern" },
            { "lai ch\u00e2u", "northern" }, { "lai chau", "northern" },
            { "\u0111i\u1ec7n bi\u00ean", "northern" }, { "dien bien", "northern" },
            { "s\u01a1n la", "northern" }, { "son la", "northern" },
            { "h\u00f2a b\u00ecnh", "northern" }, { "hoa binh", "northern" },
        };

        public ShippingService(
            GenericRepository<Order> orderRepository,
            GenericRepository<ComplaintRequest> complaintRepository,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ShippingService> logger)
        {
            _orderRepository = orderRepository;
            _complaintRepository = complaintRepository;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            // Ahamove config
            _ahamoveApiKey = configuration["Ahamove:ApiKey"] ?? string.Empty;
            _ahamoveMobile = configuration["Ahamove:Mobile"] ?? string.Empty;
            _ahamoveBaseUrl = configuration["Ahamove:BaseUrl"] ?? "https://partner-apistg.ahamove.com/v3";

            _warehousePoint = new AhamovePathPoint
            {
                Lat = double.TryParse(configuration["Ahamove:WarehouseLat"], out var lat) ? lat : 10.7379,
                Lng = double.TryParse(configuration["Ahamove:WarehouseLng"], out var lng) ? lng : 106.7218,
                Address = configuration["Ahamove:WarehouseAddress"] ?? "123 Nguy\u1ec5n V\u0103n Linh, Qu\u1eadn 7, Th\u00e0nh ph\u1ed1 H\u1ed3 Ch\u00ed Minh",
                Name = configuration["Ahamove:WarehouseName"] ?? "Spectra Glasses Warehouse",
                Mobile = configuration["Ahamove:WarehousePhone"] ?? "0909123456"
            };
        }

        public AhamovePathPoint GetWarehousePathPoint() => _warehousePoint;

        #region Shipping zone & fee logic

        public string DetermineShippingZone(string? shippingAddress)
        {
            if (string.IsNullOrWhiteSpace(shippingAddress))
                return "southern";

            var addressLower = shippingAddress.ToLower();
            foreach (var entry in CityZoneMap)
            {
                if (addressLower.Contains(entry.Key))
                    return entry.Value;
            }
            return "southern";
        }

        public double CalculateShippingFee(string shippingMethod, double orderSubtotal, string? shippingAddress = null)
        {
            var subtotalVND = orderSubtotal * USD_TO_VND_RATE;
            if (subtotalVND >= FREE_SHIPPING_THRESHOLD_VND)
                return 0;

            var zone = DetermineShippingZone(shippingAddress);
            var distanceKm = ZoneEstimatedDistanceKm.TryGetValue(zone, out var d) ? d : 80;

            double feeVND = BASE_FEE_VND;
            if (distanceKm > BASE_DISTANCE_KM)
            {
                feeVND += (distanceKm - BASE_DISTANCE_KM) * PER_KM_FEE_VND;
            }

            if (feeVND > MAX_FEE_VND)
                feeVND = MAX_FEE_VND;

            var method = shippingMethod?.ToLower() ?? "standard";
            var multiplier = ShippingMethods.TryGetValue(method, out var info) ? info.multiplier : 1.0;
            feeVND *= multiplier;

            if (feeVND > MAX_FEE_VND)
                feeVND = MAX_FEE_VND;

            return Math.Round(feeVND / USD_TO_VND_RATE, 2);
        }

        public List<ShippingMethodInfo> GetAvailableShippingMethods()
        {
            return ShippingMethods.Select(sm => new ShippingMethodInfo
            {
                Method = sm.Key,
                Fee = 0,
                Description = sm.Value.desc
            }).ToList();
        }

        public async Task<Order?> AssignTrackingNumberAsync(Guid orderId, string trackingNumber, string carrier)
        {
            var orders = await _orderRepository.SearchAsync(o => o.OrderId == orderId);
            var order = orders.FirstOrDefault();
            if (order == null) return null;

            order.TrackingNumber = trackingNumber;
            order.ShippingCarrier = carrier ?? DefaultCarrier;
            order.ShippedAt = TimeHelper.Now;

            var deliveryDays = (order.ShippingMethod?.ToLower()) switch
            {
                "express" => 3,
                _ => 7
            };
            order.EstimatedDeliveryDate = TimeHelper.Now.AddDays(deliveryDays);

            if (order.Status?.ToLower() == "processing")
            {
                order.Status = "shipped";
            }

            return await _orderRepository.UpdateAsync(order);
        }

        #endregion

        #region Ahamove integration

        /// <summary>
        /// Obtains a valid Ahamove token, refreshing if expired. Thread-safe.
        /// </summary>
        private async Task<string> GetAhamoveTokenAsync()
        {
            await _tokenLock.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
                    return _cachedToken;

                var client = _httpClientFactory.CreateClient("Ahamove");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var payload = new { mobile = _ahamoveMobile, api_key = _ahamoveApiKey };
                var response = await client.PostAsJsonAsync($"{_ahamoveBaseUrl}/accounts/token", payload);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Ahamove token refresh failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    throw new InvalidOperationException($"Ahamove authentication failed: {body}");
                }

                var tokenResp = JsonSerializer.Deserialize<AhamoveTokenResponse>(body);
                _cachedToken = tokenResp?.Token ?? throw new InvalidOperationException("Empty Ahamove token");
                _tokenExpiry = DateTime.UtcNow.AddHours(23); // Ahamove tokens last ~24h
                return _cachedToken;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private async Task<HttpClient> CreateAhamoveClientAsync()
        {
            var token = await GetAhamoveTokenAsync();
            var client = _httpClientFactory.CreateClient("Ahamove");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        /// <summary>
        /// Estimates fees for multiple Ahamove services at once.
        /// Returns a list of available services with their fees and distances.
        /// </summary>
        public async Task<List<AhamoveEstimateResult>> EstimateFeesAsync(AhamoveEstimateRequest request)
        {
            try
            {
                var client = await CreateAhamoveClientAsync();
                var response = await client.PostAsJsonAsync($"{_ahamoveBaseUrl}/orders/estimates", request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Ahamove estimate failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    return [];
                }

                var results = JsonSerializer.Deserialize<List<AhamoveEstimateResult>>(body) ?? [];
                return results.Where(r => r.Data != null).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ahamove estimate error");
                return [];
            }
        }

        /// <summary>
        /// Creates an Ahamove delivery order. Returns order_id, shared_link, and order details.
        /// </summary>
        public async Task<AhamoveCreateOrderResponse?> CreateOrderAsync(AhamoveCreateOrderRequest request)
        {
            try
            {
                var client = await CreateAhamoveClientAsync();
                var response = await client.PostAsJsonAsync($"{_ahamoveBaseUrl}/orders", request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Ahamove create order failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    return null;
                }

                return JsonSerializer.Deserialize<AhamoveCreateOrderResponse>(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ahamove create order error");
                return null;
            }
        }

        /// <summary>
        /// Gets the detail/status of an Ahamove order by its order ID.
        /// </summary>
        public async Task<AhamoveOrderDetail?> GetOrderDetailAsync(string ahamoveOrderId)
        {
            try
            {
                var client = await CreateAhamoveClientAsync();
                var response = await client.GetAsync($"{_ahamoveBaseUrl}/orders/{Uri.EscapeDataString(ahamoveOrderId)}");
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Ahamove get order failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    return null;
                }

                return JsonSerializer.Deserialize<AhamoveOrderDetail>(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ahamove get order error");
                return null;
            }
        }

        /// <summary>
        /// Cancels an Ahamove order. Only works for IDLE, ASSIGNING, ACCEPTED, CONFIRMING, PAYING statuses.
        /// </summary>
        public async Task<bool> CancelOrderAsync(string ahamoveOrderId, string comment)
        {
            try
            {
                var client = await CreateAhamoveClientAsync();
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{_ahamoveBaseUrl}/orders/{Uri.EscapeDataString(ahamoveOrderId)}")
                {
                    Content = JsonContent.Create(new AhamoveCancelRequest { Comment = comment })
                };
                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Ahamove cancel order failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ahamove cancel order error");
                return false;
            }
        }

        /// <summary>
        /// Creates an Ahamove delivery and assigns the tracking info to an order.
        /// The Ahamove order_id is stored as the tracking number, and shared_link provides customer tracking.
        /// </summary>
        public async Task<Order?> CreateShipmentForOrderAsync(Guid orderId, AhamoveCreateOrderRequest request)
        {
            var createResult = await CreateOrderAsync(request);
            if (createResult == null || string.IsNullOrEmpty(createResult.OrderId))
                return null;

            // Use Ahamove order ID as tracking number
            var trackingNumber = createResult.OrderId;
            var carrier = $"Ahamove ({AhamoveServiceNames.GetName(createResult.Order?.ServiceId ?? request.GroupServiceId)})";

            return await AssignTrackingNumberAsync(orderId, trackingNumber, carrier);
        }

        /// <summary>
        /// Creates an Ahamove delivery and assigns the tracking info to a complaint (for return shipments).
        /// </summary>
        public async Task<ComplaintRequest?> CreateShipmentForComplaintAsync(Guid complaintId, AhamoveCreateOrderRequest request)
        {
            var complaints = await _complaintRepository.SearchAsync(c => c.RequestId == complaintId);
            var complaint = complaints.FirstOrDefault();
            if (complaint == null) return null;

            var createResult = await CreateOrderAsync(request);
            if (createResult == null || string.IsNullOrEmpty(createResult.OrderId))
                return null;

            complaint.ReturnTrackingNumber = createResult.OrderId;
            complaint.ReturnShippingCarrier = $"Ahamove ({AhamoveServiceNames.GetName(createResult.Order?.ServiceId ?? request.GroupServiceId)})";
            return await _complaintRepository.UpdateAsync(complaint);
        }

        #endregion
    }
}