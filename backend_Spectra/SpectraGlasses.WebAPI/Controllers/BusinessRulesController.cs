using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.GlassesService;
using System.Security.Claims;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BusinessRulesController : ControllerBase
    {
        private readonly IBusinessRuleService _ruleService;

        public BusinessRulesController(IBusinessRuleService ruleService)
        {
            _ruleService = ruleService;
        }

        /// <summary>
        /// Gets all business rules (Manager/Admin)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "manager,admin")]
        public async Task<IActionResult> GetAllRules()
        {
            var rules = await _ruleService.GetAllRulesAsync();
            return Ok(rules);
        }

        /// <summary>
        /// Gets business rules by category (Manager/Admin)
        /// </summary>
        [HttpGet("category/{category}")]
        [Authorize(Roles = "manager,admin")]
        public async Task<IActionResult> GetRulesByCategory(string category)
        {
            var rules = await _ruleService.GetRulesByCategoryAsync(category);
            return Ok(rules);
        }

        /// <summary>
        /// Gets a single business rule by key (Manager/Admin)
        /// </summary>
        [HttpGet("{ruleKey}")]
        [Authorize(Roles = "manager,admin")]
        public async Task<IActionResult> GetRuleByKey(string ruleKey)
        {
            var rule = await _ruleService.GetRuleByKeyAsync(ruleKey);
            if (rule == null) return NotFound(new { message = $"Rule '{ruleKey}' not found." });
            return Ok(rule);
        }

        /// <summary>
        /// Updates a business rule value (Manager/Admin)
        /// </summary>
        [HttpPut("{ruleKey}")]
        [Authorize(Roles = "manager,admin")]
        public async Task<IActionResult> UpdateRule(string ruleKey, [FromBody] UpdateRuleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Value))
                return BadRequest(new { message = "Value is required." });

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var updated = await _ruleService.UpdateRuleAsync(ruleKey, request.Value, userId);
            if (updated == null)
                return NotFound(new { message = $"Rule '{ruleKey}' not found." });

            return Ok(updated);
        }

        /// <summary>
        /// Gets public business rules (shipping, exchange rate) for frontend display.
        /// No authentication required.
        /// </summary>
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicRules()
        {
            var shipping = await _ruleService.GetRulesByCategoryAsync("shipping");
            var exchange = await _ruleService.GetRulesByCategoryAsync("exchange");
            var complaint = await _ruleService.GetRulesByCategoryAsync("complaint");

            var result = new Dictionary<string, string>();
            foreach (var rule in shipping.Concat(exchange).Concat(complaint))
            {
                result[rule.RuleKey] = rule.RuleValue;
            }
            return Ok(result);
        }
    }

    public class UpdateRuleRequest
    {
        public string Value { get; set; } = string.Empty;
    }
}
