using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LensTypesController : ControllerBase
    {
        private readonly ILensTypeService _lensTypeService;

        public LensTypesController(ILensTypeService lensTypeService)
        {
            _lensTypeService = lensTypeService;
        }

        #region Public Endpoints (No Authorization)

        /// <summary>
        /// Gets all lens types with pagination
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLensTypes([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _lensTypeService.GetLensTypesAsync(page, pageSize);
            return Ok(result);
        }

        /// <summary>
        /// Gets a specific lens type by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLensTypeById(Guid id)
        {
            var lensType = await _lensTypeService.GetLensTypeByIdAsync(id);

            if (lensType == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "LENS_TYPE_NOT_FOUND",
                    Message = "Lens type not found"
                });
            }

            return Ok(lensType);
        }

        /// <summary>
        /// Gets all lens types that require a prescription
        /// </summary>
        [HttpGet("prescription-required")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLensTypesRequiringPrescription()
        {
            var lensTypes = await _lensTypeService.GetLensTypesRequiringPrescriptionAsync();
            return Ok(lensTypes);
        }

        /// <summary>
        /// Gets all lens types that do not require a prescription
        /// </summary>
        [HttpGet("no-prescription")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLensTypesNotRequiringPrescription()
        {
            var lensTypes = await _lensTypeService.GetLensTypesNotRequiringPrescriptionAsync();
            return Ok(lensTypes);
        }

        #endregion

        #region Manager Endpoints (Authorization Required)

        /// <summary>
        /// Creates a new lens type (Manager only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateLensType([FromBody] CreateLensTypeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LensSpecification))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Lens specification is required"
                });
            }

            // Validate price
            if (request.ExtraPrice.HasValue && request.ExtraPrice < 0)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Extra price cannot be negative"
                });
            }

            var lensType = new LensType
            {
                LensSpecification = request.LensSpecification,
                RequiresPrescription = request.RequiresPrescription,
                ExtraPrice = request.ExtraPrice
            };

            var createdLensType = await _lensTypeService.CreateLensTypeAsync(lensType);

            return CreatedAtAction(
                nameof(GetLensTypeById),
                new { id = createdLensType.LensTypeId },
                createdLensType
            );
        }

        /// <summary>
        /// Updates an existing lens type (Manager only)
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateLensType(Guid id, [FromBody] UpdateLensTypeRequest request)
        {
            // Validate price if provided
            if (request.ExtraPrice.HasValue && request.ExtraPrice < 0)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Extra price cannot be negative"
                });
            }

            var updatedLensType = new LensType
            {
                LensSpecification = request.LensSpecification,
                RequiresPrescription = request.RequiresPrescription,
                ExtraPrice = request.ExtraPrice
            };

            var result = await _lensTypeService.UpdateLensTypeAsync(id, updatedLensType);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "LENS_TYPE_NOT_FOUND",
                    Message = "Lens type not found"
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Deletes a lens type (Manager only) - Only if not used in any orders
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteLensType(Guid id)
        {
            // Check if lens type can be deleted
            var canDelete = await _lensTypeService.CanDeleteLensTypeAsync(id);

            if (!canDelete)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "LENS_TYPE_IN_USE",
                    Message = "Cannot delete lens type because it is used in existing orders or preorders"
                });
            }

            var result = await _lensTypeService.DeleteLensTypeAsync(id);

            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "LENS_TYPE_NOT_FOUND",
                    Message = "Lens type not found"
                });
            }

            return NoContent();
        }

        #endregion
    }
}
