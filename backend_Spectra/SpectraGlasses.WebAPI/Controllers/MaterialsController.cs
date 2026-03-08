using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MaterialsController : ControllerBase
    {
        private readonly IMaterialService _materialService;

        public MaterialsController(IMaterialService materialService)
        {
            _materialService = materialService;
        }

        /// <summary>
        /// Gets all active materials
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllMaterials()
        {
            var materials = await _materialService.GetAllMaterialsAsync();
            return Ok(materials);
        }

        /// <summary>
        /// Gets a specific material by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMaterialById(Guid id)
        {
            var material = await _materialService.GetMaterialByIdAsync(id);
            if (material == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "MATERIAL_NOT_FOUND",
                    Message = "Material not found"
                });
            }
            return Ok(material);
        }

        /// <summary>
        /// Creates a new material (Manager only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateMaterial([FromBody] CreateMaterialRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MaterialName))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Material name is required"
                });
            }

            var material = new Material { MaterialName = request.MaterialName };
            var created = await _materialService.CreateMaterialAsync(material);

            return CreatedAtAction(nameof(GetMaterialById), new { id = created.MaterialId }, created);
        }

        /// <summary>
        /// Updates an existing material (Manager only)
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMaterial(Guid id, [FromBody] UpdateMaterialRequest request)
        {
            var updated = new Material { MaterialName = request.MaterialName };
            var result = await _materialService.UpdateMaterialAsync(id, updated);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "MATERIAL_NOT_FOUND",
                    Message = "Material not found"
                });
            }
            return Ok(result);
        }

        /// <summary>
        /// Soft deletes a material (Manager only)
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteMaterial(Guid id)
        {
            var result = await _materialService.SoftDeleteMaterialAsync(id);
            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "MATERIAL_NOT_FOUND",
                    Message = "Material not found"
                });
            }
            return NoContent();
        }
    }
}
