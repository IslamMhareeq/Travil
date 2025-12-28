using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TRAVEL.Models;
using TRAVEL.Services;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Account controller for user authentication and authorization
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AccountController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<AccountController> _logger;

        /// <summary>
        /// Constructor for AccountController
        /// </summary>
        public AccountController(
            IAuthenticationService authService,
            ILogger<AccountController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Registers a new user account
        /// </summary>
        /// <param name="request">Registration request with user details</param>
        /// <returns>Registration response with JWT token</returns>
        /// <response code="200">User registered successfully</response>
        /// <response code="400">Invalid registration data or email already exists</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid input data",
                    errors = ModelState
                });
            }

            _logger.LogInformation($"Registration attempt for email: {request.Email}");

            var result = await _authService.RegisterAsync(request);

            if (result.Success)
            {
                _logger.LogInformation($"User registered successfully: {request.Email}");
                return Ok(result);
            }

            _logger.LogWarning($"Registration failed for email: {request.Email}. Reason: {result.Message}");
            return BadRequest(result);
        }

        /// <summary>
        /// Authenticates a user and returns JWT token
        /// </summary>
        /// <param name="request">Login request with email and password</param>
        /// <returns>Login response with JWT token</returns>
        /// <response code="200">Login successful</response>
        /// <response code="401">Invalid credentials</response>
        /// <response code="400">Invalid input data</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid input data",
                    errors = ModelState
                });
            }

            _logger.LogInformation($"Login attempt for email: {request.Email}");

            var result = await _authService.LoginAsync(request);

            if (result.Success)
            {
                _logger.LogInformation($"User logged in successfully: {request.Email}");

                // Set JWT token in secure cookie (optional)
                Response.Cookies.Append("authToken", result.Token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(60)
                });

                return Ok(result);
            }

            _logger.LogWarning($"Login failed for email: {request.Email}");
            return Unauthorized(result);
        }

        /// <summary>
        /// Logs out the current user
        /// </summary>
        /// <returns>Logout confirmation message</returns>
        /// <response code="200">Logout successful</response>
        /// <response code="401">User not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "User not authenticated"
                });
            }

            _logger.LogInformation($"User {userId} logged out");

            await _authService.LogoutAsync(userId);

            // Clear authentication cookie
            Response.Cookies.Delete("authToken");

            return Ok(new
            {
                success = true,
                message = "Logged out successfully"
            });
        }

        /// <summary>
        /// Verifies if the current JWT token is valid
        /// </summary>
        /// <returns>Token validation result</returns>
        /// <response code="200">Token is valid</response>
        /// <response code="401">Token is invalid or expired</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("verify-token")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> VerifyToken()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            var token = authHeader.Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "No token provided"
                });
            }

            var isValid = await _authService.ValidateTokenAsync(token);

            if (isValid)
            {
                var userId = User.FindFirst("UserId")?.Value;
                return Ok(new
                {
                    success = true,
                    message = "Token is valid",
                    userId = userId
                });
            }

            return Unauthorized(new
            {
                success = false,
                message = "Invalid or expired token"
            });
        }

        /// <summary>
        /// Gets the current authenticated user's information
        /// </summary>
        /// <returns>Current user information</returns>
        /// <response code="200">User information retrieved successfully</response>
        /// <response code="401">User not authenticated</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("current-user")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult GetCurrentUser()
        {
            var userId = User.FindFirst("UserId")?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var name = User.FindFirst(ClaimTypes.Name)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "User not authenticated"
                });
            }

            return Ok(new
            {
                success = true,
                userId = userId,
                email = email,
                name = name,
                role = role
            });
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        /// <returns>Health status</returns>
        /// <response code="200">API is healthy</response>
        [HttpGet("health")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Health()
        {
            return Ok(new
            {
                success = true,
                message = "API is healthy",
                timestamp = DateTime.UtcNow
            });
        }
    }
}