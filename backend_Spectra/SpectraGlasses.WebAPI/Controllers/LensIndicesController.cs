using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LensIndicesController : ControllerBase
    {
        private readonly ILensIndexService _lensIndexService;

        public LensIndicesController(ILensIndexService lensIndexService)
        {
            _lensIndexService = lensIndexService;
        }

        /// <summary>
        /// Gets all active lens indices
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllLensIndices()
        {
            var indices = await _lensIndexService.GetAllLensIndicesAsync();
            return Ok(indices);
        }

        /// <summary>
        /// Gets a specific lens index by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLensIndexById(Guid id)
        {
            var lensIndex = await _lensIndexService.GetLensIndexByIdAsync(id);
            if (lensIndex == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "LENS_INDEX_NOT_FOUND",
                    Message = "Lens index not found"
                });
            }
            return Ok(lensIndex);
        }

        /// <summary>
        /// Gets compatible lens indices for a given prescription sphere value
        /// </summary>
        [HttpGet("compatible")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCompatibleIndices([FromQuery] double sphere)
        {
            var indices = await _lensIndexService.GetCompatibleIndicesForPrescriptionAsync(sphere);
            return Ok(indices);
        }

        /// <summary>
        /// Creates a new lens index (Manager only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateLensIndex([FromBody] CreateLensIndexRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Lens index name is required"
                });
            }

            if (request.IndexValue <= 0)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Index value must be greater than 0"
                });
            }

            var lensIndex = new LensIndex
            {
                IndexValue = request.IndexValue,
                Name = request.Name,
                Description = request.Description,
                AdditionalPrice = request.AdditionalPrice,
                MinPrescription = request.MinPrescription,
                MaxPrescription = request.MaxPrescription,
                BrandId = request.BrandId,
                ColorId = request.ColorId
            };
            var created = await _lensIndexService.CreateLensIndexAsync(lensIndex);

            return CreatedAtAction(nameof(GetLensIndexById), new { id = created.LensIndexId }, created);
        }

        /// <summary>
        /// Updates an existing lens index (Manager only)
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateLensIndex(Guid id, [FromBody] UpdateLensIndexRequest request)
        {
            var updated = new LensIndex
            {
                IndexValue = request.IndexValue ?? 0,
                Name = request.Name,
                Description = request.Description,
                AdditionalPrice = request.AdditionalPrice ?? 0,
                MinPrescription = request.MinPrescription,
                MaxPrescription = request.MaxPrescription,
                BrandId = request.BrandId,
                ColorId = request.ColorId
            };
            var result = await _lensIndexService.UpdateLensIndexAsync(id, updated);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "LENS_INDEX_NOT_FOUND",
                    Message = "Lens index not found"
                });
            }
            return Ok(result);
        }

        /// <summary>
        /// Soft deletes a lens index (Manager only)
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteLensIndex(Guid id)
        {
            var result = await _lensIndexService.SoftDeleteLensIndexAsync(id);
            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "LENS_INDEX_NOT_FOUND",
                    Message = "Lens index not found"
                });
            }
            return NoContent();
        }
    }
}
