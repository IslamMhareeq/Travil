using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TRAVEL.Models;
using TRAVEL.Data;


namespace TRAVIL.Controllers
{
    [ApiController]
    [Route("api/packages")]
    public class PackagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PackagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all active packages with filters
        /// GET /api/packages
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPackages(
            [FromQuery] string? search = null,
            [FromQuery] int? category = null,
            [FromQuery] string? country = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? sort = null,
            [FromQuery] bool? sale = null,
            [FromQuery] int limit = 50)
        {
            try
            {
                var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

                var query = _context.TravelPackages
                    .Where(p => p.IsActive && p.EndDate >= today)
                    .AsQueryable();

                // Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(p =>
                        p.Destination.ToLower().Contains(searchLower) ||
                        p.Country.ToLower().Contains(searchLower) ||
                        (p.Description != null && p.Description.ToLower().Contains(searchLower)));
                }

                // Category filter
                if (category.HasValue)
                {
                    query = query.Where(p => (int)p.PackageType == category.Value);
                }

                // Country filter
                if (!string.IsNullOrEmpty(country))
                {
                    query = query.Where(p => p.Country.ToLower() == country.ToLower());
                }

                // Price range
                if (minPrice.HasValue)
                {
                    query = query.Where(p => (p.DiscountedPrice ?? p.Price) >= minPrice.Value);
                }
                if (maxPrice.HasValue)
                {
                    query = query.Where(p => (p.DiscountedPrice ?? p.Price) <= maxPrice.Value);
                }

                // Sale filter (discounted packages only)
                if (sale == true)
                {
                    query = query.Where(p => p.DiscountedPrice != null && p.DiscountedPrice < p.Price &&
                        (p.DiscountStartDate == null || p.DiscountStartDate <= today) &&
                        (p.DiscountEndDate == null || p.DiscountEndDate >= today));
                }

                // Sorting
                query = sort switch
                {
                    "price_asc" => query.OrderBy(p => p.DiscountedPrice ?? p.Price),
                    "price_desc" => query.OrderByDescending(p => p.DiscountedPrice ?? p.Price),
                    "date" => query.OrderBy(p => p.StartDate),
                    "popular" => query.OrderByDescending(p => p.Reviews.Count),
                    _ => query.OrderBy(p => p.StartDate)
                };

                var packages = await query
                    .Take(limit)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Include(p => p.Reviews.Where(r => r.IsApproved))
                    .Select(p => new
                    {
                        p.PackageId,
                        p.Destination,
                        p.Country,
                        p.Description,
                        p.Price,
                        DiscountedPrice = (p.DiscountedPrice != null &&
                            (p.DiscountStartDate == null || p.DiscountStartDate <= today) &&
                            (p.DiscountEndDate == null || p.DiscountEndDate >= today))
                            ? p.DiscountedPrice : null,
                        p.StartDate,
                        p.EndDate,
                        p.AvailableRooms,
                        p.PackageType,
                        p.MinimumAge,
                        p.MaximumAge,
                        p.Itinerary,
                        p.IsActive,
                        ImageUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault() ?? p.ImageUrl,
                        Images = p.Images.OrderBy(i => i.DisplayOrder).Select(i => new { i.ImageId, i.ImageUrl, i.AltText }).ToList(),
                        AverageRating = p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0,
                        ReviewCount = p.Reviews.Count
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = packages, count = packages.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to load packages", error = ex.Message });
            }
        }

        /// <summary>
        /// Get single package by ID
        /// GET /api/packages/{id}
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPackage(int id)
        {
            try
            {
                var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

                var package = await _context.TravelPackages
                    .Where(p => p.PackageId == id)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Include(p => p.Reviews.Where(r => r.IsApproved))
                        .ThenInclude(r => r.User)
                    .Select(p => new
                    {
                        p.PackageId,
                        p.Destination,
                        p.Country,
                        p.Description,
                        p.Price,
                        DiscountedPrice = (p.DiscountedPrice != null &&
                            (p.DiscountStartDate == null || p.DiscountStartDate <= today) &&
                            (p.DiscountEndDate == null || p.DiscountEndDate >= today))
                            ? p.DiscountedPrice : null,
                        p.DiscountStartDate,
                        p.DiscountEndDate,
                        p.StartDate,
                        p.EndDate,
                        p.AvailableRooms,
                        p.PackageType,
                        p.MinimumAge,
                        p.MaximumAge,
                        p.Itinerary,
                        p.IsActive,
                        p.CreatedAt,
                        ImageUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault() ?? p.ImageUrl,
                        Images = p.Images.OrderBy(i => i.DisplayOrder).Select(i => new { i.ImageId, i.ImageUrl, i.AltText }).ToList(),
                        Reviews = p.Reviews.Where(r => r.IsApproved).OrderByDescending(r => r.CreatedAt).Select(r => new
                        {
                            r.ReviewId,
                            r.Rating,
                            r.Comment,
                            r.CreatedAt,
                            UserName = r.User != null ? r.User.FirstName + " " + r.User.LastName : "Anonymous",
                            UserImage = r.User != null ? r.User.ProfileImageUrl : null
                        }).ToList(),
                        AverageRating = p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0,
                        ReviewCount = p.Reviews.Count
                    })
                    .FirstOrDefaultAsync();

                if (package == null)
                {
                    return NotFound(new { success = false, message = "Package not found" });
                }

                return Ok(new { success = true, data = package });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to load package", error = ex.Message });
            }
        }

        /// <summary>
        /// Get unique countries for filter dropdown
        /// GET /api/packages/countries
        /// </summary>
        [HttpGet("countries")]
        public async Task<IActionResult> GetCountries()
        {
            try
            {
                var countries = await _context.TravelPackages
                    .Where(p => p.IsActive)
                    .Select(p => p.Country)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                return Ok(new { success = true, data = countries });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to load countries", error = ex.Message });
            }
        }

        /// <summary>
        /// Get featured packages for homepage
        /// GET /api/packages/featured
        /// </summary>
        [HttpGet("featured")]
        public async Task<IActionResult> GetFeaturedPackages([FromQuery] int limit = 6)
        {
            try
            {
                var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

                var packages = await _context.TravelPackages
                    .Where(p => p.IsActive && p.EndDate >= today)
                    .OrderBy(p => p.StartDate)
                    .Take(limit)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Select(p => new
                    {
                        p.PackageId,
                        p.Destination,
                        p.Country,
                        p.Price,
                        DiscountedPrice = (p.DiscountedPrice != null &&
                            (p.DiscountStartDate == null || p.DiscountStartDate <= today) &&
                            (p.DiscountEndDate == null || p.DiscountEndDate >= today))
                            ? p.DiscountedPrice : null,
                        p.StartDate,
                        p.PackageType,
                        ImageUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault() ?? p.ImageUrl
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = packages });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to load packages", error = ex.Message });
            }
        }

        #region Admin Endpoints

        [HttpGet("admin")]
        [Authorize]
        public async Task<IActionResult> GetAllPackagesAdmin()
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("Role")?.Value;
                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var packages = await _context.TravelPackages
                    .OrderByDescending(p => p.CreatedAt)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Select(p => new
                    {
                        p.PackageId,
                        p.Destination,
                        p.Country,
                        p.Description,
                        p.Price,
                        p.DiscountedPrice,
                        p.DiscountStartDate,
                        p.DiscountEndDate,
                        p.StartDate,
                        p.EndDate,
                        p.AvailableRooms,
                        p.PackageType,
                        p.MinimumAge,
                        p.MaximumAge,
                        p.IsActive,
                        p.CreatedAt,
                        ImageUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault() ?? p.ImageUrl,
                        Images = p.Images.OrderBy(i => i.DisplayOrder).Select(i => new { i.ImageId, i.ImageUrl }).ToList()
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = packages });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to load packages", error = ex.Message });
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreatePackage([FromBody] CreatePackageDto dto)
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("Role")?.Value;
                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var package = new TravelPackage
                {
                    Destination = dto.Destination,
                    Country = dto.Country,
                    Description = dto.Description,
                    Price = dto.Price,
                    DiscountedPrice = dto.DiscountedPrice,
                    DiscountStartDate = dto.DiscountStartDate.HasValue ? DateTime.SpecifyKind(dto.DiscountStartDate.Value, DateTimeKind.Utc) : null,
                    DiscountEndDate = dto.DiscountEndDate.HasValue ? DateTime.SpecifyKind(dto.DiscountEndDate.Value, DateTimeKind.Utc) : null,
                    StartDate = DateTime.SpecifyKind(dto.StartDate, DateTimeKind.Utc),
                    EndDate = DateTime.SpecifyKind(dto.EndDate, DateTimeKind.Utc),
                    AvailableRooms = dto.AvailableRooms,
                    PackageType = dto.PackageType,
                    MinimumAge = dto.MinimumAge,
                    MaximumAge = dto.MaximumAge,
                    Itinerary = dto.Itinerary,
                    ImageUrl = dto.ImageUrl,
                    IsActive = dto.IsActive,
                    CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                };

                _context.TravelPackages.Add(package);
                await _context.SaveChangesAsync();

                if (dto.ImageUrls != null && dto.ImageUrls.Any())
                {
                    var order = 0;
                    foreach (var url in dto.ImageUrls)
                    {
                        _context.PackageImages.Add(new PackageImage
                        {
                            PackageId = package.PackageId,
                            ImageUrl = url,
                            DisplayOrder = order++
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true, message = "Package created", data = new { package.PackageId } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to create package", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdatePackage(int id, [FromBody] UpdatePackageDto dto)
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("Role")?.Value;
                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var package = await _context.TravelPackages.FindAsync(id);
                if (package == null)
                {
                    return NotFound(new { success = false, message = "Package not found" });
                }

                package.Destination = dto.Destination ?? package.Destination;
                package.Country = dto.Country ?? package.Country;
                package.Description = dto.Description ?? package.Description;
                package.Price = dto.Price ?? package.Price;
                package.DiscountedPrice = dto.DiscountedPrice;
                package.DiscountStartDate = dto.DiscountStartDate.HasValue ? DateTime.SpecifyKind(dto.DiscountStartDate.Value, DateTimeKind.Utc) : package.DiscountStartDate;
                package.DiscountEndDate = dto.DiscountEndDate.HasValue ? DateTime.SpecifyKind(dto.DiscountEndDate.Value, DateTimeKind.Utc) : package.DiscountEndDate;
                package.StartDate = dto.StartDate.HasValue ? DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Utc) : package.StartDate;
                package.EndDate = dto.EndDate.HasValue ? DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Utc) : package.EndDate;
                package.AvailableRooms = dto.AvailableRooms ?? package.AvailableRooms;
                package.PackageType = dto.PackageType ?? package.PackageType;
                package.MinimumAge = dto.MinimumAge ?? package.MinimumAge;
                package.MaximumAge = dto.MaximumAge ?? package.MaximumAge;
                package.Itinerary = dto.Itinerary ?? package.Itinerary;
                package.IsActive = dto.IsActive ?? package.IsActive;
                package.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                if (!string.IsNullOrEmpty(dto.ImageUrl))
                {
                    package.ImageUrl = dto.ImageUrl;
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Package updated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to update package", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeletePackage(int id)
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("Role")?.Value;
                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var package = await _context.TravelPackages.FindAsync(id);
                if (package == null)
                {
                    return NotFound(new { success = false, message = "Package not found" });
                }

                _context.TravelPackages.Remove(package);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Package deleted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to delete package", error = ex.Message });
            }
        }

        [HttpPut("{id}/price")]
        [Authorize]
        public async Task<IActionResult> UpdatePackagePrice(int id, [FromBody] UpdatePriceDto dto)
        {
            try
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("Role")?.Value;
                if (roleClaim != "0" && roleClaim?.ToLower() != "admin")
                {
                    return Forbid();
                }

                var package = await _context.TravelPackages.FindAsync(id);
                if (package == null)
                {
                    return NotFound(new { success = false, message = "Package not found" });
                }

                package.Price = dto.Price;
                package.DiscountedPrice = dto.DiscountedPrice;
                package.DiscountStartDate = dto.DiscountStartDate.HasValue ? DateTime.SpecifyKind(dto.DiscountStartDate.Value, DateTimeKind.Utc) : null;
                package.DiscountEndDate = dto.DiscountEndDate.HasValue ? DateTime.SpecifyKind(dto.DiscountEndDate.Value, DateTimeKind.Utc) : null;
                package.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Price updated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Failed to update price", error = ex.Message });
            }
        }

        #endregion
    }

    public class CreatePackageDto
    {
        public string Destination { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public DateTime? DiscountStartDate { get; set; }
        public DateTime? DiscountEndDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int AvailableRooms { get; set; }
        public PackageType PackageType { get; set; }
        public int? MinimumAge { get; set; }
        public int? MaximumAge { get; set; }
        public string? Itinerary { get; set; }
        public string? ImageUrl { get; set; }
        public List<string>? ImageUrls { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdatePackageDto
    {
        public string? Destination { get; set; }
        public string? Country { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public DateTime? DiscountStartDate { get; set; }
        public DateTime? DiscountEndDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? AvailableRooms { get; set; }
        public PackageType? PackageType { get; set; }
        public int? MinimumAge { get; set; }
        public int? MaximumAge { get; set; }
        public string? Itinerary { get; set; }
        public string? ImageUrl { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UpdatePriceDto
    {
        public decimal Price { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public DateTime? DiscountStartDate { get; set; }
        public DateTime? DiscountEndDate { get; set; }
    }
}