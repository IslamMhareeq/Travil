using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TRAVEL.Services;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// API Controller for Cart operations
    /// </summary>
    [ApiController]
    [Route("api/cart")]
    [Produces("application/json")]
    public class CartCheckoutController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly ILogger<CartCheckoutController> _logger;

        public CartCheckoutController(
            ICartService cartService,
            ILogger<CartCheckoutController> logger)
        {
            _cartService = cartService;
            _logger = logger;
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value
                ?? User.FindFirst("userid")?.Value
                ?? User.FindFirst("sub")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return null;
            }
            return userId;
        }

        /// <summary>
        /// Get current user's cart
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetCart()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var cart = await _cartService.GetCartWithItemsAsync(userId.Value);

                if (cart == null)
                {
                    return Ok(new { success = true, data = new { items = new object[0], totalPrice = 0, totalItems = 0 } });
                }

                var result = new
                {
                    cartId = cart.CartId,
                    items = cart.Items.Select(i => new
                    {
                        cartItemId = i.CartItemId,
                        packageId = i.PackageId,
                        quantity = i.Quantity,
                        numberOfGuests = i.NumberOfGuests,
                        unitPrice = i.UnitPrice,
                        subtotal = i.Subtotal,
                        dateAdded = i.DateAdded,
                        specialRequests = i.SpecialRequests,
                        travelPackage = i.TravelPackage != null ? new
                        {
                            packageId = i.TravelPackage.PackageId,
                            destination = i.TravelPackage.Destination,
                            country = i.TravelPackage.Country,
                            startDate = i.TravelPackage.StartDate,
                            endDate = i.TravelPackage.EndDate,
                            price = i.TravelPackage.Price,
                            discountedPrice = i.TravelPackage.DiscountedPrice,
                            imageUrl = i.TravelPackage.ImageUrl,
                            availableRooms = i.TravelPackage.AvailableRooms
                        } : null
                    }).ToList(),
                    totalPrice = cart.TotalPrice,
                    totalItems = cart.TotalItems,
                    createdAt = cart.CreatedAt,
                    updatedAt = cart.UpdatedAt
                };

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Add package to cart
        /// </summary>
        [HttpPost("add")]
        [Authorize]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                if (request.PackageId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid package ID" });
                }

                var result = await _cartService.AddToCartAsync(
                    userId.Value,
                    request.PackageId,
                    request.Quantity > 0 ? request.Quantity : 1,
                    request.Guests > 0 ? request.Guests : 1,
                    request.SpecialRequests);

                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new { success = true, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding to cart");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Update cart item
        /// </summary>
        [HttpPut("item/{cartItemId}")]
        [Authorize]
        public async Task<IActionResult> UpdateCartItem(int cartItemId, [FromBody] UpdateCartItemRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var result = await _cartService.UpdateCartItemAsync(
                    userId.Value,
                    cartItemId,
                    request.Quantity,
                    request.Guests > 0 ? request.Guests : 1);

                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new { success = true, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating cart item {cartItemId}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Remove item from cart
        /// </summary>
        [HttpDelete("item/{cartItemId}")]
        [Authorize]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var result = await _cartService.RemoveFromCartAsync(userId.Value, cartItemId);

                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new { success = true, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing cart item {cartItemId}");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Clear entire cart
        /// </summary>
        [HttpDelete("clear")]
        [Authorize]
        public async Task<IActionResult> ClearCart()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var result = await _cartService.ClearCartAsync(userId.Value);

                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new { success = true, message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get cart item count
        /// </summary>
        [HttpGet("count")]
        [Authorize]
        public async Task<IActionResult> GetCartCount()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var count = await _cartService.GetCartItemCountAsync(userId.Value);

                return Ok(new { success = true, count = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart count");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Check if package is in cart
        /// </summary>
        [HttpGet("check/{packageId}")]
        [Authorize]
        public async Task<IActionResult> CheckPackageInCart(int packageId)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var inCart = await _cartService.IsPackageInCartAsync(userId.Value, packageId);

                return Ok(new { success = true, inCart = inCart });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if package {packageId} is in cart");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Checkout cart - creates bookings from cart items
        /// </summary>
        [HttpPost("checkout")]
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var result = await _cartService.CheckoutCartAsync(userId.Value);

                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                var bookingsResult = result.Bookings?.Select(b => new
                {
                    bookingId = b.BookingId,
                    bookingReference = b.BookingReference,
                    packageId = b.PackageId,
                    totalPrice = b.TotalPrice,
                    status = b.Status.ToString()
                }).ToList();

                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    bookings = bookingsResult
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during checkout");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }
    }

    // Request DTOs
    public class AddToCartRequest
    {
        public int PackageId { get; set; }
        public int Quantity { get; set; } = 1;
        public int Guests { get; set; } = 1;
        public string? SpecialRequests { get; set; }
    }

    public class UpdateCartItemRequest
    {
        public int Quantity { get; set; }
        public int Guests { get; set; } = 1;
    }
}