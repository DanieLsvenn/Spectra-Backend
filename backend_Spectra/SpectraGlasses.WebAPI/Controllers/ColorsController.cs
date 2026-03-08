using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ColorsController : ControllerBase
    {
        private readonly IColorService _colorService;

        public ColorsController(IColorService colorService)
        {
            _colorService = colorService;
        }

        /// <summary>
        /// Gets all active colors
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllColors()
        {
            var colors = await _colorService.GetAllColorsAsync();
            return Ok(colors);
        }

        /// <summary>
        /// Gets a specific color by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetColorById(Guid id)
        {
            var color = await _colorService.GetColorByIdAsync(id);
            if (color == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "COLOR_NOT_FOUND",
                    Message = "Color not found"
                });
            }
            return Ok(color);
        }

        /// <summary>
        /// Creates a new color (Manager only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateColor([FromBody] CreateColorRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ColorName))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Color name is required"
                });
            }

            var color = new Color
            {
                ColorName = request.ColorName,
                HexCode = request.HexCode
            };
            var created = await _colorService.CreateColorAsync(color);

            return CreatedAtAction(nameof(GetColorById), new { id = created.ColorId }, created);
        }

        /// <summary>
        /// Updates an existing color (Manager only)
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateColor(Guid id, [FromBody] UpdateColorRequest request)
        {
            var updated = new Color
            {
                ColorName = request.ColorName,
                HexCode = request.HexCode
            };
            var result = await _colorService.UpdateColorAsync(id, updated);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "COLOR_NOT_FOUND",
                    Message = "Color not found"
                });
            }
            return Ok(result);
        }

        /// <summary>
        /// Soft deletes a color (Manager only)
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "manager")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteColor(Guid id)
        {
            var result = await _colorService.SoftDeleteColorAsync(id);
            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "COLOR_NOT_FOUND",
                    Message = "Color not found"
                });
            }
            return NoContent();
        }
    }
}
