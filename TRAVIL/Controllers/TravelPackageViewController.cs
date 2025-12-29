using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Travel Package Views
    /// </summary>
    [Route("packages")]
    public class TravelPackageViewController : Controller
    {
        /// <summary>
        /// Package Gallery/Search
        /// </summary>
        [HttpGet]
        public IActionResult Index()
        {
            return View("~/Views/TravelPackage/Index.cshtml");
        }

        /// <summary>
        /// Package Details
        /// </summary>
        [HttpGet("{id}")]
        public IActionResult Details(int id)
        {
            ViewData["PackageId"] = id;
            return View("~/Views/TravelPackage/Details.cshtml");
        }

        /// <summary>
        /// Search Packages
        /// </summary>
        [HttpGet("search")]
        public IActionResult Search()
        {
            return View("~/Views/TravelPackage/Search.cshtml");
        }
    }
}
