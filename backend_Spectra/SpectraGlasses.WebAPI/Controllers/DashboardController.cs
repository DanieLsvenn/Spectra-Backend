using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// Gets overall business statistics (Manager/Admin)
        /// </summary>
        [HttpGet("statistics")]
        [Authorize(Roles = "manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetStatistics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var statistics = await _dashboardService.GetStatisticsAsync(startDate, endDate);
            return Ok(statistics);
        }

        /// <summary>
        /// Gets daily revenue report (Manager/Admin)
        /// </summary>
        [HttpGet("revenue/daily")]
        [Authorize(Roles = "manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetDailyRevenue(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            // Default to last 30 days
            var end = endDate ?? DateTime.UtcNow;
            var start = startDate ?? end.AddDays(-30);

            if (start > end)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Start date must be before end date"
                });
            }

            var revenue = await _dashboardService.GetDailyRevenueAsync(start, end);
            return Ok(revenue);
        }

        /// <summary>
        /// Gets monthly revenue report for a year (Manager/Admin)
        /// </summary>
        [HttpGet("revenue/monthly")]
        [Authorize(Roles = "manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMonthlyRevenue([FromQuery] int? year = null)
        {
            var targetYear = year ?? DateTime.UtcNow.Year;
            var revenue = await _dashboardService.GetMonthlyRevenueAsync(targetYear);
            return Ok(revenue);
        }

        /// <summary>
        /// Gets popular frames (Manager/Admin)
        /// </summary>
        [HttpGet("popular-frames")]
        [Authorize(Roles = "manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPopularFrames(
            [FromQuery] int limit = 10,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            if (limit < 1) limit = 10;
            if (limit > 50) limit = 50;

            var popularFrames = await _dashboardService.GetPopularFramesAsync(limit, startDate, endDate);
            return Ok(popularFrames);
        }

        /// <summary>
        /// Gets order summary (today, week, month) (Staff/Manager/Admin)
        /// </summary>
        [HttpGet("orders/summary")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOrderSummary()
        {
            var summary = await _dashboardService.GetOrderSummaryAsync();
            return Ok(summary);
        }
    }
}
