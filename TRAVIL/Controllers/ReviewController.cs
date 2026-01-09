using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TRAVEL.Models;
using TRAVEL.Data;
using TRAVEL.Models;

namespace TRAVIL.Controllers
{
    [ApiController]
    [Route("api/review")]
    public class ReviewController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        #region Package Reviews

        /// <summary>
        /// Get reviews for a specific package
        /// GET /api/review/package/{packageId}
        /// </summary>
        [HttpGet("package/{packageId}")]
        public async Task<IActionResult> GetPackageReviews(int packageId)
        {
            try
            {
                var reviews = await _context.Reviews
                    .Where(r => r.PackageId == packageId && r.IsApproved)
                    .OrderByDescending(r => r.CreatedAt)
                    .Include(r => r.User)
                    .Select(r => new
                    {
                        r.ReviewId,
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
        /// Submit a package review
        /// POST /api/review/package
        /// </summary>
        [HttpPost("package")]
        [Authorize]
        public async Task<IActionResult> CreatePackageReview([FromBody] CreatePackageReviewDto dto)
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

                // Check if package exists
                var packageExists = await _context.TravelPackages.AnyAsync(p => p.PackageId == dto.PackageId);
                if (!packageExists)
                {
                    return NotFound(new { success = false, message = "Package not found" });
                }

                // Check if user already reviewed this package
                var existingReview = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.PackageId == dto.PackageId);

                if (existingReview != null)
                {
                    existingReview.Rating = dto.Rating;
                    existingReview.Comment = dto.Comment;
                    existingReview.CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                    await _context.SaveChangesAsync();
                    return Ok(new { success = true, message = "Review updated", data = existingReview });
                }

                var review = new Review
                {
                    UserId = userId,
                    PackageId = dto.PackageId,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    ReviewType = ReviewType.Package,
                    IsApproved = true,
                    CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                };

                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Review submitted", data = review });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to submit review", error = ex.Message });
            }
        }

        #endregion

        #region Site Reviews (Testimonials for Homepage)

        /// <summary>
        /// Get approved site reviews for homepage testimonials
        /// GET /api/review/site
        /// </summary>
        [HttpGet("site")]
        public async Task<IActionResult> GetSiteReviews([FromQuery] int limit = 10)
        {
            try
            {
                var reviews = await _context.Reviews
                    .Where(r => r.ReviewType == ReviewType.Site && r.IsApproved)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(limit)
                    .Include(r => r.User)
                    .Select(r => new
                    {
                        r.ReviewId,
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
        /// Submit a site review (testimonial)
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

                var existingReview = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.ReviewType == ReviewType.Site);

                if (existingReview != null)
                {
                    existingReview.Rating = dto.Rating;
                    existingReview.Comment = dto.Comment;
                    existingReview.CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
                    await _context.SaveChangesAsync();
                    return Ok(new { success = true, message = "Review updated", data = existingReview });
                }

                var review = new Review
                {
                    UserId = userId,
                    PackageId = null,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    ReviewType = ReviewType.Site,
                    IsApproved = true,
                    CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                };

                _context.Reviews.Add(review);
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

                var review = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.ReviewType == ReviewType.Site);

                if (review == null)
                {
                    return NotFound(new { success = false, message = "Review not found" });
                }

                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Review deleted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to delete review", error = ex.Message });
            }
        }

        #endregion

        #region Admin Endpoints

        /// <summary>
        /// Admin: Get all reviews
        /// GET /api/review/admin
        /// </summary>
        [HttpGet("admin")]
        [Authorize]
        public async Task<IActionResult> GetAllReviewsAdmin([FromQuery] string? type = null)
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
                    ?? User.FindFirst("Role")?.Value;

                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var query = _context.Reviews.AsQueryable();

                if (type == "site")
                {
                    query = query.Where(r => r.ReviewType == ReviewType.Site);
                }
                else if (type == "package")
                {
                    query = query.Where(r => r.ReviewType == ReviewType.Package);
                }

                var reviews = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .Include(r => r.User)
                    .Include(r => r.TravelPackage)
                    .Select(r => new
                    {
                        r.ReviewId,
                        r.UserId,
                        r.PackageId,
                        r.Rating,
                        r.Comment,
                        r.ReviewType,
                        r.IsApproved,
                        r.CreatedAt,
                        UserName = r.User != null ? r.User.FirstName + " " + r.User.LastName : "Unknown",
                        UserEmail = r.User != null ? r.User.Email : null,
                        PackageName = r.TravelPackage != null ? r.TravelPackage.Destination : null
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
        /// Admin: Toggle review approval
        /// PUT /api/review/{id}/approve
        /// </summary>
        [HttpPut("{id}/approve")]
        [Authorize]
        public async Task<IActionResult> ToggleApproval(int id, [FromBody] ToggleApprovalDto dto)
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
                    ?? User.FindFirst("Role")?.Value;

                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var review = await _context.Reviews.FindAsync(id);
                if (review == null)
                {
                    return NotFound(new { success = false, message = "Review not found" });
                }

                review.IsApproved = dto.IsApproved;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Review updated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to update review", error = ex.Message });
            }
        }

        /// <summary>
        /// Admin: Delete any review
        /// DELETE /api/review/{id}
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteReview(int id)
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value
                    ?? User.FindFirst("Role")?.Value;

                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var review = await _context.Reviews.FindAsync(id);
                if (review == null)
                {
                    return NotFound(new { success = false, message = "Review not found" });
                }

                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Review deleted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to delete review", error = ex.Message });
            }
        }

        #endregion
    }

    // DTOs
    public class CreatePackageReviewDto
    {
        public int PackageId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
    }

    public class CreateSiteReviewDto
    {
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
    }

    public class ToggleApprovalDto
    {
        public bool IsApproved { get; set; }
    }
}