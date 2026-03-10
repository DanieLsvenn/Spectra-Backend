using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShapesController : ControllerBase
    {
        private readonly IShapeService _shapeService;

        public ShapesController(IShapeService shapeService)
        {
            _shapeService = shapeService;
        }

        /// <summary>
        /// Gets all active shapes
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllShapes()
        {
            var shapes = await _shapeService.GetAllShapesAsync();
            return Ok(shapes);
        }

        /// <summary>
        /// Gets a specific shape by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetShapeById(Guid id)
        {
            var shape = await _shapeService.GetShapeByIdAsync(id);
            if (shape == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "SHAPE_NOT_FOUND",
                    Message = "Shape not found"
                });
            }
            return Ok(shape);
        }

        /// <summary>
        /// Creates a new shape (Manager only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateShape([FromBody] CreateShapeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ShapeName))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Shape name is required"
                });
            }

            var shape = new Shape { ShapeName = request.ShapeName };
            var created = await _shapeService.CreateShapeAsync(shape);

            return CreatedAtAction(nameof(GetShapeById), new { id = created.ShapeId }, created);
        }

        /// <summary>
        /// Updates an existing shape (Manager only)
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateShape(Guid id, [FromBody] UpdateShapeRequest request)
        {
            var updated = new Shape { ShapeName = request.ShapeName };
            var result = await _shapeService.UpdateShapeAsync(id, updated);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "SHAPE_NOT_FOUND",
                    Message = "Shape not found"
                });
            }
            return Ok(result);
        }

        /// <summary>
        /// Soft deletes a shape (Manager only)
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteShape(Guid id)
        {
            var result = await _shapeService.SoftDeleteShapeAsync(id);
            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "SHAPE_NOT_FOUND",
                    Message = "Shape not found"
                });
            }
            return NoContent();
        }
    }
}
