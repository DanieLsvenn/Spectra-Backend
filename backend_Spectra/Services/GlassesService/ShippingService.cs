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

    // GoShip address used for rate & shipment requests
    public class GoShipAddress
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;

        [JsonPropertyName("street")]
        public string Street { get; set; } = string.Empty;

        [JsonPropertyName("ward")]
        public string Ward { get; set; } = string.Empty;

        [JsonPropertyName("district")]
        public string District { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;
    }

    public class GoShipParcel
    {
        [JsonPropertyName("cod")]
        public int Cod { get; set; }

        [JsonPropertyName("weight")]
        public int Weight { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("length")]
        public int Length { get; set; }

        [JsonPropertyName("metadata")]
        public string Metadata { get; set; } = string.Empty;
    }

    public class GoShipShipmentRequest
    {
        [JsonPropertyName("rate")]
        public string Rate { get; set; } = string.Empty;

        [JsonPropertyName("address_from")]
        public GoShipAddress AddressFrom { get; set; } = new();

        [JsonPropertyName("address_to")]
        public GoShipAddress AddressTo { get; set; } = new();

        [JsonPropertyName("parcel")]
        public GoShipParcel Parcel { get; set; } = new();
    }

    public class GoShipRateRequest
    {
        [JsonPropertyName("address_from")]
        public GoShipAddress AddressFrom { get; set; } = new();

        [JsonPropertyName("address_to")]
        public GoShipAddress AddressTo { get; set; } = new();

        [JsonPropertyName("parcel")]
        public GoShipParcel Parcel { get; set; } = new();
    }

    public class GoShipRateResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public List<GoShipRate> Data { get; set; } = [];
    }

    public class GoShipRate
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("carrier_name")]
        public string CarrierName { get; set; } = string.Empty;

        [JsonPropertyName("carrier_logo")]
        public string CarrierLogo { get; set; } = string.Empty;

        [JsonPropertyName("service")]
        public string Service { get; set; } = string.Empty;

        [JsonPropertyName("total_fee")]
        public double TotalFee { get; set; }

        [JsonPropertyName("total_fee_after_discount")]
        public double TotalFeeAfterDiscount { get; set; }

        [JsonPropertyName("expected")]
        public string Expected { get; set; } = string.Empty;
    }

    public class GoShipShipmentResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        // GoShip returns shipment fields at the top level (not nested in data)
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("tracking_number")]
        public string TrackingNumber { get; set; } = string.Empty;

        [JsonPropertyName("carrier")]
        public string Carrier { get; set; } = string.Empty;

        [JsonPropertyName("carrier_short_name")]
        public string CarrierShortName { get; set; } = string.Empty;

        [JsonPropertyName("fee")]
        public double Fee { get; set; }

        [JsonPropertyName("cod")]
        public int Cod { get; set; }

        [JsonPropertyName("shipment_status")]
        public int ShipmentStatus { get; set; }

        [JsonPropertyName("shipment_status_txt")]
        public string ShipmentStatusTxt { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;
    }

    public class GoShipShipmentData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("tracking_number")]
        public string TrackingNumber { get; set; } = string.Empty;

        [JsonPropertyName("carrier")]
        public string Carrier { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("rate")]
        public string Rate { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public double Price { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;
    }

    public class GoShipTrackingResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public GoShipShipmentData? Data { get; set; }
    }

    // GoShip location models
    public class GoShipCity
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class GoShipDistrict
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("city_id")]
        public string CityId { get; set; } = string.Empty;
    }

    public class GoShipWard
    {
        [JsonPropertyName("id")]
        public object Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("district_id")]
        public string DistrictId { get; set; } = string.Empty;
    }

    public class GoShipLocationResponse<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public List<T> Data { get; set; } = [];
    }

    #endregion

    public interface IShippingService
    {
        // Local helpers
        double CalculateShippingFee(string shippingMethod, double orderSubtotal, string? shippingAddress = null);
        List<ShippingMethodInfo> GetAvailableShippingMethods();
        Task<Order?> AssignTrackingNumberAsync(Guid orderId, string trackingNumber, string carrier);
        string DetermineShippingZone(string? shippingAddress);

        // GoShip integration (filtered to J&T Express)
        Task<GoShipRateResponse?> GetRatesAsync(GoShipRateRequest request);
        Task<GoShipShipmentResponse?> CreateShipmentAsync(GoShipShipmentRequest request);
        Task<GoShipTrackingResponse?> GetShipmentAsync(string shipmentId);
        Task<Order?> CreateShipmentForOrderAsync(Guid orderId, GoShipShipmentRequest request);
        Task<ComplaintRequest?> CreateShipmentForComplaintAsync(Guid complaintId, GoShipShipmentRequest request);

        // GoShip location lookups
        Task<List<GoShipCity>> GetCitiesAsync();
        Task<List<GoShipDistrict>> GetDistrictsAsync(string cityId);
        Task<List<GoShipWard>> GetWardsAsync(string districtId);

        // Environment info
        bool IsSandbox { get; }
    }

    public class ShippingService : IShippingService
    {
        private readonly GenericRepository<Order> _orderRepository;
        private readonly GenericRepository<ComplaintRequest> _complaintRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ShippingService> _logger;
        private readonly string _goShipToken;
        private readonly double _freeShippingThreshold;
        private readonly string GoShipBaseUrl;

        public bool IsSandbox => GoShipBaseUrl.Contains("sandbox", StringComparison.OrdinalIgnoreCase);

        private const string DefaultCarrier = "J&T Express";

        // Shipping methods: standard is FREE (seller strategy), express has zone-based fee
        private static readonly Dictionary<string, (double baseFee, string desc)> ShippingMethods = new()
        {
            { "standard", (0.0, "Standard Shipping via J&T Express (5-7 business days) — Free") },
            { "express", (2.0, "Express Shipping via J&T Express (2-3 business days)") }
        };

        // Zone-based express surcharge (USD)
        // Zone 1: Same city (HCM) — base fee only ($2)
        // Zone 2: Southern region — base + $2 = $4
        // Zone 3: Central region — base + $4 = $6
        // Zone 4: Northern region — base + $5 = $7
        private static readonly Dictionary<string, double> ZoneSurcharge = new()
        {
            { "same_city", 0.0 },
            { "southern", 2.0 },
            { "central", 4.0 },
            { "northern", 5.0 }
        };

        // City/province → zone mapping (warehouse assumed in HCM)
        private static readonly Dictionary<string, string> CityZoneMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // Same city
            { "hồ chí minh", "same_city" }, { "ho chi minh", "same_city" }, { "hcm", "same_city" },
            { "thủ đức", "same_city" }, { "thu duc", "same_city" },
            // Southern region
            { "bình dương", "southern" }, { "binh duong", "southern" },
            { "đồng nai", "southern" }, { "dong nai", "southern" },
            { "long an", "southern" }, { "bà rịa", "southern" }, { "ba ria", "southern" },
            { "vũng tàu", "southern" }, { "vung tau", "southern" },
            { "tây ninh", "southern" }, { "tay ninh", "southern" },
            { "bình phước", "southern" }, { "binh phuoc", "southern" },
            { "tiền giang", "southern" }, { "tien giang", "southern" },
            { "bến tre", "southern" }, { "ben tre", "southern" },
            { "vĩnh long", "southern" }, { "vinh long", "southern" },
            { "cần thơ", "southern" }, { "can tho", "southern" },
            { "an giang", "southern" }, { "kiên giang", "southern" }, { "kien giang", "southern" },
            { "đồng tháp", "southern" }, { "dong thap", "southern" },
            { "sóc trăng", "southern" }, { "soc trang", "southern" },
            { "trà vinh", "southern" }, { "tra vinh", "southern" },
            { "bạc liêu", "southern" }, { "bac lieu", "southern" },
            { "cà mau", "southern" }, { "ca mau", "southern" },
            { "hậu giang", "southern" }, { "hau giang", "southern" },
            { "lâm đồng", "southern" }, { "lam dong", "southern" },
            { "đà lạt", "southern" }, { "da lat", "southern" },
            { "ninh thuận", "southern" }, { "ninh thuan", "southern" },
            { "bình thuận", "southern" }, { "binh thuan", "southern" },
            // Central region
            { "đà nẵng", "central" }, { "da nang", "central" },
            { "huế", "central" }, { "hue", "central" },
            { "thừa thiên", "central" }, { "thua thien", "central" },
            { "quảng nam", "central" }, { "quang nam", "central" },
            { "quảng ngãi", "central" }, { "quang ngai", "central" },
            { "bình định", "central" }, { "binh dinh", "central" },
            { "phú yên", "central" }, { "phu yen", "central" },
            { "khánh hòa", "central" }, { "khanh hoa", "central" },
            { "nha trang", "central" },
            { "gia lai", "central" }, { "kon tum", "central" },
            { "đắk lắk", "central" }, { "dak lak", "central" },
            { "đắk nông", "central" }, { "dak nong", "central" },
            { "quảng bình", "central" }, { "quang binh", "central" },
            { "quảng trị", "central" }, { "quang tri", "central" },
            { "hà tĩnh", "central" }, { "ha tinh", "central" },
            { "nghệ an", "central" }, { "nghe an", "central" },
            { "thanh hóa", "central" }, { "thanh hoa", "central" },
            // Northern region
            { "hà nội", "northern" }, { "ha noi", "northern" }, { "hanoi", "northern" },
            { "hải phòng", "northern" }, { "hai phong", "northern" },
            { "nam định", "northern" }, { "nam dinh", "northern" },
            { "ninh bình", "northern" }, { "ninh binh", "northern" },
            { "hải dương", "northern" }, { "hai duong", "northern" },
            { "hưng yên", "northern" }, { "hung yen", "northern" },
            { "thái bình", "northern" }, { "thai binh", "northern" },
            { "bắc ninh", "northern" }, { "bac ninh", "northern" },
            { "bắc giang", "northern" }, { "bac giang", "northern" },
            { "phú thọ", "northern" }, { "phu tho", "northern" },
            { "vĩnh phúc", "northern" }, { "vinh phuc", "northern" },
            { "quảng ninh", "northern" }, { "quang ninh", "northern" },
            { "lạng sơn", "northern" }, { "lang son", "northern" },
            { "cao bằng", "northern" }, { "cao bang", "northern" },
            { "bắc kạn", "northern" }, { "bac kan", "northern" },
            { "thái nguyên", "northern" }, { "thai nguyen", "northern" },
            { "tuyên quang", "northern" }, { "tuyen quang", "northern" },
            { "hà giang", "northern" }, { "ha giang", "northern" },
            { "lào cai", "northern" }, { "lao cai", "northern" },
            { "yên bái", "northern" }, { "yen bai", "northern" },
            { "lai châu", "northern" }, { "lai chau", "northern" },
            { "điện biên", "northern" }, { "dien bien", "northern" },
            { "sơn la", "northern" }, { "son la", "northern" },
            { "hòa bình", "northern" }, { "hoa binh", "northern" },
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
            _goShipToken = configuration["GoShip:Token"] ?? string.Empty;
            GoShipBaseUrl = configuration["GoShip:BaseUrl"] ?? "https://sandbox.goship.io/api/v2";
            _freeShippingThreshold = double.TryParse(configuration["Shipping:FreeShippingThreshold"], out var threshold)
                ? threshold
                : 89.0;
        }

        #region Shipping zone & fee logic

        /// <summary>
        /// Determines the shipping zone based on the shipping address string.
        /// The address format is: "[Name - Phone - Email] Street, City..."
        /// </summary>
        public string DetermineShippingZone(string? shippingAddress)
        {
            if (string.IsNullOrWhiteSpace(shippingAddress))
                return "southern"; // Default fallback

            var addressLower = shippingAddress.ToLower();

            // Try to match city/province names from the address
            foreach (var entry in CityZoneMap)
            {
                if (addressLower.Contains(entry.Key))
                    return entry.Value;
            }

            // Default to southern if no match (most orders are from southern Vietnam)
            return "southern";
        }

        /// <summary>
        /// Calculates shipping fee.
        /// Standard shipping: Always FREE (seller absorbs cost — competitive strategy for lightweight glasses).
        /// Express shipping: Zone-based fee via J&T Express.
        /// </summary>
        public double CalculateShippingFee(string shippingMethod, double orderSubtotal, string? shippingAddress = null)
        {
            var method = shippingMethod?.ToLower() ?? "standard";

            // Standard shipping is always free
            if (method != "express")
                return 0;

            // Express shipping: base fee + zone surcharge
            var baseFee = ShippingMethods.TryGetValue("express", out var info) ? info.baseFee : 2.0;
            var zone = DetermineShippingZone(shippingAddress);
            var surcharge = ZoneSurcharge.TryGetValue(zone, out var s) ? s : ZoneSurcharge["southern"];

            return baseFee + surcharge;
        }

        public List<ShippingMethodInfo> GetAvailableShippingMethods()
        {
            return ShippingMethods.Select(sm => new ShippingMethodInfo
            {
                Method = sm.Key,
                Fee = sm.Value.baseFee,
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

            // Calculate estimated delivery based on shipping method
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

        #region GoShip integration

        /// <summary>
        /// Get available shipping rates from GoShip, filtered to J&T Express only.
        /// </summary>
        public async Task<GoShipRateResponse?> GetRatesAsync(GoShipRateRequest request)
        {
            var client = CreateGoShipClient();
            var payload = new { shipment = request };

            var response = await client.PostAsJsonAsync($"{GoShipBaseUrl}/rates", payload);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GoShip GetRates failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                return null;
            }

            var result = JsonSerializer.Deserialize<GoShipRateResponse>(body);

            // Filter to J&T Express rates only
            if (result?.Data != null)
            {
                result.Data = result.Data
                    .Where(r => r.CarrierName != null &&
                                r.CarrierName.Contains("J&T", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return result;
        }

        /// <summary>
        /// Create a shipment on GoShip. The request must include a valid rate ID obtained from GetRatesAsync.
        /// </summary>
        public async Task<GoShipShipmentResponse?> CreateShipmentAsync(GoShipShipmentRequest request)
        {
            var client = CreateGoShipClient();
            var payload = new { shipment = request };

            var response = await client.PostAsJsonAsync($"{GoShipBaseUrl}/shipments", payload);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GoShip CreateShipment failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                return null;
            }

            return JsonSerializer.Deserialize<GoShipShipmentResponse>(body);
        }

        /// <summary>
        /// Get shipment details / tracking info from GoShip.
        /// </summary>
        public async Task<GoShipTrackingResponse?> GetShipmentAsync(string shipmentId)
        {
            var client = CreateGoShipClient();

            var response = await client.GetAsync($"{GoShipBaseUrl}/shipments/{shipmentId}");
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GoShip GetShipment failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
                return null;
            }

            return JsonSerializer.Deserialize<GoShipTrackingResponse>(body);
        }

        /// <summary>
        /// Create a GoShip shipment and automatically assign the tracking number to an order.
        /// </summary>
        public async Task<Order?> CreateShipmentForOrderAsync(Guid orderId, GoShipShipmentRequest request)
        {
            var shipmentResult = await CreateShipmentAsync(request);
            if (shipmentResult == null || shipmentResult.Code != 200)
                return null;

            var trackingNumber = shipmentResult.TrackingNumber;
            var carrier = shipmentResult.Carrier ?? DefaultCarrier;

            // GoShip sandbox may return "NULL" as a string for tracking_number
            if (string.IsNullOrEmpty(trackingNumber) || trackingNumber == "NULL")
                trackingNumber = shipmentResult.Id;

            return await AssignTrackingNumberAsync(orderId, trackingNumber, carrier);
        }

        /// <summary>
        /// Create a GoShip shipment and automatically assign the tracking number to a complaint.
        /// </summary>
        public async Task<ComplaintRequest?> CreateShipmentForComplaintAsync(Guid complaintId, GoShipShipmentRequest request)
        {
            var complaints = await _complaintRepository.SearchAsync(c => c.RequestId == complaintId);
            var complaint = complaints.FirstOrDefault();
            if (complaint == null) return null;

            var shipmentResult = await CreateShipmentAsync(request);
            if (shipmentResult == null || shipmentResult.Code != 200)
                return null;

            var trackingNumber = shipmentResult.TrackingNumber;
            var carrier = shipmentResult.Carrier ?? DefaultCarrier;

            // GoShip sandbox may return "NULL" as a string for tracking_number
            if (string.IsNullOrEmpty(trackingNumber) || trackingNumber == "NULL")
                trackingNumber = shipmentResult.Id;

            complaint.ReturnTrackingNumber = trackingNumber;
            complaint.ReturnShippingCarrier = carrier;
            return await _complaintRepository.UpdateAsync(complaint);
        }

        public async Task<List<GoShipCity>> GetCitiesAsync()
        {
            using var client = CreateGoShipClient();
            var response = await client.GetAsync($"{GoShipBaseUrl}/cities");
            if (!response.IsSuccessStatusCode) return [];

            var result = await response.Content.ReadFromJsonAsync<GoShipLocationResponse<GoShipCity>>();
            return result?.Data ?? [];
        }

        public async Task<List<GoShipDistrict>> GetDistrictsAsync(string cityId)
        {
            using var client = CreateGoShipClient();
            var response = await client.GetAsync($"{GoShipBaseUrl}/cities/{Uri.EscapeDataString(cityId)}/districts");
            if (!response.IsSuccessStatusCode) return [];

            var result = await response.Content.ReadFromJsonAsync<GoShipLocationResponse<GoShipDistrict>>();
            return result?.Data ?? [];
        }

        public async Task<List<GoShipWard>> GetWardsAsync(string districtId)
        {
            using var client = CreateGoShipClient();
            var response = await client.GetAsync($"{GoShipBaseUrl}/districts/{Uri.EscapeDataString(districtId)}/wards");
            if (!response.IsSuccessStatusCode) return [];

            var result = await response.Content.ReadFromJsonAsync<GoShipLocationResponse<GoShipWard>>();
            return result?.Data ?? [];
        }

        private HttpClient CreateGoShipClient()
        {
            var client = _httpClientFactory.CreateClient("GoShip");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _goShipToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        #endregion
    }
}
