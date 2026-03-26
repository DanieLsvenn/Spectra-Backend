using Microsoft.AspNetCore.Mvc;
using Services.GlassesService;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExchangeRateController : ControllerBase
    {
        private readonly IExchangeRateService _exchangeRateService;

        public ExchangeRateController(IExchangeRateService exchangeRateService)
        {
            _exchangeRateService = exchangeRateService;
        }

        /// <summary>
        /// Gets the current USD to VND exchange rate (cached for 1 hour)
        /// </summary>
        [HttpGet("usd-vnd")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsdToVndRate()
        {
            var rate = await _exchangeRateService.GetUsdToVndRateAsync();
            return Ok(new { rate, currency = "VND", baseCurrency = "USD" });
        }
    }
}
