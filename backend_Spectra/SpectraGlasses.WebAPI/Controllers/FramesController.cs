using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public FramesController(IFrameService frameService)
        {
            _frameService = frameService;
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

            return Ok(result);
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

            return Ok(frame);
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

            var frame = new Frame
            {
                FrameName = request.FrameName,
                Brand = request.Brand,
                Color = request.Color,
                Material = request.Material,
                LensWidth = request.LensWidth,
                BridgeWidth = request.BridgeWidth,
                FrameWidth = request.FrameWidth,
                TempleLength = request.TempleLength,
                Shape = request.Shape,
                Size = request.Size,
                BasePrice = request.BasePrice
            };

            var createdFrame = await _frameService.CreateFrameAsync(frame);

            return CreatedAtAction(
                nameof(GetFrameById),
                new { id = createdFrame.FrameId },
                createdFrame
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
                Brand = request.Brand,
                Color = request.Color,
                Material = request.Material,
                LensWidth = request.LensWidth,
                BridgeWidth = request.BridgeWidth,
                FrameWidth = request.FrameWidth,
                TempleLength = request.TempleLength,
                Shape = request.Shape,
                Size = request.Size,
                BasePrice = request.BasePrice,
                Status = request.Status
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

            return Ok(result);
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

        #endregion
    }
}
