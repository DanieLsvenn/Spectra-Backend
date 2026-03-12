using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PreorderCampaignsController : ControllerBase
    {
        private readonly IPreorderCampaignService _campaignService;

        public PreorderCampaignsController(IPreorderCampaignService campaignService)
        {
            _campaignService = campaignService;
        }

        #region Public Endpoints

        /// <summary>
        /// Gets all preorder campaigns (upcoming, active, ended)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllCampaigns()
        {
            var campaigns = await _campaignService.GetAllCampaignsAsync();
            var result = campaigns.Select(MapCampaignResponse).ToList();
            return Ok(result);
        }

        /// <summary>
        /// Gets all active preorder campaigns
        /// </summary>
        [HttpGet("active")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActiveCampaigns()
        {
            var campaigns = await _campaignService.GetActiveCampaignsAsync();
            var result = campaigns.Select(MapCampaignResponse).ToList();
            return Ok(result);
        }

        /// <summary>
        /// Gets the list of possible campaign statuses
        /// </summary>
        [HttpGet("statuses")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetCampaignStatuses()
        {
            var statuses = new[]
            {
                new { Value = "upcoming", Description = "Campaign has not started yet" },
                new { Value = "active", Description = "Campaign is currently running" },
                new { Value = "ended", Description = "Campaign has ended" }
            };
            return Ok(statuses);
        }

        /// <summary>
        /// Gets a specific campaign by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCampaignById(Guid id)
        {
            var campaign = await _campaignService.GetCampaignByIdAsync(id);
            if (campaign == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "CAMPAIGN_NOT_FOUND",
                    Message = "Campaign not found"
                });
            }
            return Ok(MapCampaignResponse(campaign));
        }

        #endregion

        #region Manager Endpoints

        /// <summary>
        /// Creates a new preorder campaign (Manager only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CampaignName))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Campaign name is required"
                });
            }

            if (request.StartDate >= request.EndDate)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Start date must be before end date"
                });
            }

            if (request.Frames == null || !request.Frames.Any())
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "At least one frame is required for a campaign"
                });
            }

            var campaign = new PreorderCampaign
            {
                CampaignName = request.CampaignName,
                Description = request.Description,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                MaxSlots = request.MaxSlots,
                EstimatedDeliveryDate = request.EstimatedDeliveryDate
            };

            var frames = request.Frames.Select(f => new CampaignFrame
            {
                FrameId = f.FrameId,
                CampaignPrice = f.CampaignPrice,
                MaxQuantityPerOrder = f.MaxQuantityPerOrder > 0 ? f.MaxQuantityPerOrder : 2
            }).ToList();

            try
            {
                var created = await _campaignService.CreateCampaignAsync(campaign, frames);
                return CreatedAtAction(nameof(GetCampaignById), new { id = created.CampaignId }, MapCampaignResponse(created));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// Updates an existing campaign (Manager only)
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCampaign(Guid id, [FromBody] UpdateCampaignRequest request)
        {
            var updates = new PreorderCampaign
            {
                CampaignName = request.CampaignName,
                Description = request.Description,
                MaxSlots = request.MaxSlots,
                EstimatedDeliveryDate = request.EstimatedDeliveryDate
            };
            var result = await _campaignService.UpdateCampaignAsync(id, updates);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "CAMPAIGN_NOT_FOUND",
                    Message = "Campaign not found"
                });
            }
            return Ok(MapCampaignResponse(result));
        }

        /// <summary>
        /// Ends a campaign (Manager only)
        /// </summary>
        [HttpPatch("{id:guid}/end")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EndCampaign(Guid id)
        {
            var result = await _campaignService.EndCampaignAsync(id);
            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "CAMPAIGN_NOT_FOUND",
                    Message = "Campaign not found"
                });
            }
            return Ok(new { Message = "Campaign ended successfully" });
        }

        #endregion

        #region Response Mapping

        private static object MapCampaignResponse(PreorderCampaign campaign)
        {
            return new
            {
                campaign.CampaignId,
                campaign.CampaignName,
                campaign.Description,
                campaign.StartDate,
                campaign.EndDate,
                campaign.MaxSlots,
                campaign.CurrentSlots,
                campaign.Status,
                campaign.EstimatedDeliveryDate,
                campaign.CreatedAt,
                Frames = campaign.CampaignFrames?.Select(cf => new
                {
                    cf.CampaignFrameId,
                    cf.FrameId,
                    cf.CampaignPrice,
                    cf.MaxQuantityPerOrder,
                    FrameName = cf.Frame?.FrameName,
                    FrameBasePrice = cf.Frame?.BasePrice,
                    FrameStatus = cf.Frame?.Status
                }).ToList()
            };
        }

        #endregion
    }
}
