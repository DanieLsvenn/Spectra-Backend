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
        private readonly IBusinessRuleService _businessRuleService;
        private readonly ILogger<ExchangeRateService> _logger;

        // Cache the rate for 1 hour
        private static double _cachedRate;
        private static DateTime _cacheExpiry = DateTime.MinValue;
        private static readonly object _cacheLock = new();

        private const double FallbackRate = 25400;
        private const string ExchangeApiUrl = "https://open.er-api.com/v6/latest/USD";
        private const string ExchangeRuleKey = "exchange_rate.usd_to_vnd";
        private const string LegacyExchangeRuleKey = "exchange.usd_vnd_fallback";

        public ExchangeRateService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IBusinessRuleService businessRuleService,
            ILogger<ExchangeRateService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _businessRuleService = businessRuleService;
            _logger = logger;
        }

        private void SetCachedRate(double rate)
        {
            lock (_cacheLock)
            {
                _cachedRate = rate;
                _cacheExpiry = DateTime.UtcNow.AddHours(1);
            }
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

            // Fallback: business rule value > config value > constant
            var ruleRate = await _businessRuleService.GetRuleValueAsDoubleAsync(ExchangeRuleKey, 0);
            if (ruleRate > 0)
            {
                SetCachedRate(ruleRate);
                _logger.LogInformation("Using exchange rate from business rule '{RuleKey}': {Rate}", ExchangeRuleKey, ruleRate);
                return ruleRate;
            }

            var legacyRuleRate = await _businessRuleService.GetRuleValueAsDoubleAsync(LegacyExchangeRuleKey, 0);
            if (legacyRuleRate > 0)
            {
                SetCachedRate(legacyRuleRate);
                _logger.LogInformation("Using legacy exchange rate from business rule '{RuleKey}': {Rate}", LegacyExchangeRuleKey, legacyRuleRate);
                return legacyRuleRate;
            }

            var configRate = _configuration.GetSection("VnPay")["UsdToVndRate"];
            if (double.TryParse(configRate, out var fallback) && fallback > 0)
            {
                SetCachedRate(fallback);
                _logger.LogInformation("Using exchange rate from VnPay config: {Rate}", fallback);
                return fallback;
            }

            SetCachedRate(FallbackRate);
            _logger.LogInformation("Using hardcoded fallback exchange rate: {Rate}", FallbackRate);
            return FallbackRate;
        }
    }
}
