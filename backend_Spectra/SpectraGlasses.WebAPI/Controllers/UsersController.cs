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
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }

        private UserResponse MapToResponse(User user)
        {
            return new UserResponse
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Address = user.Address,
                Role = user.Role,
                Status = user.Status,
                CreatedAt = user.CreatedAt
            };
        }

        #region Customer Endpoints

        /// <summary>
        /// Gets current user's profile
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyProfile()
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

            var user = await _userService.GetUserByIdAsync(userId);

            if (user == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "USER_NOT_FOUND",
                    Message = "User not found"
                });
            }

            return Ok(MapToResponse(user));
        }

        /// <summary>
        /// Updates current user's profile
        /// </summary>
        [HttpPut("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateUserRequest request)
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

            var updatedUser = new User
            {
                FullName = request.FullName,
                Phone = request.Phone,
                Address = request.Address
            };

            var result = await _userService.UpdateUserAsync(userId, updatedUser);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "USER_NOT_FOUND",
                    Message = "User not found"
                });
            }

            return Ok(MapToResponse(result));
        }

        #endregion

        #region Admin Endpoints

        /// <summary>
        /// Gets all users (Admin/Manager)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "admin,manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _userService.GetAllUsersAsync(page, pageSize);

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
        /// Search users (Admin/Manager)
        /// </summary>
        [HttpGet("search")]
        [Authorize(Roles = "admin,manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SearchUsers([FromQuery] string? searchTerm, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetAllUsers(page, pageSize);
            }

            var result = await _userService.SearchUsersAsync(searchTerm, page, pageSize);

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
        /// Gets users by role (Admin/Manager)
        /// </summary>
        [HttpGet("role/{role}")]
        [Authorize(Roles = "admin,manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUsersByRole(string role, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (!_userService.IsValidRole(role))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid role. Allowed: customer, staff, manager, admin"
                });
            }

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _userService.GetUsersByRoleAsync(role, page, pageSize);

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
        /// Gets users by status (Admin/Manager)
        /// </summary>
        [HttpGet("status/{status}")]
        [Authorize(Roles = "admin,manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUsersByStatus(string status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (!_userService.IsValidStatus(status))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid status. Allowed: active, inactive, suspended, pending"
                });
            }

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var result = await _userService.GetUsersByStatusAsync(status, page, pageSize);

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
        /// Gets a specific user by ID (Admin/Manager)
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "admin,manager")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUserById(Guid id)
        {
            var user = await _userService.GetUserByIdAsync(id);

            if (user == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "USER_NOT_FOUND",
                    Message = "User not found"
                });
            }

            return Ok(MapToResponse(user));
        }

        /// <summary>
        /// Creates a new user (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            // Validate email
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Email is required"
                });
            }

            // Validate password
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Password must be at least 6 characters"
                });
            }

            // Validate role
            if (!_userService.IsValidRole(request.Role))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid role. Allowed: customer, staff, manager, admin"
                });
            }

            // Check if email already exists
            var existingUser = await _userService.GetUserByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "EMAIL_EXISTS",
                    Message = "A user with this email already exists"
                });
            }

            var user = new User
            {
                Email = request.Email,
                PasswordHash = request.Password, // In production, hash this password!
                FullName = request.FullName,
                Phone = request.Phone,
                Address = request.Address,
                Role = request.Role.ToLower()
            };

            var createdUser = await _userService.CreateUserAsync(user);

            return CreatedAtAction(
                nameof(GetUserById),
                new { id = createdUser.UserId },
                MapToResponse(createdUser)
            );
        }

        /// <summary>
        /// Updates a user's profile (Admin only)
        /// </summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
        {
            var updatedUser = new User
            {
                FullName = request.FullName,
                Phone = request.Phone,
                Address = request.Address
            };

            var result = await _userService.UpdateUserAsync(id, updatedUser);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "USER_NOT_FOUND",
                    Message = "User not found"
                });
            }

            return Ok(MapToResponse(result));
        }

        /// <summary>
        /// Updates a user's status (Admin only) - Activate/Deactivate/Suspend
        /// </summary>
        [HttpPut("{id:guid}/status")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Status is required"
                });
            }

            if (!_userService.IsValidStatus(request.Status))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid status. Allowed: active, inactive, suspended, pending"
                });
            }

            var result = await _userService.UpdateUserStatusAsync(id, request.Status);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "USER_NOT_FOUND",
                    Message = "User not found"
                });
            }

            return Ok(MapToResponse(result));
        }

        /// <summary>
        /// Updates a user's role (Admin only)
        /// </summary>
        [HttpPut("{id:guid}/role")]
        [Authorize(Roles = "admin")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateUserRole(Guid id, [FromBody] UpdateUserRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Role))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Role is required"
                });
            }

            if (!_userService.IsValidRole(request.Role))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid role. Allowed: customer, staff, manager, admin"
                });
            }

            var result = await _userService.UpdateUserRoleAsync(id, request.Role);

            if (result == null)
            {
                return NotFound(new ErrorResponse
                {
                    ErrorCode = "USER_NOT_FOUND",
                    Message = "User not found"
                });
            }

            return Ok(MapToResponse(result));
        }

        #endregion
    }
}
