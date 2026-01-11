using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    public class BookingViewController : Controller
    {
        [HttpGet]
        [Route("booking/create/{packageId:int}")]
        public IActionResult Create(int packageId)
        {
            ViewData["PackageId"] = packageId;
            return View("~/Views/Booking/Create.cshtml");
        }

        [HttpGet]
        [Route("booking/buynow/{packageId:int}")]
        public IActionResult BuyNow(int packageId)
        {
            ViewData["PackageId"] = packageId;
            return View("~/Views/Booking/BuyNow.cshtml");
        }

        [HttpGet]
        [Route("booking/payment/{bookingId:int}")]
        public IActionResult Payment(int bookingId)
        {
            ViewData["BookingId"] = bookingId;
            return View("~/Views/Booking/Payment.cshtml");
        }

        [HttpGet]
        [Route("booking/confirmation/{bookingId:int}")]
        public IActionResult Confirmation(int bookingId)
        {
            ViewData["BookingId"] = bookingId;
            return View("~/Views/Booking/Confirmation.cshtml");
        }

        [HttpGet]
        [Route("booking/details/{bookingId:int}")]
        public IActionResult Details(int bookingId)
        {
            ViewData["BookingId"] = bookingId;
            return View("~/Views/Booking/Details.cshtml");
        }

        /// <summary>
        /// My Trips page - /my-trips
        /// Shows user's booked destinations with countdown timers
        /// </summary>
        [HttpGet]
        [Route("my-trips")]
        public IActionResult MyTrips()
        {
            return View("~/Views/Booking/MyTrips.cshtml");
        }
    }
}