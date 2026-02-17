using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FrameMediaController : ControllerBase
    {
        private readonly IFrameMediaService _frameMediaService;
        private readonly IFrameService _frameService;

        public FrameMediaController(
            IFrameMediaService frameMediaService,
            IFrameService frameService)
        {
            _frameMediaService = frameMediaService;
            _frameService = frameService;
        }

        private FrameMediaResponse MapToResponse(FrameMedium media)
        {
            return new FrameMediaResponse
            {
                MediaId = media.MediaId,
                FrameId = media.FrameId,
                MediaUrl = media.MediaUrl,
                MediaType = media.MediaType
            };
        }

        #region Public Endpoints

        /// <summary>
        /// Gets all media for a specific frame
        /// </summary>
        [HttpGet("frame/{frameId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMediaByFrameId(Guid frameId)
        {
            // Check if frame exists
            var frame = await _frameService.GetFrameByIdAsync(frameId);
            if (frame == null)
            {
                // Also check for manager access (inactive frames)
                frame = await _frameService.GetFrameByIdForManagerAsync(frameId);
                if (frame == null)
                {
                    return NotFound(new ErrorResponse
                    {
                        ErrorCode = "FRAME_NOT_FOUND",
                        Message = "Frame not found"
                    });
                }
            }

            var mediaList = await _frameMediaService.GetMediaByFrameIdAsync(frameId);
            var response = mediaList.Select(MapToResponse).ToList();

            return Ok(response);
        }

        /// <summary>
        /// Gets a specific media item by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(FrameMediaResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMediaById(Guid id)
        {
            var media = await _frameMediaService.GetMediaByIdAsync(id);

            if (media == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "MEDIA_NOT_FOUND",
                    Message = "Media not found"
                });
            }

            return Ok(MapToResponse(media));
        }

        #endregion

        #region Manager Endpoints

        /// <summary>
        /// Adds a new media item to a frame (Manager only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(typeof(FrameMediaResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AddMedia([FromBody] AddFrameMediaRequest request)
        {
            // Validate frame exists
            var frame = await _frameService.GetFrameByIdForManagerAsync(request.FrameId);
            if (frame == null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "FRAME_NOT_FOUND",
                    Message = "Frame not found"
                });
            }

            // Validate media URL
            if (string.IsNullOrWhiteSpace(request.MediaUrl))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Media URL is required"
                });
            }

            // Validate media type
            if (!_frameMediaService.IsValidMediaType(request.MediaType))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid media type. Allowed: image, video, thumbnail, gallery"
                });
            }

            var media = new FrameMedium
            {
                FrameId = request.FrameId,
                MediaUrl = request.MediaUrl,
                MediaType = request.MediaType.ToLower()
            };

            var createdMedia = await _frameMediaService.AddMediaAsync(media);

            return CreatedAtAction(
                nameof(GetMediaById),
                new { id = createdMedia.MediaId },
                MapToResponse(createdMedia)
            );
        }

        /// <summary>
        /// Adds multiple media items to a frame (Manager only)
        /// </summary>
        [HttpPost("batch")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AddMultipleMedia([FromBody] AddMultipleFrameMediaRequest request)
        {
            // Validate frame exists
            var frame = await _frameService.GetFrameByIdForManagerAsync(request.FrameId);
            if (frame == null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "FRAME_NOT_FOUND",
                    Message = "Frame not found"
                });
            }

            if (request.MediaItems == null || !request.MediaItems.Any())
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "At least one media item is required"
                });
            }

            // Validate all media items
            var errors = new List<string>();
            for (int i = 0; i < request.MediaItems.Count; i++)
            {
                var item = request.MediaItems[i];
                
                if (string.IsNullOrWhiteSpace(item.MediaUrl))
                {
                    errors.Add($"Item {i + 1}: Media URL is required");
                }

                if (!string.IsNullOrEmpty(item.MediaType) && !_frameMediaService.IsValidMediaType(item.MediaType))
                {
                    errors.Add($"Item {i + 1}: Invalid media type");
                }
            }

            if (errors.Any())
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = string.Join("; ", errors)
                });
            }

            var mediaList = request.MediaItems.Select(item => new FrameMedium
            {
                MediaUrl = item.MediaUrl,
                MediaType = item.MediaType?.ToLower() ?? "image"
            }).ToList();

            var createdMedia = await _frameMediaService.AddMultipleMediaAsync(request.FrameId, mediaList);
            var response = createdMedia.Select(MapToResponse).ToList();

            return CreatedAtAction(
                nameof(GetMediaByFrameId),
                new { frameId = request.FrameId },
                response
            );
        }

        /// <summary>
        /// Updates a media item (Manager only)
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(typeof(FrameMediaResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateMedia(Guid id, [FromBody] UpdateFrameMediaRequest request)
        {
            // Validate media type if provided
            if (!string.IsNullOrEmpty(request.MediaType) && !_frameMediaService.IsValidMediaType(request.MediaType))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid media type. Allowed: image, video, thumbnail, gallery"
                });
            }

            var updatedMedia = new FrameMedium
            {
                MediaUrl = request.MediaUrl,
                MediaType = request.MediaType?.ToLower()
            };

            var result = await _frameMediaService.UpdateMediaAsync(id, updatedMedia);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "MEDIA_NOT_FOUND",
                    Message = "Media not found"
                });
            }

            return Ok(MapToResponse(result));
        }

        /// <summary>
        /// Deletes a media item (Manager only)
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteMedia(Guid id)
        {
            var result = await _frameMediaService.DeleteMediaAsync(id);

            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "MEDIA_NOT_FOUND",
                    Message = "Media not found"
                });
            }

            return NoContent();
        }

        /// <summary>
        /// Deletes all media for a frame (Manager only)
        /// </summary>
        [HttpDelete("frame/{frameId:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteAllMediaByFrame(Guid frameId)
        {
            // Validate frame exists
            var frame = await _frameService.GetFrameByIdForManagerAsync(frameId);
            if (frame == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "FRAME_NOT_FOUND",
                    Message = "Frame not found"
                });
            }

            await _frameMediaService.DeleteAllMediaByFrameIdAsync(frameId);

            return NoContent();
        }

        #endregion
    }
}
