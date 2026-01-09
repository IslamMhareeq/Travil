using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Controller for Cart Views
    /// </summary>
    public class CartViewController : Controller
    {
        /// <summary>
        /// Shopping Cart page - route: /cart
        /// </summary>
        [HttpGet]
        [Route("cart")]
        public IActionResult Index()
        {
            return View("~/Views/Cart/Index.cshtml");
        }

        /// <summary>
        /// Checkout page - route: /checkout
        /// </summary>
        [HttpGet]
        [Route("checkout")]
        public IActionResult Checkout()
        {
            return View("~/Views/Cart/Checkout.cshtml");
        }
    }
}