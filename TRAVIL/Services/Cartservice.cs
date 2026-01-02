using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRAVEL.Data;
using TRAVEL.Models;

namespace TRAVEL.Services
{
    public interface ICartService
    {
        Task<Cart> GetOrCreateCartAsync(int userId);
        Task<Cart> GetCartWithItemsAsync(int userId);
        Task<CartItem> AddToCartAsync(int userId, int packageId, int numberOfRooms, int numberOfGuests, string specialRequests = null);
        Task<bool> RemoveFromCartAsync(int userId, int cartItemId);
        Task<bool> UpdateCartItemAsync(int userId, int cartItemId, int numberOfRooms, int numberOfGuests);
        Task<bool> ClearCartAsync(int userId);
        Task<int> GetCartItemCountAsync(int userId);
        Task<decimal> GetCartTotalAsync(int userId);
        Task<bool> IsPackageInCartAsync(int userId, int packageId);
    }

    public class CartService : ICartService
    {
        private readonly TravelDbContext _context;
        private readonly ILogger<CartService> _logger;

        public CartService(TravelDbContext context, ILogger<CartService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Cart> GetOrCreateCartAsync(int userId)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Package)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Created new cart for user {userId}");
            }

            return cart;
        }

        public async Task<Cart> GetCartWithItemsAsync(int userId)
        {
            return await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Package)
                .FirstOrDefaultAsync(c => c.UserId == userId);
        }

        public async Task<CartItem> AddToCartAsync(int userId, int packageId, int numberOfRooms, int numberOfGuests, string specialRequests = null)
        {
            var cart = await GetOrCreateCartAsync(userId);

            // Check if package exists and is available
            var package = await _context.TravelPackages.FindAsync(packageId);
            if (package == null)
            {
                _logger.LogWarning($"Package {packageId} not found");
                return null;
            }

            if (!package.IsActive)
            {
                _logger.LogWarning($"Package {packageId} is not active");
                return null;
            }

            if (package.AvailableRooms < numberOfRooms)
            {
                _logger.LogWarning($"Not enough rooms available for package {packageId}");
                return null;
            }

            // Check if already in cart
            var existingItem = cart.Items.FirstOrDefault(i => i.PackageId == packageId);
            if (existingItem != null)
            {
                // Update existing item
                existingItem.NumberOfRooms = numberOfRooms;
                existingItem.NumberOfGuests = numberOfGuests;
                existingItem.SpecialRequests = specialRequests;
                existingItem.Price = GetCurrentPrice(package);
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Updated cart item for package {packageId}");
                return existingItem;
            }

            // Add new item
            var cartItem = new CartItem
            {
                CartId = cart.CartId,
                PackageId = packageId,
                NumberOfRooms = numberOfRooms,
                NumberOfGuests = numberOfGuests,
                SpecialRequests = specialRequests,
                Price = GetCurrentPrice(package),
                AddedAt = DateTime.UtcNow
            };

            _context.CartItems.Add(cartItem);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Added package {packageId} to cart for user {userId}");
            return cartItem;
        }

        public async Task<bool> RemoveFromCartAsync(int userId, int cartItemId)
        {
            var cart = await GetCartWithItemsAsync(userId);
            if (cart == null) return false;

            var item = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId);
            if (item == null) return false;

            _context.CartItems.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Removed item {cartItemId} from cart for user {userId}");
            return true;
        }

        public async Task<bool> UpdateCartItemAsync(int userId, int cartItemId, int numberOfRooms, int numberOfGuests)
        {
            var cart = await GetCartWithItemsAsync(userId);
            if (cart == null) return false;

            var item = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId);
            if (item == null) return false;

            // Verify availability
            var package = await _context.TravelPackages.FindAsync(item.PackageId);
            if (package == null || package.AvailableRooms < numberOfRooms)
                return false;

            item.NumberOfRooms = numberOfRooms;
            item.NumberOfGuests = numberOfGuests;
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ClearCartAsync(int userId)
        {
            var cart = await GetCartWithItemsAsync(userId);
            if (cart == null) return false;

            _context.CartItems.RemoveRange(cart.Items);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Cleared cart for user {userId}");
            return true;
        }

        public async Task<int> GetCartItemCountAsync(int userId)
        {
            var cart = await GetCartWithItemsAsync(userId);
            return cart?.Items?.Count ?? 0;
        }

        public async Task<decimal> GetCartTotalAsync(int userId)
        {
            var cart = await GetCartWithItemsAsync(userId);
            if (cart?.Items == null) return 0;

            return cart.Items.Sum(i => i.Price * i.NumberOfRooms);
        }

        public async Task<bool> IsPackageInCartAsync(int userId, int packageId)
        {
            var cart = await GetCartWithItemsAsync(userId);
            return cart?.Items?.Any(i => i.PackageId == packageId) ?? false;
        }

        private decimal GetCurrentPrice(TravelPackage package)
        {
            // Check if discount is active
            if (package.DiscountedPrice.HasValue &&
                package.DiscountStartDate.HasValue &&
                package.DiscountEndDate.HasValue &&
                DateTime.UtcNow >= package.DiscountStartDate.Value &&
                DateTime.UtcNow <= package.DiscountEndDate.Value)
            {
                return package.DiscountedPrice.Value;
            }
            return package.Price;
        }
    }
}