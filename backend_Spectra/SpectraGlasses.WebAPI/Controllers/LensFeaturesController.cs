using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LensFeaturesController : ControllerBase
    {
        private readonly ILensFeatureService _lensFeatureService;
        private readonly ILensTypeService _lensTypeService;

        public LensFeaturesController(
            ILensFeatureService lensFeatureService,
            ILensTypeService lensTypeService)
        {
            _lensFeatureService = lensFeatureService;
            _lensTypeService = lensTypeService;
        }

        #region Public Endpoints (No Authorization)

        /// <summary>
        /// Gets all lens features with pagination
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLensFeatures([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _lensFeatureService.GetLensFeaturesAsync(page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Gets a specific lens feature by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLensFeatureById(Guid id)
        {
            var feature = await _lensFeatureService.GetLensFeatureByIdAsync(id);

            if (feature == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "LENS_FEATURE_NOT_FOUND",
                    Message = "Lens feature not found"
                });
            }

            return Ok(feature);
        }

        /// <summary>
        /// Gets lens features by lens index
        /// </summary>
        [HttpGet("by-index/{lensIndex:double}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLensFeaturesByIndex(double lensIndex)
        {
            var features = await _lensFeatureService.GetLensFeaturesByIndexAsync(lensIndex);
            return Ok(features);
        }

        /// <summary>
        /// Calculates total price based on frame, lens feature, and lens type
        /// </summary>
        [HttpPost("calculate-price")]
        [ProducesResponseType(typeof(PriceCalculationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CalculatePrice([FromBody] PriceCalculationRequest request)
        {
            if (request.BasePrice < 0)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Base price cannot be negative"
                });
            }

            LensFeature? feature = null;
            LensType? lensType = null;

            if (request.LensFeatureId.HasValue)
            {
                feature = await _lensFeatureService.GetLensFeatureByIdAsync(request.LensFeatureId.Value);
            }

            if (request.LensTypeId.HasValue)
            {
                lensType = await _lensTypeService.GetLensTypeByIdAsync(request.LensTypeId.Value);
            }

            var featureExtraPrice = _lensFeatureService.GetFeatureExtraPrice(feature);
            var lensTypeExtraPrice = _lensFeatureService.GetLensTypeExtraPrice(lensType);
            var totalPrice = _lensFeatureService.CalculateTotalPrice(request.BasePrice, feature, lensType);

            return Ok(new PriceCalculationResponse
            {
                BasePrice = request.BasePrice,
                FeatureExtraPrice = featureExtraPrice,
                LensTypeExtraPrice = lensTypeExtraPrice,
                TotalPrice = totalPrice
            });
        }

        #endregion

        #region Manager Endpoints (Authorization Required)

        /// <summary>
        /// Creates a new lens feature (Manager only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateLensFeature([FromBody] CreateLensFeatureRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FeatureSpecification))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Feature specification is required"
                });
            }

            // Validate price
            var priceValidation = _lensFeatureService.ValidatePrice(request.ExtraPrice);
            if (!priceValidation.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = string.Join("; ", priceValidation.Errors)
                });
            }

            // Validate lens index
            var indexValidation = _lensFeatureService.ValidateLensIndex(request.LensIndex);
            if (!indexValidation.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = string.Join("; ", indexValidation.Errors)
                });
            }

            var lensFeature = new LensFeature
            {
                LensIndex = request.LensIndex,
                FeatureSpecification = request.FeatureSpecification,
                ExtraPrice = request.ExtraPrice
            };

            var createdFeature = await _lensFeatureService.CreateLensFeatureAsync(lensFeature);

            return CreatedAtAction(
                nameof(GetLensFeatureById),
                new { id = createdFeature.FeatureId },
                createdFeature
            );
        }

        /// <summary>
        /// Updates an existing lens feature (Manager only)
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateLensFeature(Guid id, [FromBody] UpdateLensFeatureRequest request)
        {
            // Validate price if provided
            var priceValidation = _lensFeatureService.ValidatePrice(request.ExtraPrice);
            if (!priceValidation.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = string.Join("; ", priceValidation.Errors)
                });
            }

            // Validate lens index if provided
            var indexValidation = _lensFeatureService.ValidateLensIndex(request.LensIndex);
            if (!indexValidation.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = string.Join("; ", indexValidation.Errors)
                });
            }

            var updatedFeature = new LensFeature
            {
                LensIndex = request.LensIndex,
                FeatureSpecification = request.FeatureSpecification,
                ExtraPrice = request.ExtraPrice
            };

            var result = await _lensFeatureService.UpdateLensFeatureAsync(id, updatedFeature);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "LENS_FEATURE_NOT_FOUND",
                    Message = "Lens feature not found"
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Deletes a lens feature (Manager only) - Only if not used in any orders
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteLensFeature(Guid id)
        {
            // Check if feature can be deleted
            var canDelete = await _lensFeatureService.CanDeleteLensFeatureAsync(id);

            if (!canDelete)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "LENS_FEATURE_IN_USE",
                    Message = "Cannot delete lens feature because it is used in existing orders or preorders"
                });
            }

            var result = await _lensFeatureService.DeleteLensFeatureAsync(id);

            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "LENS_FEATURE_NOT_FOUND",
                    Message = "Lens feature not found"
                });
            }

            return NoContent();
        }

        #endregion
    }
}
