using Microsoft.AspNetCore.Mvc;

namespace TRAVEL.Controllers
{
    public class CartViewController : Controller
    {
        /// <summary>
        /// Shopping Cart page - /cart
        /// </summary>
        [HttpGet]
        [Route("cart")]
        public IActionResult Index()
        {
            return View("~/Views/Cart/Index.cshtml");
        }

        /// <summary>
        /// Checkout page - /checkout
        /// </summary>
        [HttpGet]
        [Route("checkout")]
        public IActionResult Checkout()
        {
            return View("~/Views/Cart/Checkout.cshtml");
        }
    }
}