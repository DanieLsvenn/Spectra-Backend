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

        [JsonPropertyName("data")]
        public GoShipShipmentData? Data { get; set; }
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

    #endregion

    public interface IShippingService
    {
        // Local helpers (kept for backward compatibility)
        double CalculateShippingFee(string shippingMethod, double orderSubtotal);
        List<ShippingMethodInfo> GetAvailableShippingMethods();
        Task<Order?> AssignTrackingNumberAsync(Guid orderId, string trackingNumber, string carrier);

        // GoShip integration
        Task<GoShipRateResponse?> GetRatesAsync(GoShipRateRequest request);
        Task<GoShipShipmentResponse?> CreateShipmentAsync(GoShipShipmentRequest request);
        Task<GoShipTrackingResponse?> GetShipmentAsync(string shipmentId);
        Task<Order?> CreateShipmentForOrderAsync(Guid orderId, GoShipShipmentRequest request);
    }

    public class ShippingService : IShippingService
    {
        private readonly GenericRepository<Order> _orderRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ShippingService> _logger;
        private readonly string _goShipToken;
        private readonly double _freeShippingThreshold;

        private const string GoShipBaseUrl = "https://sandbox.goship.io/api/v2";

        private static readonly Dictionary<string, (double fee, string desc)> ShippingMethods = new()
        {
            { "standard", (5.0, "Standard Shipping (5-7 business days)") },
            { "express", (15.0, "Express Shipping (2-3 business days)") },
            { "free", (0.0, "Free Shipping") }
        };

        public ShippingService(
            GenericRepository<Order> orderRepository,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ShippingService> logger)
        {
            _orderRepository = orderRepository;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _goShipToken = configuration["GoShip:Token"] ?? string.Empty;
            _freeShippingThreshold = double.TryParse(configuration["Shipping:FreeShippingThreshold"], out var threshold)
                ? threshold
                : 89.0;
        }

        #region Local helpers (backward compatible)

        public double CalculateShippingFee(string shippingMethod, double orderSubtotal)
        {
            if (orderSubtotal >= _freeShippingThreshold)
                return 0;

            var method = shippingMethod?.ToLower() ?? "standard";
            return ShippingMethods.TryGetValue(method, out var info) ? info.fee : ShippingMethods["standard"].fee;
        }

        public List<ShippingMethodInfo> GetAvailableShippingMethods()
        {
            return ShippingMethods.Select(sm => new ShippingMethodInfo
            {
                Method = sm.Key,
                Fee = sm.Value.fee,
                Description = sm.Value.desc
            }).ToList();
        }

        public async Task<Order?> AssignTrackingNumberAsync(Guid orderId, string trackingNumber, string carrier)
        {
            var orders = await _orderRepository.SearchAsync(o => o.OrderId == orderId);
            var order = orders.FirstOrDefault();
            if (order == null) return null;

            order.TrackingNumber = trackingNumber;
            order.ShippingCarrier = carrier;
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
        /// Get available shipping rates from GoShip for the given addresses and parcel.
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

            return JsonSerializer.Deserialize<GoShipRateResponse>(body);
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
            if (shipmentResult?.Data == null)
                return null;

            var trackingNumber = shipmentResult.Data.TrackingNumber;
            var carrier = shipmentResult.Data.Carrier;

            if (string.IsNullOrEmpty(trackingNumber))
                trackingNumber = shipmentResult.Data.Id;

            return await AssignTrackingNumberAsync(orderId, trackingNumber, carrier);
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
