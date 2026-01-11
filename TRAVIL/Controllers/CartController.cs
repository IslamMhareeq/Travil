using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TRAVEL.Data;
using TRAVEL.Models;

namespace TRAVEL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly TravelDbContext _context;

        public CartController(TravelDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        /// <summary>
        /// Get or create user's cart
        /// </summary>
        private async Task<CartModels> GetOrCreateCart(int userId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.TravelPackage)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new CartModels
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return cart;
        }

        /// <summary>
        /// Get user's cart items
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var cart = await GetOrCreateCart(userId);

                var items = cart.Items.Select(item => new
                {
                    cartItemId = item.CartItemId,
                    cartId = item.CartId,
                    packageId = item.PackageId,
                    quantity = item.Quantity,
                    numberOfRooms = item.Quantity, // Map quantity to numberOfRooms for frontend
                    numberOfGuests = item.NumberOfGuests,
                    unitPrice = item.UnitPrice,
                    price = item.UnitPrice,
                    subtotal = item.Subtotal,
                    specialRequests = item.SpecialRequests,
                    dateAdded = item.DateAdded,
                    package = item.TravelPackage != null ? new
                    {
                        packageId = item.TravelPackage.PackageId,
                        destination = item.TravelPackage.Destination,
                        country = item.TravelPackage.Country,
                        imageUrl = item.TravelPackage.ImageUrl,
                        price = item.TravelPackage.Price,
                        discountedPrice = item.TravelPackage.DiscountedPrice,
                        startDate = item.TravelPackage.StartDate,
                        endDate = item.TravelPackage.EndDate,
                        availableRooms = item.TravelPackage.AvailableRooms,
                        description = item.TravelPackage.Description
                    } : null
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        cartId = cart.CartId,
                        items = items,
                        totalItems = cart.ItemCount,
                        totalPrice = cart.Total
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error fetching cart", error = ex.Message });
            }
        }

        /// <summary>
        /// Add item to cart
        /// </summary>
        [HttpPost("add")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized(new { message = "User not authenticated" });

            try
            {
                // Check if package exists
                var package = await _context.TravelPackages.FindAsync(dto.PackageId);
                if (package == null)
                    return NotFound(new { success = false, message = "Package not found" });

                // Get or create cart
                var cart = await GetOrCreateCart(userId);

                // Check if item already in cart
                var existingItem = cart.Items.FirstOrDefault(i => i.PackageId == dto.PackageId);

                if (existingItem != null)
                {
                    // Update existing item
                    existingItem.Quantity = dto.NumberOfRooms > 0 ? dto.NumberOfRooms : existingItem.Quantity;
                    existingItem.NumberOfGuests = dto.NumberOfGuests > 0 ? dto.NumberOfGuests : existingItem.NumberOfGuests;
                    existingItem.UnitPrice = package.DiscountedPrice ?? package.Price;
                    existingItem.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    return Ok(new { success = true, message = "Cart updated", cartItemId = existingItem.CartItemId });
                }

                // Add new item
                var cartItem = new CartItem
                {
                    CartId = cart.CartId,
                    PackageId = dto.PackageId,
                    Quantity = dto.NumberOfRooms > 0 ? dto.NumberOfRooms : 1,
                    NumberOfGuests = dto.NumberOfGuests > 0 ? dto.NumberOfGuests : 1,
                    UnitPrice = package.DiscountedPrice ?? package.Price,
                    DateAdded = DateTime.UtcNow
                };

                _context.CartItems.Add(cartItem);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Added to cart", cartItemId = cartItem.CartItemId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error adding to cart", error = ex.Message });
            }
        }

        /// <summary>
        /// Update cart item
        /// </summary>
        [HttpPut("update/{cartItemId}")]
        public async Task<IActionResult> UpdateCartItem(int cartItemId, [FromBody] UpdateCartDto dto)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var cart = await GetOrCreateCart(userId);
                var cartItem = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId);

                if (cartItem == null)
                    return NotFound(new { success = false, message = "Cart item not found" });

                if (dto.NumberOfRooms > 0)
                    cartItem.Quantity = dto.NumberOfRooms;
                if (dto.NumberOfGuests > 0)
                    cartItem.NumberOfGuests = dto.NumberOfGuests;
                cartItem.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Cart updated" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error updating cart", error = ex.Message });
            }
        }

        /// <summary>
        /// Remove item from cart
        /// </summary>
        [HttpDelete("remove/{cartItemId}")]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var cart = await GetOrCreateCart(userId);
                var cartItem = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId);

                if (cartItem == null)
                    return NotFound(new { success = false, message = "Cart item not found" });

                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Item removed from cart" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error removing from cart", error = ex.Message });
            }
        }

        /// <summary>
        /// Clear entire cart
        /// </summary>
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearCart()
        {
            var userId = GetUserId();
            if (userId == 0) return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var cart = await GetOrCreateCart(userId);

                if (cart.Items.Any())
                {
                    _context.CartItems.RemoveRange(cart.Items);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true, message = "Cart cleared" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Error clearing cart", error = ex.Message });
            }
        }

        /// <summary>
        /// Get cart count for navbar badge
        /// </summary>
        [HttpGet("count")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCartCount()
        {
            var userId = GetUserId();
            if (userId == 0) return Ok(new { count = 0 });

            try
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                return Ok(new { count = cart?.ItemCount ?? 0 });
            }
            catch
            {
                return Ok(new { count = 0 });
            }
        }
    }

    public class AddToCartDto
    {
        public int PackageId { get; set; }
        public int NumberOfRooms { get; set; } = 1;
        public int NumberOfGuests { get; set; } = 1;
        public string? SpecialRequests { get; set; }
    }

    public class UpdateCartDto
    {
        public int NumberOfRooms { get; set; }
        public int NumberOfGuests { get; set; }
        public string? SpecialRequests { get; set; }
    }
}