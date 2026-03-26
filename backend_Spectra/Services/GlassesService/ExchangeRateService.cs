using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Services.GlassesService
{
    public interface IExchangeRateService
    {
        Task<double> GetUsdToVndRateAsync();
    }

    public class ExchangeRateService : IExchangeRateService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ExchangeRateService> _logger;

        // Cache the rate for 1 hour
        private static double _cachedRate;
        private static DateTime _cacheExpiry = DateTime.MinValue;
        private static readonly object _cacheLock = new();

        private const double FallbackRate = 25400;
        private const string ExchangeApiUrl = "https://open.er-api.com/v6/latest/USD";

        public ExchangeRateService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ExchangeRateService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<double> GetUsdToVndRateAsync()
        {
            // Return cached rate if still valid
            lock (_cacheLock)
            {
                if (DateTime.UtcNow < _cacheExpiry && _cachedRate > 0)
                {
                    return _cachedRate;
                }
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetAsync(ExchangeApiUrl);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var root = doc.RootElement;
                if (root.TryGetProperty("rates", out var rates) &&
                    rates.TryGetProperty("VND", out var vndRate))
                {
                    var newRate = vndRate.GetDouble();
                    if (newRate > 0)
                    {
                        lock (_cacheLock)
                        {
                            _cachedRate = newRate;
                            _cacheExpiry = DateTime.UtcNow.AddHours(1);
                        }

                        _logger.LogInformation("Exchange rate updated: 1 USD = {Rate} VND", newRate);
                        return newRate;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch exchange rate from API, using fallback rate");
            }

            // Fallback: use config value or default
            var configRate = _configuration.GetSection("VnPay")["UsdToVndRate"];
            if (double.TryParse(configRate, out var fallback) && fallback > 0)
            {
                return fallback;
            }

            return FallbackRate;
        }
    }
}
