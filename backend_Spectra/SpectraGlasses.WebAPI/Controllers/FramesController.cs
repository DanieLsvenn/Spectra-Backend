using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.ModelExtensions;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FramesController : ControllerBase
    {
        private readonly IFrameService _frameService;
        private readonly IPreorderCampaignService _campaignService;

        public FramesController(IFrameService frameService, IPreorderCampaignService campaignService)
        {
            _frameService = frameService;
            _campaignService = campaignService;
        }

        #region Public Endpoints (No Authorization)

        /// <summary>
        /// Gets all available frames with pagination
        /// </summary>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Number of items per page (default: 10)</param>
        /// <returns>Paginated list of available frames</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFrames([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50; // Limit max page size

            var result = await _frameService.GetAvailableFramesAsync(page, pageSize);

            // Enrich frames with preorder info for out-of-stock frames in active campaigns
            var activeCampaignFrameInfo = await _campaignService.GetActiveCampaignFrameInfoAsync();
            var enrichedItems = result.Items
                .Select(f => MapToFrameWithPreorderResponse(f, activeCampaignFrameInfo))
                .ToList();

            return Ok(new PaginationResult<FrameWithPreorderResponse>
            {
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages,
                CurrentPage = result.CurrentPage,
                PageSize = result.PageSize,
                Items = enrichedItems
            });
        }

        /// <summary>
        /// Gets all frames (manager/admin only), including inactive/out_of_stock
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllFrames([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _frameService.GetAllFramesAsync(page, pageSize);
            var activeCampaignFrameInfo = await _campaignService.GetActiveCampaignFrameInfoAsync();
            var enrichedItems = result.Items
                .Select(f => MapToFrameWithPreorderResponse(f, activeCampaignFrameInfo))
                .ToList();

            return Ok(new PaginationResult<FrameWithPreorderResponse>
            {
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages,
                CurrentPage = result.CurrentPage,
                PageSize = result.PageSize,
                Items = enrichedItems
            });
        }

        /// <summary>
        /// Gets a specific frame by ID
        /// </summary>
        /// <param name="id">The frame ID</param>
        /// <returns>Frame details including media</returns>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFrameById(Guid id)
        {
            var frame = await _frameService.GetFrameByIdAsync(id);

            if (frame == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "FRAME_NOT_FOUND",
                    Message = "Frame not found or is not available"
                });
            }

            // Enrich frame with preorder info if out of stock and in an active campaign
            var activeCampaignFrameInfo = await _campaignService.GetActiveCampaignFrameInfoAsync();
            var enrichedFrame = MapToFrameWithPreorderResponse(frame, activeCampaignFrameInfo);

            return Ok(enrichedFrame);
        }

        /// <summary>
        /// Gets all media (images/videos) for a specific frame
        /// </summary>
        /// <param name="id">The frame ID</param>
        /// <returns>List of media items for the frame</returns>
        [HttpGet("{id:guid}/media")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFrameMedia(Guid id)
        {
            var media = await _frameService.GetFrameMediaAsync(id);

            if (media.Count == 0)
            {
                // Check if frame exists but has no media, or frame doesn't exist
                var frame = await _frameService.GetFrameByIdAsync(id);
                if (frame == null)
                {
                    return NotFound(new ErrorResponse
                    {
                        ErrorCode = "FRAME_NOT_FOUND",
                        Message = "Frame not found or is not available"
                    });
                }
            }

            return Ok(media);
        }

        /// <summary>
        /// Gets the supported lens types for a specific frame.
        /// Single Vision and Non-Prescription lens types are always available.
        /// </summary>
        /// <param name="id">The frame ID</param>
        /// <returns>List of supported lens types for the frame</returns>
        [HttpGet("{id:guid}/lens-types")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFrameSupportedLensTypes(Guid id)
        {
            var frame = await _frameService.GetFrameByIdAsync(id);
            if (frame == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "FRAME_NOT_FOUND",
                    Message = "Frame not found or is not available"
                });
            }

            var supportedLensTypes = await _frameService.GetSupportedLensTypesAsync(id);
            return Ok(new
            {
                FrameId = id,
                FrameName = frame.FrameName,
                MinRx = frame.MinRx,
                MaxRx = frame.MaxRx,
                MinPd = frame.MinPd,
                MaxPd = frame.MaxPd,
                SupportedLensTypes = supportedLensTypes
            });
        }

        #endregion

        #region Manager Endpoints (Authorization Required)

        /// <summary>
        /// Creates a new frame (Manager only)
        /// </summary>
        /// <param name="request">Frame creation request</param>
        /// <returns>Created frame details</returns>
        [HttpPost]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateFrame([FromBody] CreateFrameRequest request)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.FrameName))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Frame name is required"
                });
            }

            // Validate size attributes
            var validationResult = _frameService.ValidateFrameSizeAttributes(
                request.LensWidth,
                request.BridgeWidth,
                request.FrameWidth,
                request.TempleLength
            );

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = string.Join("; ", validationResult.Errors)
                });
            }

            // Calculate initial stock from color variants if provided
            int initialStock = request.StockQuantity ?? 0;
            if (request.ColorVariants != null && request.ColorVariants.Count > 0)
            {
                initialStock = request.ColorVariants.Sum(cv => cv.StockQuantity);
            }

            var frame = new Frame
            {
                FrameName = request.FrameName,
                BrandId = request.BrandId,
                MaterialId = request.MaterialId,
                ShapeId = request.ShapeId,
                LensWidth = request.LensWidth,
                BridgeWidth = request.BridgeWidth,
                FrameWidth = request.FrameWidth,
                TempleLength = request.TempleLength,
                Size = request.Size,
                BasePrice = request.BasePrice,
                StockQuantity = initialStock,
                ReorderLevel = request.ReorderLevel ?? 5,
                MinRx = request.MinRx,
                MaxRx = request.MaxRx,
                MinPd = request.MinPd,
                MaxPd = request.MaxPd
            };

            var createdFrame = await _frameService.CreateFrameAsync(frame);

            // Set frame color variants with per-color stock
            if (request.ColorVariants != null && request.ColorVariants.Count > 0)
            {
                var colorInputs = request.ColorVariants.Select(cv => new FrameColorInput
                {
                    ColorId = cv.ColorId,
                    StockQuantity = cv.StockQuantity,
                    ColorExtraCost = cv.ColorExtraCost
                }).ToList();
                await _frameService.SetFrameColorsAsync(createdFrame.FrameId, colorInputs);
            }

            // Set supported lens types if provided
            if (request.SupportedLensTypeIds != null && request.SupportedLensTypeIds.Count > 0)
            {
                await _frameService.SetFrameLensTypesAsync(createdFrame.FrameId, request.SupportedLensTypeIds);
            }

            // Re-fetch with full includes
            var result = await _frameService.GetFrameByIdForManagerAsync(createdFrame.FrameId);

            return CreatedAtAction(
                nameof(GetFrameById),
                new { id = createdFrame.FrameId },
                result
            );
        }

        /// <summary>
        /// Updates an existing frame (Manager only)
        /// </summary>
        /// <param name="id">The frame ID</param>
        /// <param name="request">Frame update request</param>
        /// <returns>Updated frame details</returns>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateFrame(Guid id, [FromBody] UpdateFrameRequest request)
        {
            // Validate size attributes if provided
            var validationResult = _frameService.ValidateFrameSizeAttributes(
                request.LensWidth,
                request.BridgeWidth,
                request.FrameWidth,
                request.TempleLength
            );

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = string.Join("; ", validationResult.Errors)
                });
            }

            // Validate status if provided
            if (!string.IsNullOrEmpty(request.Status))
            {
                var validStatuses = new[] { "available", "inactive", "out_of_stock" };
                if (!validStatuses.Contains(request.Status.ToLower()))
                {
                    return BadRequest(new ErrorResponse
                    {
                        ErrorCode = "VALIDATION_ERROR",
                        Message = $"Invalid status. Allowed values: {string.Join(", ", validStatuses)}"
                    });
                }
            }

            var updatedFrame = new Frame
            {
                FrameName = request.FrameName,
                BrandId = request.BrandId,
                MaterialId = request.MaterialId,
                ShapeId = request.ShapeId,
                LensWidth = request.LensWidth,
                BridgeWidth = request.BridgeWidth,
                FrameWidth = request.FrameWidth,
                TempleLength = request.TempleLength,
                Size = request.Size,
                BasePrice = request.BasePrice,
                Status = request.Status,
                StockQuantity = request.StockQuantity,
                ReorderLevel = request.ReorderLevel,
                MinRx = request.MinRx,
                MaxRx = request.MaxRx,
                MinPd = request.MinPd,
                MaxPd = request.MaxPd
            };

            var result = await _frameService.UpdateFrameAsync(id, updatedFrame);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "FRAME_NOT_FOUND",
                    Message = "Frame not found"
                });
            }

            // Update frame color variants if provided
            if (request.ColorVariants != null)
            {
                var colorInputs = request.ColorVariants.Select(cv => new FrameColorInput
                {
                    ColorId = cv.ColorId,
                    StockQuantity = cv.StockQuantity,
                    ColorExtraCost = cv.ColorExtraCost
                }).ToList();
                await _frameService.SetFrameColorsAsync(id, colorInputs);
            }

            // Update supported lens types if provided
            if (request.SupportedLensTypeIds != null)
            {
                await _frameService.SetFrameLensTypesAsync(id, request.SupportedLensTypeIds);
            }

            // Re-fetch with full includes
            var fullResult = await _frameService.GetFrameByIdForManagerAsync(id);

            return Ok(fullResult);
        }

        /// <summary>
        /// Soft deletes a frame by setting status to inactive (Manager only)
        /// </summary>
        /// <param name="id">The frame ID</param>
        /// <returns>No content response</returns>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteFrame(Guid id)
        {
            var result = await _frameService.SoftDeleteFrameAsync(id);

            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "FRAME_NOT_FOUND",
                    Message = "Frame not found"
                });
            }

            return NoContent();
        }

        /// <summary>
        /// Gets frames with low stock (Manager only)
        /// </summary>
        [HttpGet("inventory/low-stock")]
        [Authorize(Roles = "manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetLowStockFrames()
        {
            var frames = await _frameService.GetLowStockFramesAsync();
            return Ok(frames);
        }

        /// <summary>
        /// Gets frames that are out of stock (Manager only)
        /// </summary>
        [HttpGet("inventory/out-of-stock")]
        [Authorize(Roles = "manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOutOfStockFrames()
        {
            var frames = await _frameService.GetOutOfStockFramesAsync();
            return Ok(frames);
        }

        /// <summary>
        /// Updates stock quantity for a frame (Manager only)
        /// </summary>
        [HttpPatch("{id:guid}/inventory")]
        [Authorize(Roles = "manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateStock(Guid id, [FromBody] UpdateStockRequest request)
        {
            if (request.Quantity < 0)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Stock quantity cannot be negative"
                });
            }

            var frame = await _frameService.GetFrameByIdForManagerAsync(id);
            if (frame == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "FRAME_NOT_FOUND",
                    Message = "Frame not found"
                });
            }

            var updatedFrame = new Frame
            {
                StockQuantity = request.Quantity,
                ReorderLevel = request.ReorderLevel
            };

            var result = await _frameService.UpdateFrameAsync(id, updatedFrame);
            return Ok(result);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Maps a Frame entity to a FrameWithPreorderResponse, attaching preorderInfo
        /// when the frame is out of stock and belongs to an active campaign.
        /// </summary>
        private static FrameWithPreorderResponse MapToFrameWithPreorderResponse(
            Frame frame,
            Dictionary<Guid, PreorderInfoDto> activeCampaignFrameInfo)
        {
            var isOutOfStock = (frame.StockQuantity ?? 0) <= 0
                || string.Equals(frame.Status, "out_of_stock", StringComparison.OrdinalIgnoreCase);

            PreorderInfoDto? preorderInfo = null;
            if (isOutOfStock && activeCampaignFrameInfo.TryGetValue(frame.FrameId, out var info))
            {
                preorderInfo = info;
            }

            return new FrameWithPreorderResponse
            {
                FrameId = frame.FrameId,
                FrameName = frame.FrameName,
                BrandId = frame.BrandId,
                MaterialId = frame.MaterialId,
                ShapeId = frame.ShapeId,
                LensWidth = frame.LensWidth,
                BridgeWidth = frame.BridgeWidth,
                FrameWidth = frame.FrameWidth,
                TempleLength = frame.TempleLength,
                Size = frame.Size,
                BasePrice = frame.BasePrice,
                Status = frame.Status,
                StockQuantity = frame.StockQuantity,
                ReorderLevel = frame.ReorderLevel,
                MinRx = frame.MinRx,
                MaxRx = frame.MaxRx,
                MinPd = frame.MinPd,
                MaxPd = frame.MaxPd,
                Brand = frame.Brand,
                Material = frame.Material,
                Shape = frame.Shape,
                FrameColors = frame.FrameColors,
                FrameMedia = frame.FrameMedia,
                FrameLensTypes = frame.FrameLensTypes,
                PreorderInfo = preorderInfo
            };
        }

        #endregion
    }
}
