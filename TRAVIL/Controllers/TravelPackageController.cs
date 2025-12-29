using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TRAVEL.Models;
using TRAVEL.Services;

namespace TRAVEL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class TravelPackageController : ControllerBase
    {
        private readonly ITravelPackageService _packageService;
        private readonly ILogger<TravelPackageController> _logger;

        public TravelPackageController(
            ITravelPackageService packageService,
            ILogger<TravelPackageController> logger)
        {
            _packageService = packageService;
            _logger = logger;
        }

        /// <summary>
        /// Get all active packages
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetPackages()
        {
            var packages = await _packageService.GetActivePackagesAsync();
            return Ok(new { success = true, data = packages, count = packages.Count });
        }

        /// <summary>
        /// Get all packages (admin)
        /// </summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllPackages()
        {
            var packages = await _packageService.GetAllPackagesAsync();
            return Ok(new { success = true, data = packages, count = packages.Count });
        }

        /// <summary>
        /// Get package by ID
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPackage(int id)
        {
            var package = await _packageService.GetPackageByIdAsync(id);
            if (package == null)
                return NotFound(new { success = false, message = "Package not found" });

            // Calculate average rating
            double avgRating = 0;
            if (package.Reviews != null && package.Reviews.Count > 0)
            {
                double sum = 0;
                int count = 0;
                foreach (var review in package.Reviews)
                {
                    if (review.IsApproved)
                    {
                        sum += review.Rating;
                        count++;
                    }
                }
                if (count > 0)
                    avgRating = sum / count;
            }

            return Ok(new
            {
                success = true,
                data = package,
                averageRating = Math.Round(avgRating, 1),
                reviewCount = package.Reviews?.Count ?? 0,
                isOnSale = package.DiscountedPrice.HasValue &&
                          package.DiscountStartDate <= DateTime.UtcNow &&
                          package.DiscountEndDate >= DateTime.UtcNow
            });
        }

        /// <summary>
        /// Search packages with filters
        /// </summary>
        [HttpPost("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchPackages([FromBody] PackageSearchCriteria criteria)
        {
            var packages = await _packageService.SearchPackagesAsync(criteria);
            return Ok(new { success = true, data = packages, count = packages.Count });
        }

        /// <summary>
        /// Get discounted packages
        /// </summary>
        [HttpGet("on-sale")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDiscountedPackages()
        {
            var packages = await _packageService.GetDiscountedPackagesAsync();
            return Ok(new { success = true, data = packages, count = packages.Count });
        }

        /// <summary>
        /// Get popular packages
        /// </summary>
        [HttpGet("popular")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPopularPackages([FromQuery] int count = 10)
        {
            var packages = await _packageService.GetPopularPackagesAsync(count);
            return Ok(new { success = true, data = packages, count = packages.Count });
        }

        /// <summary>
        /// Get unique countries
        /// </summary>
        [HttpGet("countries")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCountries()
        {
            var packages = await _packageService.GetActivePackagesAsync();
            var countries = new HashSet<string>();
            foreach (var p in packages)
            {
                if (!string.IsNullOrEmpty(p.Country))
                    countries.Add(p.Country);
            }
            return Ok(new { success = true, data = countries });
        }

        /// <summary>
        /// Get unique destinations
        /// </summary>
        [HttpGet("destinations")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDestinations()
        {
            var packages = await _packageService.GetActivePackagesAsync();
            var destinations = new HashSet<string>();
            foreach (var p in packages)
            {
                if (!string.IsNullOrEmpty(p.Destination))
                    destinations.Add(p.Destination);
            }
            return Ok(new { success = true, data = destinations });
        }

        /// <summary>
        /// Create new package (Admin only)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreatePackage([FromBody] TravelPackageDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid data", errors = ModelState });

            // Validate required fields
            if (string.IsNullOrEmpty(dto.Destination))
                return BadRequest(new { success = false, message = "Destination is required" });
            if (string.IsNullOrEmpty(dto.Country))
                return BadRequest(new { success = false, message = "Country is required" });
            if (dto.StartDate <= DateTime.UtcNow)
                return BadRequest(new { success = false, message = "Start date must be in the future" });
            if (dto.EndDate <= dto.StartDate)
                return BadRequest(new { success = false, message = "End date must be after start date" });
            if (dto.Price <= 0)
                return BadRequest(new { success = false, message = "Price must be greater than 0" });
            if (dto.AvailableRooms <= 0)
                return BadRequest(new { success = false, message = "Available rooms must be greater than 0" });
            if (string.IsNullOrEmpty(dto.Description))
                return BadRequest(new { success = false, message = "Description is required" });

            var package = await _packageService.CreatePackageAsync(dto);

            _logger.LogInformation($"Package created: {package.Destination} by admin");

            return CreatedAtAction(nameof(GetPackage), new { id = package.PackageId },
                new { success = true, message = "Package created successfully", data = package });
        }

        /// <summary>
        /// Update package (Admin only)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePackage(int id, [FromBody] TravelPackageDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid data", errors = ModelState });

            var package = await _packageService.UpdatePackageAsync(id, dto);
            if (package == null)
                return NotFound(new { success = false, message = "Package not found" });

            _logger.LogInformation($"Package updated: {package.Destination} (ID: {id})");

            return Ok(new { success = true, message = "Package updated successfully", data = package });
        }

        /// <summary>
        /// Delete package (Admin only)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeletePackage(int id)
        {
            var result = await _packageService.DeletePackageAsync(id);
            if (!result)
                return BadRequest(new { success = false, message = "Cannot delete package. It may have active bookings." });

            _logger.LogInformation($"Package deleted: ID {id}");

            return Ok(new { success = true, message = "Package deleted successfully" });
        }

        /// <summary>
        /// Apply discount to package (Admin only)
        /// </summary>
        [HttpPost("{id}/discount")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApplyDiscount(int id, [FromBody] DiscountRequest request)
        {
            // Validate discount duration (max 7 days)
            var duration = request.EndDate - request.StartDate;
            if (duration.TotalDays > 7)
                return BadRequest(new { success = false, message = "Discount duration cannot exceed 7 days" });

            if (request.DiscountedPrice <= 0)
                return BadRequest(new { success = false, message = "Discounted price must be greater than 0" });

            var result = await _packageService.ApplyDiscountAsync(id, request.DiscountedPrice, request.StartDate, request.EndDate);
            if (!result)
                return BadRequest(new { success = false, message = "Failed to apply discount. Ensure discounted price is less than original price." });

            _logger.LogInformation($"Discount applied to package {id}");

            return Ok(new { success = true, message = "Discount applied successfully" });
        }

        /// <summary>
        /// Remove discount from package (Admin only)
        /// </summary>
        [HttpDelete("{id}/discount")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveDiscount(int id)
        {
            var result = await _packageService.RemoveDiscountAsync(id);
            if (!result)
                return NotFound(new { success = false, message = "Package not found" });

            _logger.LogInformation($"Discount removed from package {id}");

            return Ok(new { success = true, message = "Discount removed successfully" });
        }

        /// <summary>
        /// Get dashboard statistics (Admin only)
        /// </summary>
        [HttpGet("stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetStats()
        {
            var stats = await _packageService.GetDashboardStatsAsync();
            return Ok(new { success = true, data = stats });
        }
    }

    public class DiscountRequest
    {
        public decimal DiscountedPrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
