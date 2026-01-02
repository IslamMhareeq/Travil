using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Admin Panel Views
    /// </summary>
    public class AdminViewController : Controller
    {
        /// <summary>
        /// Admin Dashboard - route: /admin
        /// </summary>
        [HttpGet]
        [Route("admin")]
        [Route("admin/index")]
        [Route("admin/dashboard")]
        public IActionResult Index()
        {
            return View("~/Views/Admin/Index.cshtml");
        }

        /// <summary>
        /// Packages Management - route: /admin/packages
        /// </summary>
        [HttpGet]
        [Route("admin/packages")]
        public IActionResult Packages()
        {
            return View("~/Views/Admin/Packages.cshtml");
        }

        /// <summary>
        /// Create New Package - route: /admin/packages/create
        /// </summary>
        [HttpGet]
        [Route("admin/packages/create")]
        [Route("admin/package/create")]
        public IActionResult CreatePackage()
        {
            return View("~/Views/Admin/EditPackage.cshtml");
        }

        /// <summary>
        /// Edit Package - route: /admin/packages/edit/{id}
        /// </summary>
        [HttpGet]
        [Route("admin/packages/edit/{id:int}")]
        [Route("admin/package/edit/{id:int}")]
        public IActionResult EditPackage(int id)
        {
            ViewData["PackageId"] = id;
            return View("~/Views/Admin/EditPackage.cshtml");
        }

        /// <summary>
        /// Bookings Management - route: /admin/bookings
        /// </summary>
        [HttpGet]
        [Route("admin/bookings")]
        public IActionResult Bookings()
        {
            return View("~/Views/Admin/Bookings.cshtml");
        }

        /// <summary>
        /// Users Management - route: /admin/users
        /// </summary>
        [HttpGet]
        [Route("admin/users")]
        public IActionResult Users()
        {
            return View("~/Views/Admin/Users.cshtml");
        }

        /// <summary>
        /// Waiting List Management - route: /admin/waiting-list
        /// </summary>
        [HttpGet]
        [Route("admin/waiting-list")]
        [Route("admin/waitlist")]
        public IActionResult WaitingList()
        {
            return View("~/Views/Admin/WaitingList.cshtml");
        }

        /// <summary>
        /// Admin Profile - route: /admin/profile
        /// </summary>
        [HttpGet]
        [Route("admin/profile")]
        public IActionResult Profile()
        {
            return View("~/Views/Admin/Profile.cshtml");
        }
    }
}