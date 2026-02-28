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
        private readonly ICloudinaryService _cloudinaryService;

        public FrameMediaController(
            IFrameMediaService frameMediaService,
            IFrameService frameService,
            ICloudinaryService cloudinaryService)
        {
            _frameMediaService = frameMediaService;
            _frameService = frameService;
            _cloudinaryService = cloudinaryService;
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

        /// <summary>
        /// Uploads an image to Cloudinary and creates a media record for a frame (Manager only)
        /// </summary>
        /// <param name="frameId">The frame ID to associate the image with</param>
        /// <param name="file">The image file to upload</param>
        /// <param name="mediaType">The type of media (image, thumbnail, gallery). Defaults to "image"</param>
        [HttpPost("upload/{frameId:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(typeof(FrameMediaUploadResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UploadImage(Guid frameId, IFormFile file, [FromQuery] string mediaType = "image")
        {
            // Validate frame exists
            var frame = await _frameService.GetFrameByIdForManagerAsync(frameId);
            if (frame == null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "FRAME_NOT_FOUND",
                    Message = "Frame not found"
                });
            }

            // Validate file
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "No file provided"
                });
            }

            // Validate file size (max 10MB)
            const long maxFileSize = 10 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "File size exceeds maximum allowed size of 10MB"
                });
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid file type. Allowed: jpg, jpeg, png, gif, webp"
                });
            }

            // Validate media type
            if (!_frameMediaService.IsValidMediaType(mediaType))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid media type. Allowed: image, video, thumbnail, gallery"
                });
            }

            // Upload to Cloudinary
            using var stream = file.OpenReadStream();
            var folder = $"spectra/frames/{frameId}";
            var uploadResult = await _cloudinaryService.UploadImageAsync(stream, file.FileName, folder);

            if (!uploadResult.Success)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "UPLOAD_ERROR",
                    Message = uploadResult.Error
                });
            }

            // Create media record
            var media = new FrameMedium
            {
                FrameId = frameId,
                MediaUrl = uploadResult.Url,
                MediaType = mediaType.ToLower()
            };

            var createdMedia = await _frameMediaService.AddMediaAsync(media);

            return CreatedAtAction(
                nameof(GetMediaById),
                new { id = createdMedia.MediaId },
                new FrameMediaUploadResponse
                {
                    MediaId = createdMedia.MediaId,
                    FrameId = createdMedia.FrameId,
                    MediaUrl = createdMedia.MediaUrl,
                    MediaType = createdMedia.MediaType,
                    PublicId = uploadResult.PublicId
                }
            );
        }

        /// <summary>
        /// Uploads multiple images to Cloudinary and creates media records for a frame (Manager only)
        /// </summary>
        /// <param name="frameId">The frame ID to associate the images with</param>
        /// <param name="files">The image files to upload</param>
        /// <param name="mediaType">The type of media (image, thumbnail, gallery). Defaults to "image"</param>
        [HttpPost("upload-multiple/{frameId:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(typeof(List<FrameMediaUploadResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UploadMultipleImages(Guid frameId, List<IFormFile> files, [FromQuery] string mediaType = "image")
        {
            // Validate frame exists
            var frame = await _frameService.GetFrameByIdForManagerAsync(frameId);
            if (frame == null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "FRAME_NOT_FOUND",
                    Message = "Frame not found"
                });
            }

            // Validate files
            if (files == null || !files.Any())
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "No files provided"
                });
            }

            // Validate max number of files (limit to 10 at a time)
            if (files.Count > 10)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Maximum 10 files can be uploaded at once"
                });
            }

            // Validate media type
            if (!_frameMediaService.IsValidMediaType(mediaType))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid media type. Allowed: image, video, thumbnail, gallery"
                });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            const long maxFileSize = 10 * 1024 * 1024;
            var results = new List<FrameMediaUploadResponse>();
            var errors = new List<string>();

            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];

                // Validate file
                if (file == null || file.Length == 0)
                {
                    errors.Add($"File {i + 1}: Empty file");
                    continue;
                }

                if (file.Length > maxFileSize)
                {
                    errors.Add($"File {i + 1}: Exceeds 10MB limit");
                    continue;
                }

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    errors.Add($"File {i + 1}: Invalid file type");
                    continue;
                }

                // Upload to Cloudinary
                using var stream = file.OpenReadStream();
                var folder = $"spectra/frames/{frameId}";
                var uploadResult = await _cloudinaryService.UploadImageAsync(stream, file.FileName, folder);

                if (!uploadResult.Success)
                {
                    errors.Add($"File {i + 1}: {uploadResult.Error}");
                    continue;
                }

                // Create media record
                var media = new FrameMedium
                {
                    FrameId = frameId,
                    MediaUrl = uploadResult.Url,
                    MediaType = mediaType.ToLower()
                };

                var createdMedia = await _frameMediaService.AddMediaAsync(media);

                results.Add(new FrameMediaUploadResponse
                {
                    MediaId = createdMedia.MediaId,
                    FrameId = createdMedia.FrameId,
                    MediaUrl = createdMedia.MediaUrl,
                    MediaType = createdMedia.MediaType,
                    PublicId = uploadResult.PublicId
                });
            }

            if (!results.Any() && errors.Any())
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "UPLOAD_ERROR",
                    Message = string.Join("; ", errors)
                });
            }

            return CreatedAtAction(
                nameof(GetMediaByFrameId),
                new { frameId },
                new
                {
                    UploadedMedia = results,
                    Errors = errors.Any() ? errors : null
                }
            );
        }

        /// <summary>
        /// Uploads an image to Cloudinary without associating it with a frame (Manager only)
        /// Useful for getting a URL before creating/updating a frame
        /// </summary>
        /// <param name="file">The image file to upload</param>
        /// <param name="folder">The folder to upload to. Defaults to "products"</param>
        [HttpPost("upload")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UploadImageOnly(IFormFile file, [FromQuery] string folder = "spectra/products")
        {
            // Validate file
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "No file provided"
                });
            }

            // Validate file size (max 10MB)
            const long maxFileSize = 10 * 1024 * 1024;
            if (file.Length > maxFileSize)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "File size exceeds maximum allowed size of 10MB"
                });
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid file type. Allowed: jpg, jpeg, png, gif, webp"
                });
            }

            // Upload to Cloudinary
            using var stream = file.OpenReadStream();
            var uploadResult = await _cloudinaryService.UploadImageAsync(stream, file.FileName, folder);

            if (!uploadResult.Success)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "UPLOAD_ERROR",
                    Message = uploadResult.Error
                });
            }

            return Ok(new ImageUploadResponse
            {
                Success = true,
                Url = uploadResult.Url,
                PublicId = uploadResult.PublicId
            });
        }

        /// <summary>
        /// Deletes an image from Cloudinary by its public ID (Manager only)
        /// </summary>
        /// <param name="publicId">The Cloudinary public ID of the image to delete</param>
        [HttpDelete("cloudinary/{*publicId}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteFromCloudinary(string publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Public ID is required"
                });
            }

            var result = await _cloudinaryService.DeleteImageAsync(publicId);

            if (!result)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "DELETE_ERROR",
                    Message = "Failed to delete image from Cloudinary"
                });
            }

            return NoContent();
        }

        #endregion
    }
}
