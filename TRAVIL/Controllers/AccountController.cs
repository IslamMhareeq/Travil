using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TRAVEL.Models;
using TRAVEL.Data;
using TRAVEL.Models;

namespace TRAVIL.Controllers
{
    [ApiController]
    [Route("api/account")]
    public class AccountController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get current user info
        /// GET /api/account/current-user
        /// </summary>
        [HttpGet("current-user")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("UserId")?.Value
                    ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Invalid token" });
                }

                var user = await _context.Users
                    .Where(u => u.UserId == userId)
                    .Select(u => new
                    {
                        u.UserId,
                        u.Email,
                        u.FirstName,
                        u.LastName,
                        u.PhoneNumber,
                        u.ProfileImageUrl,
                        u.Role,
                        u.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                return Ok(new { success = true, user });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to get user", error = ex.Message });
            }
        }

        /// <summary>
        /// Update user profile
        /// PUT /api/account/update-profile
        /// </summary>
        [HttpPut("update-profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("UserId")?.Value
                    ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Invalid token" });
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                if (!string.IsNullOrEmpty(dto.FirstName))
                    user.FirstName = dto.FirstName;

                if (!string.IsNullOrEmpty(dto.LastName))
                    user.LastName = dto.LastName;

                if (!string.IsNullOrEmpty(dto.PhoneNumber))
                    user.PhoneNumber = dto.PhoneNumber;

                if (dto.ProfileImageUrl != null)
                    user.ProfileImageUrl = dto.ProfileImageUrl;

                if (!string.IsNullOrEmpty(dto.CurrentPassword) && !string.IsNullOrEmpty(dto.NewPassword))
                {
                    if (!VerifyPassword(dto.CurrentPassword, user.PasswordHash))
                    {
                        return BadRequest(new { success = false, message = "Current password is incorrect" });
                    }
                    user.PasswordHash = HashPassword(dto.NewPassword);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Profile updated successfully",
                    user = new
                    {
                        user.UserId,
                        user.Email,
                        user.FirstName,
                        user.LastName,
                        user.PhoneNumber,
                        user.ProfileImageUrl
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to update profile", error = ex.Message });
            }
        }

        /// <summary>
        /// Get user stats
        /// GET /api/account/stats
        /// </summary>
        [HttpGet("stats")]
        [Authorize]
        public async Task<IActionResult> GetUserStats()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("UserId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Invalid token" });
                }

                var now = DateTime.UtcNow;

                var bookings = await _context.Bookings
                    .Where(b => b.UserId == userId)
                    .Include(b => b.TravelPackage)
                    .ToListAsync();

                var stats = new
                {
                    TotalBookings = bookings.Count,
                    UpcomingTrips = bookings.Count(b => b.Status != BookingStatus.Cancelled &&
                        (b.TravelPackage?.StartDate ?? DateTime.MaxValue) > now),
                    CompletedTrips = bookings.Count(b => b.Status != BookingStatus.Cancelled &&
                        (b.TravelPackage?.EndDate ?? DateTime.MinValue) < now),
                    CancelledTrips = bookings.Count(b => b.Status == BookingStatus.Cancelled),
                    TotalSpent = bookings.Where(b => b.Status != BookingStatus.Cancelled)
                        .Sum(b => b.TotalPrice)
                };

                return Ok(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to get stats", error = ex.Message });
            }
        }

        #region Admin User Management

        [HttpGet("admin/users")]
        [Authorize]
        public async Task<IActionResult> GetAllUsersAdmin()
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("Role")?.Value;
                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var users = await _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Select(u => new
                    {
                        u.UserId,
                        u.Email,
                        u.FirstName,
                        u.LastName,
                        u.PhoneNumber,
                        u.ProfileImageUrl,
                        u.Role,
                        u.CreatedAt,
                        BookingsCount = _context.Bookings.Count(b => b.UserId == u.UserId)
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = users });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to load users", error = ex.Message });
            }
        }

        [HttpGet("admin/users/{id}")]
        [Authorize]
        public async Task<IActionResult> GetUserAdmin(int id)
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("Role")?.Value;
                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var user = await _context.Users
                    .Where(u => u.UserId == id)
                    .Select(u => new
                    {
                        u.UserId,
                        u.Email,
                        u.FirstName,
                        u.LastName,
                        u.PhoneNumber,
                        u.ProfileImageUrl,
                        u.Role,
                        u.CreatedAt,
                        Bookings = _context.Bookings
                            .Where(b => b.UserId == u.UserId)
                            .OrderByDescending(b => b.CreatedAt)
                            .Select(b => new
                            {
                                b.BookingId,
                                b.BookingReference,
                                b.Status,
                                b.TotalPrice,
                                b.CreatedAt,
                                PackageName = b.TravelPackage != null ? b.TravelPackage.Destination : null
                            })
                            .ToList()
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                return Ok(new { success = true, data = user });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to load user", error = ex.Message });
            }
        }

        [HttpPut("admin/users/{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateUserAdmin(int id, [FromBody] AdminUpdateUserDto dto)
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("Role")?.Value;
                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                if (!string.IsNullOrEmpty(dto.FirstName))
                    user.FirstName = dto.FirstName;

                if (!string.IsNullOrEmpty(dto.LastName))
                    user.LastName = dto.LastName;

                if (!string.IsNullOrEmpty(dto.PhoneNumber))
                    user.PhoneNumber = dto.PhoneNumber;

                if (dto.Role.HasValue)
                    user.Role = dto.Role.Value;

                if (!string.IsNullOrEmpty(dto.NewPassword))
                    user.PasswordHash = HashPassword(dto.NewPassword);

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "User updated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to update user", error = ex.Message });
            }
        }

        [HttpDelete("admin/users/{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteUserAdmin(int id)
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("Role")?.Value;
                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "User deleted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to delete user", error = ex.Message });
            }
        }

        #endregion

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
    }

    public class UpdateProfileDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }

    public class AdminUpdateUserDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public UserRole? Role { get; set; }
        public string? NewPassword { get; set; }
    }
}