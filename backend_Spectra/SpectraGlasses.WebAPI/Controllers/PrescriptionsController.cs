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
    public class PrescriptionsController : ControllerBase
    {
        private readonly IPrescriptionService _prescriptionService;

        public PrescriptionsController(IPrescriptionService prescriptionService)
        {
            _prescriptionService = prescriptionService;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        private PrescriptionResponse MapToResponse(Prescription prescription)
        {
            return new PrescriptionResponse
            {
                PrescriptionId = prescription.PrescriptionId,
                UserId = prescription.UserId,
                SphereRight = prescription.SphereRight,
                CylinderRight = prescription.CylinderRight,
                AxisRight = prescription.AxisRight,
                AddRight = prescription.AddRight,
                SphereLeft = prescription.SphereLeft,
                CylinderLeft = prescription.CylinderLeft,
                AxisLeft = prescription.AxisLeft,
                AddLeft = prescription.AddLeft,
                PupillaryDistance = prescription.PupillaryDistance,
                DoctorName = prescription.DoctorName,
                ClinicName = prescription.ClinicName,
                ExpirationDate = prescription.ExpirationDate,
                CreatedAt = prescription.CreatedAt,
                IsExpired = _prescriptionService.IsPrescriptionExpired(prescription),
                DaysUntilExpiration = _prescriptionService.GetDaysUntilExpiration(prescription)
            };
        }

        #region Customer Endpoints

        /// <summary>
        /// Creates a new prescription for the current user
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(typeof(PrescriptionResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreatePrescription([FromBody] CreatePrescriptionRequest request)
        {
            var userId = GetCurrentUserId();

            if (userId == Guid.Empty)
            {
                return Unauthorized(new ErrorResponse
                {
                    ErrorCode = "UNAUTHORIZED",
                    Message = "User not authenticated"
                });
            }

            // At least one sphere value is required
            if (!request.SphereLeft.HasValue && !request.SphereRight.HasValue)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "At least one sphere value (left or right) is required"
                });
            }

            var prescription = new Prescription
            {
                UserId = userId,
                SphereRight = request.SphereRight,
                CylinderRight = request.CylinderRight,
                AxisRight = request.AxisRight,
                AddRight = request.AddRight,
                SphereLeft = request.SphereLeft,
                CylinderLeft = request.CylinderLeft,
                AxisLeft = request.AxisLeft,
                AddLeft = request.AddLeft,
                PupillaryDistance = request.PupillaryDistance,
                DoctorName = request.DoctorName,
                ClinicName = request.ClinicName,
                ExpirationDate = request.ExpirationDate
            };

            // Validate prescription values
            var validationResult = _prescriptionService.ValidatePrescription(prescription);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = string.Join("; ", validationResult.Errors)
                });
            }

            var createdPrescription = await _prescriptionService.CreatePrescriptionAsync(prescription);
            var response = MapToResponse(createdPrescription);

            return CreatedAtAction(
                nameof(GetPrescriptionById),
                new { id = createdPrescription.PrescriptionId },
                response
            );
        }

        /// <summary>
        /// Gets all prescriptions for the current user
        /// </summary>
        [HttpGet("my")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyPrescriptions([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var userId = GetCurrentUserId();

            if (userId == Guid.Empty)
            {
                return Unauthorized(new ErrorResponse
                {
                    ErrorCode = "UNAUTHORIZED",
                    Message = "User not authenticated"
                });
            }

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _prescriptionService.GetPrescriptionsByUserAsync(userId, page, pageSize);

            // Map to response with expiration info
            var responseItems = result.Items.Select(MapToResponse).ToList();

            return Ok(new
            {
                result.TotalItems,
                result.TotalPages,
                result.CurrentPage,
                result.PageSize,
                Items = responseItems
            });
        }

        /// <summary>
        /// Gets only valid (non-expired) prescriptions for the current user
        /// </summary>
        [HttpGet("my/valid")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyValidPrescriptions()
        {
            var userId = GetCurrentUserId();

            if (userId == Guid.Empty)
            {
                return Unauthorized(new ErrorResponse
                {
                    ErrorCode = "UNAUTHORIZED",
                    Message = "User not authenticated"
                });
            }

            var prescriptions = await _prescriptionService.GetValidPrescriptionsByUserAsync(userId);
            var response = prescriptions.Select(MapToResponse).ToList();

            return Ok(response);
        }

        /// <summary>
        /// Gets a specific prescription by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize]
        [ProducesResponseType(typeof(PrescriptionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPrescriptionById(Guid id)
        {
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            var prescription = await _prescriptionService.GetPrescriptionByIdAsync(id);

            if (prescription == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "PRESCRIPTION_NOT_FOUND",
                    Message = "Prescription not found"
                });
            }

            // Customers can only view their own prescriptions
            if (userRole.ToLower() == "customer" && prescription.UserId != userId)
            {
                return Forbid();
            }

            return Ok(MapToResponse(prescription));
        }

        /// <summary>
        /// Updates an existing prescription
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(typeof(PrescriptionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdatePrescription(Guid id, [FromBody] UpdatePrescriptionRequest request)
        {
            var userId = GetCurrentUserId();

            if (userId == Guid.Empty)
            {
                return Unauthorized(new ErrorResponse
                {
                    ErrorCode = "UNAUTHORIZED",
                    Message = "User not authenticated"
                });
            }

            var updatedPrescription = new Prescription
            {
                SphereRight = request.SphereRight,
                CylinderRight = request.CylinderRight,
                AxisRight = request.AxisRight,
                AddRight = request.AddRight,
                SphereLeft = request.SphereLeft,
                CylinderLeft = request.CylinderLeft,
                AxisLeft = request.AxisLeft,
                AddLeft = request.AddLeft,
                PupillaryDistance = request.PupillaryDistance,
                DoctorName = request.DoctorName,
                ClinicName = request.ClinicName,
                ExpirationDate = request.ExpirationDate
            };

            // Validate if values are provided
            var validationResult = _prescriptionService.ValidatePrescription(updatedPrescription);

            if (!validationResult.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = string.Join("; ", validationResult.Errors)
                });
            }

            var result = await _prescriptionService.UpdatePrescriptionAsync(id, updatedPrescription, userId);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "PRESCRIPTION_NOT_FOUND",
                    Message = "Prescription not found or you don't have permission to update it"
                });
            }

            return Ok(MapToResponse(result));
        }

        /// <summary>
        /// Deletes a prescription (only if not used in orders)
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "customer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeletePrescription(Guid id)
        {
            var userId = GetCurrentUserId();

            if (userId == Guid.Empty)
            {
                return Unauthorized(new ErrorResponse
                {
                    ErrorCode = "UNAUTHORIZED",
                    Message = "User not authenticated"
                });
            }

            // Check if can delete
            var canDelete = await _prescriptionService.CanDeletePrescriptionAsync(id);

            if (!canDelete)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "PRESCRIPTION_IN_USE",
                    Message = "Cannot delete prescription because it is used in existing orders or preorders"
                });
            }

            var result = await _prescriptionService.DeletePrescriptionAsync(id, userId);

            if (!result)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "PRESCRIPTION_NOT_FOUND",
                    Message = "Prescription not found or you don't have permission to delete it"
                });
            }

            return NoContent();
        }

        /// <summary>
        /// Checks if a prescription is valid (not expired)
        /// </summary>
        [HttpGet("{id:guid}/validate")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ValidatePrescription(Guid id)
        {
            var prescription = await _prescriptionService.GetPrescriptionByIdAsync(id);

            if (prescription == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "PRESCRIPTION_NOT_FOUND",
                    Message = "Prescription not found"
                });
            }

            var isValid = _prescriptionService.IsPrescriptionValid(prescription);
            var isExpired = _prescriptionService.IsPrescriptionExpired(prescription);
            var daysUntilExpiration = _prescriptionService.GetDaysUntilExpiration(prescription);

            return Ok(new
            {
                PrescriptionId = prescription.PrescriptionId,
                IsValid = isValid,
                IsExpired = isExpired,
                DaysUntilExpiration = daysUntilExpiration,
                ExpirationDate = prescription.ExpirationDate,
                Message = isExpired 
                    ? "This prescription has expired and cannot be used for orders" 
                    : daysUntilExpiration <= 30 
                        ? $"This prescription will expire in {daysUntilExpiration} days" 
                        : "This prescription is valid"
            });
        }

        #endregion

        #region Staff/Manager Endpoints

        /// <summary>
        /// Gets prescriptions for a specific user (Staff/Manager/Admin)
        /// </summary>
        [HttpGet("user/{userId:guid}")]
        [Authorize(Roles = "staff,manager,admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPrescriptionsByUser(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _prescriptionService.GetPrescriptionsByUserAsync(userId, page, pageSize);
            var responseItems = result.Items.Select(MapToResponse).ToList();

            return Ok(new
            {
                result.TotalItems,
                result.TotalPages,
                result.CurrentPage,
                result.PageSize,
                Items = responseItems
            });
        }

        #endregion
    }
}
