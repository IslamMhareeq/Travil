using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Booking Views
    /// </summary>
    [Route("booking")]
    public class BookingViewController : Controller
    {
        /// <summary>
        /// Create Booking
        /// </summary>
        [HttpGet("create/{packageId}")]
        public IActionResult Create(int packageId)
        {
            ViewData["PackageId"] = packageId;
            return View("~/Views/Booking/Create.cshtml");
        }

        /// <summary>
        /// Booking Details
        /// </summary>
        [HttpGet("{id}")]
        public IActionResult Details(int id)
        {
            ViewData["BookingId"] = id;
            return View("~/Views/Booking/Details.cshtml");
        }

        /// <summary>
        /// My Bookings
        /// </summary>
        [HttpGet("my-bookings")]
        public IActionResult MyBookings()
        {
            return View("~/Views/Booking/MyBookings.cshtml");
        }

        /// <summary>
        /// Payment Page
        /// </summary>
        [HttpGet("payment/{bookingId}")]
        public IActionResult Payment(int bookingId)
        {
            ViewData["BookingId"] = bookingId;
            return View("~/Views/Booking/Payment.cshtml");
        }

        /// <summary>
        /// Payment Success
        /// </summary>
        [HttpGet("success")]
        public IActionResult Success()
        {
            return View("~/Views/Booking/Success.cshtml");
        }
    }
}
