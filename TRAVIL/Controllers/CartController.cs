using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TRAVEL.Services;

namespace TRAVEL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly ILogger<CartController> _logger;

        public CartController(ICartService cartService, ILogger<CartController> logger)
        {
            _cartService = cartService;
            _logger = logger;
        }

        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return null;
            return userId;
        }

        /// <summary>
        /// Get user's cart
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetCart()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var cart = await _cartService.GetCartAsync(userId.Value);

                var items = cart.Items.Select(item => new
                {
                    cartItemId = item.CartItemId,
                    cartId = item.CartId,
                    packageId = item.PackageId,
                    destination = item.TravelPackage?.Destination ?? "Unknown",
                    country = item.TravelPackage?.Country ?? "Unknown",
                    imageUrl = item.TravelPackage?.ImageUrl,
                    unitPrice = item.UnitPrice,
                    quantity = item.Quantity,
                    numberOfGuests = item.NumberOfGuests,
                    subtotal = item.Subtotal,
                    specialRequests = item.SpecialRequests,
                    dateAdded = item.DateAdded,
                    startDate = item.TravelPackage?.StartDate,
                    endDate = item.TravelPackage?.EndDate,
                    availableRooms = item.TravelPackage?.AvailableRooms ?? 0
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = items,
                    total = cart.Total,
                    itemCount = cart.ItemCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart");
                return StatusCode(500, new { success = false, message = "Error retrieving cart" });
            }
        }

        /// <summary>
        /// Add item to cart
        /// </summary>
        [HttpPost("add")]
        [Authorize]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                _logger.LogInformation($"AddToCart: User {userId} adding package {request?.PackageId}");

                if (request == null || request.PackageId <= 0)
                    return BadRequest(new { success = false, message = "Invalid package ID" });

                var quantity = request.Quantity > 0 ? request.Quantity : 1;
                var guests = request.NumberOfGuests > 0 ? request.NumberOfGuests : 1;

                var cartItem = await _cartService.AddToCartAsync(
                    userId.Value,
                    request.PackageId,
                    quantity,
                    guests,
                    request.SpecialRequests);

                if (cartItem == null)
                    return BadRequest(new { success = false, message = "Failed to add item to cart. Package may not be available." });

                return Ok(new { success = true, message = "Added to cart", cartItemId = cartItem.CartItemId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding to cart");
                return StatusCode(500, new { success = false, message = "Error adding to cart" });
            }
        }

        /// <summary>
        /// Update cart item
        /// </summary>
        [HttpPut("{cartItemId}")]
        [Authorize]
        public async Task<IActionResult> UpdateCartItem(int cartItemId, [FromBody] UpdateCartItemRequest request)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var success = await _cartService.UpdateCartItemAsync(
                    userId.Value,
                    cartItemId,
                    request.Quantity,
                    request.NumberOfGuests);

                if (!success)
                    return BadRequest(new { success = false, message = "Failed to update cart item" });

                return Ok(new { success = true, message = "Cart updated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart item");
                return StatusCode(500, new { success = false, message = "Error updating cart" });
            }
        }

        /// <summary>
        /// Remove item from cart
        /// </summary>
        [HttpDelete("{cartItemId}")]
        [Authorize]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var success = await _cartService.RemoveFromCartAsync(userId.Value, cartItemId);

                if (!success)
                    return NotFound(new { success = false, message = "Cart item not found" });

                return Ok(new { success = true, message = "Removed from cart" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing from cart");
                return StatusCode(500, new { success = false, message = "Error removing from cart" });
            }
        }

        /// <summary>
        /// Clear cart
        /// </summary>
        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> ClearCart()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                await _cartService.ClearCartAsync(userId.Value);

                return Ok(new { success = true, message = "Cart cleared" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart");
                return StatusCode(500, new { success = false, message = "Error clearing cart" });
            }
        }

        /// <summary>
        /// Get cart count
        /// </summary>
        [HttpGet("count")]
        [Authorize]
        public async Task<IActionResult> GetCartCount()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                var count = await _cartService.GetCartItemCountAsync(userId.Value);

                return Ok(new { success = true, count = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart count");
                return StatusCode(500, new { success = false, message = "Error getting cart count" });
            }
        }
    }

    // Request DTOs
    public class AddToCartRequest
    {
        public int PackageId { get; set; }
        public int Quantity { get; set; } = 1;
        public int NumberOfGuests { get; set; } = 1;
        public string? SpecialRequests { get; set; }
    }

    public class UpdateCartItemRequest
    {
        public int Quantity { get; set; }
        public int? NumberOfGuests { get; set; }
    }
}