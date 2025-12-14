using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TravelAgency.Data;
using TravelAgency.Models;

namespace TravelAgency.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: Add review
        [HttpPost]
        public async Task<IActionResult> Create(int packageId, int rating, string comment)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return BadRequest("User not found");

            // Check if user has booked this package
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.UserId == userId && b.PackageId == packageId && b.Status == "Confirmed");

            if (booking == null)
                return BadRequest("You must book this package to review it");

            // Check if already reviewed
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.UserId == userId && r.PackageId == packageId);

            if (existingReview != null)
                return BadRequest("You already reviewed this package");

            var review = new Review
            {
                UserId = userId,
                PackageId = packageId,
                Rating = Math.Max(1, Math.Min(5, rating)), // Ensure 1-5
                Comment = comment
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Review added successfully" });
        }

        // POST: Rate website
        [HttpPost]
        public async Task<IActionResult> RateWebsite(int rating, string comment)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var websiteRating = new WebsiteRating
            {
                UserId = userId,
                Rating = Math.Max(1, Math.Min(5, rating)),
                Comment = comment
            };

            _context.WebsiteRatings.Add(websiteRating);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Thank you for your feedback!" });
        }
    }
}