using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BrandsController : ControllerBase
    {
        private readonly IBrandService _brandService;

        public BrandsController(IBrandService brandService)
        {
            _brandService = brandService;
        }

        /// <summary>
        /// Gets all active brands
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllBrands()
        {
            var brands = await _brandService.GetAllBrandsAsync();
            return Ok(brands);
        }

        /// <summary>
        /// Gets a specific brand by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBrandById(Guid id)
        {
            var brand = await _brandService.GetBrandByIdAsync(id);
            if (brand == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "BRAND_NOT_FOUND",
                    Message = "Brand not found"
                });
            }
            return Ok(brand);
        }

        /// <summary>
        /// Creates a new brand (Manager only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateBrand([FromBody] CreateBrandRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BrandName))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Brand name is required"
                });
            }

            var brand = new Brand { BrandName = request.BrandName };
            var created = await _brandService.CreateBrandAsync(brand);

            return CreatedAtAction(nameof(GetBrandById), new { id = created.BrandId }, created);
        }

        /// <summary>
        /// Updates an existing brand (Manager only)
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateBrand(Guid id, [FromBody] UpdateBrandRequest request)
        {
            var updated = new Brand { BrandName = request.BrandName };
            var result = await _brandService.UpdateBrandAsync(id, updated);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "BRAND_NOT_FOUND",
                    Message = "Brand not found"
                });
            }
            return Ok(result);
        }

        /// <summary>
        /// Soft deletes a brand (Manager only)
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteBrand(Guid id)
        {
            var result = await _brandService.SoftDeleteBrandAsync(id);
            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "BRAND_NOT_FOUND",
                    Message = "Brand not found"
                });
            }
            return NoContent();
        }
    }
}
