using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TRAVEL.Data;
using TRAVEL.Models;

namespace TRAVEL.Controllers
{
    [ApiController]
    [Route("api/review")]
    public class SiteReviewController : ControllerBase
    {
        private readonly TravelDbContext _context;

        public SiteReviewController(TravelDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all approved site reviews for homepage display
        /// GET /api/review/site
        /// </summary>
        [HttpGet("site")]
        public async Task<IActionResult> GetSiteReviews([FromQuery] int limit = 10)
        {
            try
            {
                var reviews = await _context.SiteReviews
                    .Where(r => r.IsApproved && r.IsVisible)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(limit)
                    .Include(r => r.User)
                    .Select(r => new
                    {
                        r.SiteReviewId,
                        r.Rating,
                        r.Comment,
                        r.CreatedAt,
                        User = r.User != null ? new
                        {
                            r.User.UserId,
                            r.User.FirstName,
                            r.User.LastName,
                            r.User.ProfileImageUrl
                        } : null
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = reviews });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to load reviews", error = ex.Message });
            }
        }

        /// <summary>
        /// Submit a site review (authenticated users only)
        /// POST /api/review/site
        /// </summary>
        [HttpPost("site")]
        [Authorize]
        public async Task<IActionResult> CreateSiteReview([FromBody] CreateSiteReviewDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("UserId")?.Value
                    ?? User.FindFirst("sub")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Invalid user token" });
                }

                // Check if user already submitted a site review
                var existingReview = await _context.SiteReviews
                    .FirstOrDefaultAsync(r => r.UserId == userId);

                if (existingReview != null)
                {
                    // Update existing review
                    existingReview.Rating = dto.Rating;
                    existingReview.Comment = dto.Comment;
                    existingReview.CreatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    return Ok(new { success = true, message = "Review updated successfully", data = existingReview });
                }

                // Create new review
                var review = new SiteReview
                {
                    UserId = userId,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    CreatedAt = DateTime.UtcNow,
                    IsApproved = true,
                    IsVisible = true
                };

                _context.SiteReviews.Add(review);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Thank you for your feedback!", data = review });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to submit review", error = ex.Message });
            }
        }

        /// <summary>
        /// Delete user's own site review
        /// DELETE /api/review/site
        /// </summary>
        [HttpDelete("site")]
        [Authorize]
        public async Task<IActionResult> DeleteSiteReview()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("UserId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Invalid user token" });
                }

                var review = await _context.SiteReviews
                    .FirstOrDefaultAsync(r => r.UserId == userId);

                if (review == null)
                {
                    return NotFound(new { success = false, message = "Review not found" });
                }

                _context.SiteReviews.Remove(review);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Review deleted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to delete review", error = ex.Message });
            }
        }

        /// <summary>
        /// Admin: Get all site reviews (including non-approved)
        /// GET /api/review/site/admin
        /// </summary>
        [HttpGet("site/admin")]
        [Authorize]
        public async Task<IActionResult> GetAllSiteReviewsAdmin()
        {
            try
            {
                // Check if user is admin
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
                    ?? User.FindFirst("Role")?.Value;

                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var reviews = await _context.SiteReviews
                    .OrderByDescending(r => r.CreatedAt)
                    .Include(r => r.User)
                    .Select(r => new
                    {
                        r.SiteReviewId,
                        r.UserId,
                        r.Rating,
                        r.Comment,
                        r.CreatedAt,
                        r.IsApproved,
                        r.IsVisible,
                        UserName = r.User != null ? r.User.FirstName + " " + r.User.LastName : "Unknown",
                        UserEmail = r.User != null ? r.User.Email : null
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = reviews });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to load reviews", error = ex.Message });
            }
        }

        /// <summary>
        /// Admin: Toggle review approval/visibility
        /// PUT /api/review/site/{id}/toggle
        /// </summary>
        [HttpPut("site/{id}/toggle")]
        [Authorize]
        public async Task<IActionResult> ToggleSiteReview(int id, [FromBody] ToggleReviewDto dto)
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
                    ?? User.FindFirst("Role")?.Value;

                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var review = await _context.SiteReviews.FindAsync(id);
                if (review == null)
                {
                    return NotFound(new { success = false, message = "Review not found" });
                }

                if (dto.Field == "approved")
                {
                    review.IsApproved = dto.Value;
                }
                else if (dto.Field == "visible")
                {
                    review.IsVisible = dto.Value;
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Review updated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to update review", error = ex.Message });
            }
        }

        /// <summary>
        /// Admin: Delete any site review
        /// DELETE /api/review/site/{id}
        /// </summary>
        [HttpDelete("site/{id}")]
        [Authorize]
        public async Task<IActionResult> AdminDeleteSiteReview(int id)
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
                    ?? User.FindFirst("Role")?.Value;

                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var review = await _context.SiteReviews.FindAsync(id);
                if (review == null)
                {
                    return NotFound(new { success = false, message = "Review not found" });
                }

                _context.SiteReviews.Remove(review);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Review deleted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to delete review", error = ex.Message });
            }
        }
    }

    // DTOs
    public class CreateSiteReviewDto
    {
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
    }

    public class ToggleReviewDto
    {
        public string Field { get; set; } = string.Empty; // "approved" or "visible"
        public bool Value { get; set; }
    }
}