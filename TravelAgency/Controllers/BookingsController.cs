using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TravelAgency.Data;
using TravelAgency.Models;

namespace TravelAgency.Controllers
{
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: User dashboard
        public async Task<IActionResult> Dashboard()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Redirect("/");

            var bookings = await _context.Bookings
                .Where(b => b.UserId == userId)
                .Include(b => b.Package)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }

        // POST: Cancel booking
        [HttpPost]
        public async Task<IActionResult> Cancel(int bookingId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId);

            if (booking == null)
                return NotFound();

            booking.Status = "Cancelled";
            _context.Update(booking);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Booking cancelled successfully" });
        }

        // GET: Download itinerary (placeholder)
        [HttpGet]
        public async Task<IActionResult> DownloadItinerary(int bookingId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var booking = await _context.Bookings
                .Include(b => b.Package)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId);

            if (booking == null)
                return NotFound();

            // Generate simple text itinerary
            var content = $@"
TRAVEL ITINERARY
================
Booking ID: {booking.Id}
Booking Date: {booking.BookingDate:MMMM dd, yyyy}

DESTINATION DETAILS
===================
Destination: {booking.Package.Destination}, {booking.Package.Country}
Travel Dates: {booking.Package.StartDate:MMMM dd, yyyy} - {booking.Package.EndDate:MMMM dd, yyyy}
Package Type: {booking.Package.PackageType}
Description: {booking.Package.Description}

PRICING
=======
Price: ${booking.Package.Price}
Status: {booking.Status}

Have a great trip!
";

            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            return File(bytes, "text/plain", $"itinerary_{booking.Id}.txt");
        }
    }
}