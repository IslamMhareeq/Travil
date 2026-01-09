using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Home/Landing page
    /// </summary>
    public class HomeController : Controller
    {
        /// <summary>
        /// Homepage - route: /
        /// </summary>
        [HttpGet]
        [Route("/")]
        [Route("/home")]
        public IActionResult Index()
        {
            return View("~/Views/Home/Index.cshtml");
        }

        /// <summary>
        /// About page - route: /about
        /// </summary>
        [HttpGet]
        [Route("/about")]
        public IActionResult About()
        {
            return View("~/Views/Home/About.cshtml");
        }

        /// <summary>
        /// Contact page - route: /contact
        /// </summary>
        [HttpGet]
        [Route("/contact")]
        public IActionResult Contact()
        {
            return View("~/Views/Home/Contact.cshtml");
        }
    }
}