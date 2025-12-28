using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TRAVIL.Models;
using TRAVEL.Services;

namespace TRAVEL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IAuthenticationService authService, ILogger<AccountController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid input data", errors = ModelState });
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

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid input data", errors = ModelState });
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

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");

            if (userId == 0)
            {
                return Unauthorized();
            }

            _logger.LogInformation($"User {userId} logged out");

            await _authService.LogoutAsync(userId);

            // Clear authentication cookie
            Response.Cookies.Delete("authToken");

            return Ok(new { success = true, message = "Logged out successfully" });
        }

        [HttpGet("verify-token")]
        [Authorize]
        public async Task<IActionResult> VerifyToken()
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var isValid = await _authService.ValidateTokenAsync(token);

            if (isValid)
            {
                var userId = User.FindFirst("UserId")?.Value;
                return Ok(new { success = true, message = "Token is valid", userId });
            }

            return Unauthorized(new { success = false, message = "Invalid token" });
        }

        [HttpGet("current-user")]
        [Authorize]
        public IActionResult GetCurrentUser()
        {
            var userId = User.FindFirst("UserId")?.Value;
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            return Ok(new
            {
                success = true,
                userId,
                email,
                name,
                role
            });
        }
    }
}