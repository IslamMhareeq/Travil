using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TravelAgency.Data;
using TravelAgency.Models;
using System.Security.Claims;

namespace TravelAgency.Controllers
{
    public class PackagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PackagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: All packages with search, filter, and sort
        public async Task<IActionResult> Index(string search, string destination, string country, decimal? minPrice, decimal? maxPrice, string sortBy)
        {
            var query = _context.Packages.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.Destination.Contains(search) || p.Country.Contains(search) || p.Description.Contains(search));

            if (!string.IsNullOrEmpty(destination))
                query = query.Where(p => p.Destination.Contains(destination));

            if (!string.IsNullOrEmpty(country))
                query = query.Where(p => p.Country.Contains(country));

            if (minPrice.HasValue)
                query = query.Where(p => p.Price >= minPrice);

            if (maxPrice.HasValue)
                query = query.Where(p => p.Price <= maxPrice);

            query = sortBy switch
            {
                "price-asc" => query.OrderBy(p => p.Price),
                "price-desc" => query.OrderByDescending(p => p.Price),
                "date" => query.OrderBy(p => p.StartDate),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            var packages = await query.ToListAsync();
            ViewBag.TotalPackages = packages.Count;

            return View(packages);
        }

        // GET: Package details
        public async Task<IActionResult> Details(int id)
        {
            var package = await _context.Packages
                .Include(p => p.Reviews)
                .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (package == null)
                return NotFound();

            var bookedRooms = await _context.Bookings.CountAsync(b => b.PackageId == id && b.Status == "Confirmed");
            ViewBag.AvailableRooms = package.RoomsAvailable - bookedRooms;
            ViewBag.AverageRating = package.Reviews.Any() ? Math.Round(package.Reviews.Average(r => r.Rating), 1) : 0;

            return View(package);
        }

        // POST: Create booking
        [HttpPost]
        public async Task<IActionResult> Book(int packageId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return BadRequest("User not found");

            // Check max 3 active bookings
            var activeBookings = await _context.Bookings.CountAsync(b => b.UserId == userId && b.Status == "Confirmed");
            if (activeBookings >= 3)
                return BadRequest("You already have 3 active bookings");

            // Check available rooms
            var package = await _context.Packages.FindAsync(packageId);
            if (package == null)
                return NotFound();

            var bookedRooms = await _context.Bookings.CountAsync(b => b.PackageId == packageId && b.Status == "Confirmed");
            
            if (bookedRooms >= package.RoomsAvailable)
                return BadRequest("No rooms available");

            // Check if already booked
            var existingBooking = await _context.Bookings.FirstOrDefaultAsync(b => b.UserId == userId && b.PackageId == packageId);
            if (existingBooking != null)
                return BadRequest("You already booked this package");

            var booking = new Booking 
            { 
                UserId = userId, 
                PackageId = packageId, 
                Status = "Confirmed",
                PaymentStatus = "Pending"
            };
            
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Booking successful!" });
        }
    }
}