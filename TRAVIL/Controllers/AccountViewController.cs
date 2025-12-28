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
        public IActionResult Login()
        {
            return View();
        }

        /// <summary>
        /// Register page
        /// </summary>
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        /// <summary>
        /// User dashboard page
        /// </summary>
        [HttpGet]
        public IActionResult Dashboard()
        {
            // Check if user is authenticated by checking localStorage token
            // For now, just return the view
            return View();
        }
    }
}