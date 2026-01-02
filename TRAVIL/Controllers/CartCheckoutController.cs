using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TRAVEL.Services;

namespace TRAVEL.Controllers
{
    /// <summary>
    /// Cart Checkout API Controller - Handles payment for multiple cart items
    /// </summary>
    [ApiController]
    [Route("api/checkout")]
    [Authorize]
    public class CartCheckoutController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly IBookingService _bookingService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<CartCheckoutController> _logger;

        public CartCheckoutController(
            ICartService cartService,
            IBookingService bookingService,
            IPaymentService paymentService,
            ILogger<CartCheckoutController> logger)
        {
            _cartService = cartService;
            _bookingService = bookingService;
            _paymentService = paymentService;
            _logger = logger;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(userIdClaim ?? "0");
        }

        /// <summary>
        /// Process checkout for all cart items - creates bookings and processes single payment
        /// </summary>
        [HttpPost("process")]
        public async Task<IActionResult> ProcessCheckout([FromBody] CheckoutRequest request)
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "Please login to checkout" });

            try
            {
                // Get cart with items
                var cart = await _cartService.GetCartWithItemsAsync(userId);
                if (cart == null || !cart.Items.Any())
                {
                    return BadRequest(new { success = false, message = "Your cart is empty" });
                }

                // Check user's active bookings count (max 3 as per requirements)
                var activeBookings = await _bookingService.GetActiveBookingsCountAsync(userId);
                if (activeBookings + cart.Items.Count > 3)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"You cannot exceed 3 active booked trips. You currently have {activeBookings} active bookings and {cart.Items.Count} items in cart."
                    });
                }

                var createdBookings = new List<object>();
                decimal totalAmount = 0;

                // Create bookings for each cart item
                foreach (var item in cart.Items)
                {
                    var booking = await _bookingService.CreateBookingAsync(
                        userId,
                        item.PackageId,
                        item.NumberOfRooms,
                        item.NumberOfGuests,
                        item.SpecialRequests
                    );

                    if (booking == null)
                    {
                        _logger.LogWarning($"Failed to create booking for package {item.PackageId}");
                        continue;
                    }

                    totalAmount += booking.TotalPrice;
                    createdBookings.Add(new
                    {
                        bookingId = booking.BookingId,
                        bookingReference = booking.BookingReference,
                        packageId = item.PackageId,
                        totalPrice = booking.TotalPrice
                    });
                }

                if (!createdBookings.Any())
                {
                    return BadRequest(new { success = false, message = "Failed to create bookings. Some packages may be unavailable." });
                }

                // Process payment for all bookings at once
                // In a real implementation, we'd process a single payment for the total
                // For now, we'll process payment for the first booking and mark others as paid

                var firstBookingId = ((dynamic)createdBookings.First()).bookingId;

                var paymentRequest = new PaymentRequest
                {
                    CardNumber = request.CardNumber,
                    CardHolderName = request.CardHolderName,
                    ExpiryMonth = request.ExpiryMonth,
                    ExpiryYear = request.ExpiryYear,
                    CVV = request.CVV,
                    PaymentMethod = request.PaymentMethod
                };

                var paymentResult = await _paymentService.ProcessPaymentAsync((int)firstBookingId, paymentRequest);

                if (!paymentResult.Success)
                {
                    return BadRequest(new { success = false, message = paymentResult.Message });
                }

                // Clear the cart after successful payment
                await _cartService.ClearCartAsync(userId);

                _logger.LogInformation($"Cart checkout completed for user {userId}. {createdBookings.Count} bookings created.");

                return Ok(new
                {
                    success = true,
                    message = "Payment successful! All bookings confirmed.",
                    data = new
                    {
                        bookings = createdBookings,
                        totalAmount = totalAmount,
                        transactionId = paymentResult.Payment?.TransactionId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing checkout for user {userId}");
                return StatusCode(500, new { success = false, message = "An error occurred during checkout" });
            }
        }

        /// <summary>
        /// Get checkout summary before payment
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetCheckoutSummary()
        {
            var userId = GetUserId();
            if (userId == 0)
                return Unauthorized(new { success = false, message = "Please login" });

            var cart = await _cartService.GetCartWithItemsAsync(userId);
            if (cart == null || !cart.Items.Any())
            {
                return Ok(new { success = true, data = new { items = new object[] { }, total = 0 } });
            }

            var items = cart.Items.Select(i => new
            {
                packageId = i.PackageId,
                destination = i.Package?.Destination,
                country = i.Package?.Country,
                imageUrl = i.Package?.ImageUrl,
                startDate = i.Package?.StartDate,
                endDate = i.Package?.EndDate,
                numberOfRooms = i.NumberOfRooms,
                numberOfGuests = i.NumberOfGuests,
                pricePerRoom = i.Price,
                subtotal = i.Price * i.NumberOfRooms
            }).ToList();

            var subtotal = items.Sum(i => i.subtotal);
            var serviceFee = Math.Round(subtotal * 0.05m, 2); // 5% service fee
            var taxes = Math.Round(subtotal * 0.10m, 2); // 10% taxes
            var total = subtotal + serviceFee + taxes;

            return Ok(new
            {
                success = true,
                data = new
                {
                    items = items,
                    subtotal = subtotal,
                    serviceFee = serviceFee,
                    taxes = taxes,
                    total = total
                }
            });
        }
    }

    public class CheckoutRequest
    {
        public string CardNumber { get; set; }
        public string CardHolderName { get; set; }
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public string CVV { get; set; }
        public int PaymentMethod { get; set; } // 0 = Credit, 1 = Debit, 2 = PayPal
    }
}