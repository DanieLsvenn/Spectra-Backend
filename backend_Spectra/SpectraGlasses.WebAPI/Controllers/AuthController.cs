using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Repositories.Models;
using Services.GlassesService;
using SpectraGlasses.WebAPI.Models;

namespace SpectraGlasses.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public AuthController(
            IAccountService accountService,
            IUserService userService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _accountService = accountService;
            _userService = userService;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
        }

        /// <summary>
        /// Authenticates a user and returns a JWT token
        /// </summary>
        /// <param name="request">Login credentials</param>
        /// <returns>JWT token and user information</returns>
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "INVALID_REQUEST",
                    Message = "Email and password are required"
                });
            }

            // Hash the password for comparison
            var passwordHash = HashPassword(request.Password);

            // Get user by email and password hash
            var user = await _accountService.GetUser(request.Email, passwordHash);

            // If not found with hash, try plain password (for backward compatibility)
            if (user == null)
            {
                user = await _accountService.GetUser(request.Email, request.Password);
            }

            if (user == null)
            {
                return Unauthorized(new ErrorResponse
                {
                    ErrorCode = "INVALID_CREDENTIALS",
                    Message = "Invalid email or password"
                });
            }

            // Check if user account is active
            if (user.Status?.ToLower() != "active")
            {
                return Unauthorized(new ErrorResponse
                {
                    ErrorCode = "ACCOUNT_INACTIVE",
                    Message = "Your account is not active. Please contact support."
                });
            }

            // Generate JWT token
            var token = GenerateJwtToken(user);

            return Ok(new LoginResponse
            {
                Token = token,
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role
            });
        }

        /// <summary>
        /// Registers a new customer account
        /// </summary>
        /// <param name="request">Registration information</param>
        /// <returns>JWT token and user information</returns>
        [HttpPost("register")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
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

            // Validate email format
            if (!IsValidEmail(request.Email))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Invalid email format"
                });
            }

            // Validate password
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Password is required"
                });
            }

            if (request.Password.Length < 6)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "Password must be at least 6 characters"
                });
            }

            // Check if email already exists
            var existingUser = await _userService.GetUserByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "EMAIL_EXISTS",
                    Message = "An account with this email already exists"
                });
            }

            // Hash password
            var passwordHash = HashPassword(request.Password);

            // Create user
            var user = new User
            {
                Email = request.Email,
                PasswordHash = passwordHash,
                FullName = request.FullName,
                Phone = request.Phone,
                Role = "customer",
                Status = "active"
            };

            var createdUser = await _userService.CreateUserAsync(user);

            // Generate JWT token
            var token = GenerateJwtToken(createdUser);

            return CreatedAtAction(nameof(Login), new LoginResponse
            {
                Token = token,
                UserId = createdUser.UserId,
                Email = createdUser.Email,
                FullName = createdUser.FullName,
                Role = createdUser.Role
            });
        }

        /// <summary>
        /// Login or register using Google Firebase authentication
        /// </summary>
        /// <param name="request">Google login information</param>
        /// <returns>JWT token and user information</returns>
        [HttpPost("google")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.IdToken))
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "VALIDATION_ERROR",
                    Message = "ID token is required"
                });
            }

            try
            {
                // Verify the Firebase ID token
                var firebaseUser = await VerifyFirebaseTokenAsync(request.IdToken);

                if (firebaseUser == null)
                {
                    return Unauthorized(new ErrorResponse
                    {
                        ErrorCode = "INVALID_TOKEN",
                        Message = "Invalid or expired Firebase token"
                    });
                }

                // Check if user exists
                var existingUser = await _userService.GetUserByEmailAsync(firebaseUser.Email);

                User user;
                if (existingUser != null)
                {
                    // User exists, check if active
                    if (existingUser.Status?.ToLower() != "active")
                    {
                        return Unauthorized(new ErrorResponse
                        {
                            ErrorCode = "ACCOUNT_INACTIVE",
                            Message = "Your account is not active. Please contact support."
                        });
                    }
                    user = existingUser;
                }
                else
                {
                    // Create new user
                    user = new User
                    {
                        Email = firebaseUser.Email,
                        FullName = firebaseUser.Name,
                        PasswordHash = GenerateRandomPassword(), // Random password for Google users
                        Role = "customer",
                        Status = "active"
                    };

                    user = await _userService.CreateUserAsync(user);
                }

                // Generate JWT token
                var token = GenerateJwtToken(user);

                return Ok(new LoginResponse
                {
                    Token = token,
                    UserId = user.UserId,
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = user.Role
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ErrorResponse
                {
                    ErrorCode = "GOOGLE_AUTH_ERROR",
                    Message = $"Google authentication failed: {ex.Message}"
                });
            }
        }

        #region Helper Methods

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? "")
            };

            if (!string.IsNullOrEmpty(user.Role))
            {
                claims.Add(new Claim(ClaimTypes.Role, user.Role));
            }

            if (!string.IsNullOrEmpty(user.FullName))
            {
                claims.Add(new Claim(ClaimTypes.Name, user.FullName));
            }

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private static string GenerateRandomPassword()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private async Task<FirebaseUser?> VerifyFirebaseTokenAsync(string idToken)
        {
            try
            {
                var projectId = _configuration["Firebase:ProjectId"];

                // Verify token with Google's public keys
                var verifyUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:lookup?key={_configuration["Firebase:ApiKey"]}";

                var requestBody = new { idToken };
                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(verifyUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<FirebaseVerifyResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Users == null || result.Users.Length == 0)
                {
                    return null;
                }

                var firebaseUserData = result.Users[0];

                return new FirebaseUser
                {
                    Uid = firebaseUserData.LocalId,
                    Email = firebaseUserData.Email,
                    Name = firebaseUserData.DisplayName ?? firebaseUserData.Email?.Split('@')[0]
                };
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Helper Classes

        private class FirebaseUser
        {
            public string Uid { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? Name { get; set; }
        }

        private class FirebaseVerifyResponse
        {
            public FirebaseUserData[]? Users { get; set; }
        }

        private class FirebaseUserData
        {
            public string LocalId { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string? DisplayName { get; set; }
        }

        #endregion
    }
}
