using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductReviewsController : ControllerBase
    {
        private readonly IProductReviewService _reviewService;

        public ProductReviewsController(IProductReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        #region Public Endpoints

        /// <summary>
        /// Gets reviews for a specific frame with pagination
        /// </summary>
        [HttpGet("frame/{frameId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReviewsByFrame(Guid frameId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _reviewService.GetReviewsByFrameAsync(frameId, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Gets the review summary (average rating, distribution) for a frame
        /// </summary>
        [HttpGet("frame/{frameId:guid}/summary")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFrameReviewSummary(Guid frameId)
        {
            var summary = await _reviewService.GetFrameReviewSummaryAsync(frameId);
            return Ok(summary);
        }

        #endregion

        #region Customer Endpoints

        /// <summary>
        /// Creates a new review (Customer only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (request.Rating < 0 || request.Rating > 5)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Rating must be between 0 and 5"
                });
            }

            try
            {
                var review = new ProductReview
                {
                    UserId = userId,
                    FrameId = request.FrameId,
                    OrderItemId = request.OrderItemId,
                    Rating = request.Rating,
                    Title = request.Title,
                    Comment = request.Comment
                };

                var created = await _reviewService.CreateReviewAsync(review);
                return CreatedAtAction(nameof(GetReviewsByFrame), new { frameId = created.FrameId }, created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "REVIEW_ERROR",
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets reviews by the current user
        /// </summary>
        [HttpGet("my-reviews")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _reviewService.GetReviewsByUserAsync(userId, page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Updates a review (Customer only, own reviews)
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateReview(Guid id, [FromBody] UpdateReviewRequest request)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var update = new ProductReview
            {
                Rating = request.Rating ?? 0,
                Title = request.Title,
                Comment = request.Comment
            };
            var result = await _reviewService.UpdateReviewAsync(id, update, userId);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "REVIEW_NOT_FOUND",
                    Message = "Review not found or you don't have permission to update it"
                });
            }
            return Ok(result);
        }

        /// <summary>
        /// Deletes a review (Customer only, own reviews)
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteReview(Guid id)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _reviewService.DeleteReviewAsync(id, userId);

            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "REVIEW_NOT_FOUND",
                    Message = "Review not found or you don't have permission to delete it"
                });
            }
            return NoContent();
        }

        /// <summary>
        /// Checks if the current user has a verified purchase of a frame
        /// </summary>
        [HttpGet("verified-purchase/{frameId:guid}")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> IsVerifiedPurchase(Guid frameId)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isVerified = await _reviewService.IsVerifiedPurchaseAsync(userId, frameId);
            return Ok(new { IsVerifiedPurchase = isVerified });
        }

        #endregion

        #region Manager Endpoints

        /// <summary>
        /// Hides a review (Manager only)
        /// </summary>
        [HttpPatch("{id:guid}/hide")]
        [Authorize(Roles = "manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> HideReview(Guid id)
        {
            var result = await _reviewService.HideReviewAsync(id);
            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "REVIEW_NOT_FOUND",
                    Message = "Review not found"
                });
            }
            return Ok(new { Message = "Review hidden successfully" });
        }

        #endregion
    }
}
