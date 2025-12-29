using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Account Views (Login and Register pages)
    /// </summary>
    public class AccountViewController : Controller
    {
        /// <summary>
        /// Login page
        /// </summary>
        [HttpGet]
        [Route("account/login")]
        public IActionResult Login()
        {
            return View("~/Views/Account/Login.cshtml");
        }

        /// <summary>
        /// Register page
        /// </summary>
        [HttpGet]
        [Route("account/register")]
        public IActionResult Register()
        {
            return View("~/Views/Account/Register.cshtml");
        }

        /// <summary>
        /// User dashboard page
        /// </summary>
        [HttpGet]
        [Route("account/dashboard")]
        public IActionResult Dashboard()
        {
            return View("~/Views/Account/Dashboard.cshtml");
        }

        /// <summary>
        /// User profile page
        /// </summary>
        [HttpGet]
        [Route("account/profile")]
        public IActionResult Profile()
        {
            return View("~/Views/Account/Profile.cshtml");
        }

        /// <summary>
        /// User bookings page
        /// </summary>
        [HttpGet]
        [Route("account/bookings")]
        public IActionResult MyBookings()
        {
            return View("~/Views/Account/MyBookings.cshtml");
        }
    }
}