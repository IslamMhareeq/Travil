using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRAVEL.Data;
using TRAVEL.Models;

namespace TRAVEL.Services
{
    public interface IReviewService
    {
        Task<ReviewResult> CreatePackageReviewAsync(int userId, int packageId, int rating, string comment);
        Task<ReviewResult> CreateSiteReviewAsync(int userId, int rating, string comment);
        Task<List<Review>> GetPackageReviewsAsync(int packageId, bool approvedOnly = true);
        Task<List<Review>> GetSiteReviewsAsync(bool approvedOnly = true);
        Task<List<Review>> GetUserReviewsAsync(int userId);
        Task<bool> ApproveReviewAsync(int reviewId);
        Task<bool> DeleteReviewAsync(int reviewId);
        Task<double> GetPackageAverageRatingAsync(int packageId);
        Task<double> GetSiteAverageRatingAsync();
        Task<bool> CanUserReviewPackageAsync(int userId, int packageId);
        Task<List<Review>> GetPendingReviewsAsync();
        Task<ReviewStats> GetReviewStatsAsync(int? packageId = null);
    }

    public class ReviewService : IReviewService
    {
        private readonly TravelDbContext _context;
        private readonly ILogger<ReviewService> _logger;

        public ReviewService(TravelDbContext context, ILogger<ReviewService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ReviewResult> CreatePackageReviewAsync(int userId, int packageId, int rating, string comment)
        {
            // Validate rating
            if (rating < 1 || rating > 5)
                return new ReviewResult { Success = false, Message = "Rating must be between 1 and 5" };

            // Check if user can review this package (must have completed booking)
            var canReview = await CanUserReviewPackageAsync(userId, packageId);
            if (!canReview)
                return new ReviewResult { Success = false, Message = "You can only review packages you have traveled with" };

            // Check if user already reviewed this package
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.UserId == userId && r.PackageId == packageId);

            if (existingReview != null)
                return new ReviewResult { Success = false, Message = "You have already reviewed this package" };

            var review = new Review
            {
                UserId = userId,
                PackageId = packageId,
                Rating = rating,
                Comment = comment,
                ReviewType = "Package",
                CreatedAt = DateTime.UtcNow,
                IsApproved = false // Requires admin approval
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Package review created by user {userId} for package {packageId}");

            return new ReviewResult
            {
                Success = true,
                Message = "Thank you for your review! It will be visible after approval.",
                Review = review
            };
        }

        public async Task<ReviewResult> CreateSiteReviewAsync(int userId, int rating, string comment)
        {
            // Validate rating
            if (rating < 1 || rating > 5)
                return new ReviewResult { Success = false, Message = "Rating must be between 1 and 5" };

            // Check if user exists
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return new ReviewResult { Success = false, Message = "User not found" };

            // Check if user already has a site review
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.UserId == userId && r.ReviewType == "Site");

            if (existingReview != null)
            {
                // Update existing review
                existingReview.Rating = rating;
                existingReview.Comment = comment;
                existingReview.CreatedAt = DateTime.UtcNow;
                existingReview.IsApproved = false;

                await _context.SaveChangesAsync();

                return new ReviewResult
                {
                    Success = true,
                    Message = "Your review has been updated!",
                    Review = existingReview
                };
            }

            var review = new Review
            {
                UserId = userId,
                PackageId = null,
                Rating = rating,
                Comment = comment,
                ReviewType = "Site",
                CreatedAt = DateTime.UtcNow,
                IsApproved = false
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Site review created by user {userId}");

            return new ReviewResult
            {
                Success = true,
                Message = "Thank you for your feedback!",
                Review = review
            };
        }

        public async Task<List<Review>> GetPackageReviewsAsync(int packageId, bool approvedOnly = true)
        {
            var query = _context.Reviews
                .Include(r => r.User)
                .Where(r => r.PackageId == packageId);

            if (approvedOnly)
                query = query.Where(r => r.IsApproved);

            return await query
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Review>> GetSiteReviewsAsync(bool approvedOnly = true)
        {
            var query = _context.Reviews
                .Include(r => r.User)
                .Where(r => r.ReviewType == "Site");

            if (approvedOnly)
                query = query.Where(r => r.IsApproved);

            return await query
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Review>> GetUserReviewsAsync(int userId)
        {
            return await _context.Reviews
                .Include(r => r.TravelPackage)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> ApproveReviewAsync(int reviewId)
        {
            var review = await _context.Reviews.FindAsync(reviewId);
            if (review == null)
                return false;

            review.IsApproved = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Review {reviewId} approved");
            return true;
        }

        public async Task<bool> DeleteReviewAsync(int reviewId)
        {
            var review = await _context.Reviews.FindAsync(reviewId);
            if (review == null)
                return false;

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Review {reviewId} deleted");
            return true;
        }

        public async Task<double> GetPackageAverageRatingAsync(int packageId)
        {
            var reviews = await _context.Reviews
                .Where(r => r.PackageId == packageId && r.IsApproved)
                .ToListAsync();

            if (!reviews.Any())
                return 0;

            return reviews.Average(r => r.Rating);
        }

        public async Task<double> GetSiteAverageRatingAsync()
        {
            var reviews = await _context.Reviews
                .Where(r => r.ReviewType == "Site" && r.IsApproved)
                .ToListAsync();

            if (!reviews.Any())
                return 0;

            return reviews.Average(r => r.Rating);
        }

        public async Task<bool> CanUserReviewPackageAsync(int userId, int packageId)
        {
            // User must have a completed or confirmed booking for this package
            return await _context.Bookings.AnyAsync(b =>
                b.UserId == userId &&
                b.PackageId == packageId &&
                (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed));
        }

        public async Task<List<Review>> GetPendingReviewsAsync()
        {
            return await _context.Reviews
                .Include(r => r.User)
                .Include(r => r.TravelPackage)
                .Where(r => !r.IsApproved)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<ReviewStats> GetReviewStatsAsync(int? packageId = null)
        {
            var query = _context.Reviews.AsQueryable();

            if (packageId.HasValue)
                query = query.Where(r => r.PackageId == packageId);

            var reviews = await query.Where(r => r.IsApproved).ToListAsync();
            var pendingCount = await query.CountAsync(r => !r.IsApproved);

            return new ReviewStats
            {
                TotalReviews = reviews.Count,
                PendingReviews = pendingCount,
                AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0,
                FiveStarCount = reviews.Count(r => r.Rating == 5),
                FourStarCount = reviews.Count(r => r.Rating == 4),
                ThreeStarCount = reviews.Count(r => r.Rating == 3),
                TwoStarCount = reviews.Count(r => r.Rating == 2),
                OneStarCount = reviews.Count(r => r.Rating == 1)
            };
        }
    }

    public class ReviewResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Review Review { get; set; }
    }

    public class ReviewStats
    {
        public int TotalReviews { get; set; }
        public int PendingReviews { get; set; }
        public double AverageRating { get; set; }
        public int FiveStarCount { get; set; }
        public int FourStarCount { get; set; }
        public int ThreeStarCount { get; set; }
        public int TwoStarCount { get; set; }
        public int OneStarCount { get; set; }
    }
}
