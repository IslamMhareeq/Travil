using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TRAVEL.Models;
using TRAVEL.Services;

namespace TRAVEL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IBookingService _bookingService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            IBookingService bookingService,
            ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _bookingService = bookingService;
            _logger = logger;
        }

        /// <summary>
        /// Process payment for a booking
        /// IMPORTANT: Card details are NOT stored
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequestDto request)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            // Validate request
            if (request.BookingId <= 0)
                return BadRequest(new { success = false, message = "Invalid booking ID" });

            if (string.IsNullOrEmpty(request.CardNumber))
                return BadRequest(new { success = false, message = "Card number is required" });

            if (string.IsNullOrEmpty(request.CardHolderName))
                return BadRequest(new { success = false, message = "Card holder name is required" });

            if (string.IsNullOrEmpty(request.CVV))
                return BadRequest(new { success = false, message = "CVV is required" });

            // Verify booking belongs to user
            var booking = await _bookingService.GetBookingByIdAsync(request.BookingId);
            if (booking == null)
                return NotFound(new { success = false, message = "Booking not found" });

            if (booking.UserId != userId)
                return Forbid();

            // Process payment
            var paymentRequest = new PaymentRequest
            {
                CardNumber = request.CardNumber,
                CardHolderName = request.CardHolderName,
                ExpiryMonth = request.ExpiryMonth,
                ExpiryYear = request.ExpiryYear,
                CVV = request.CVV,
                PaymentMethod = request.PaymentMethod
            };

            var result = await _paymentService.ProcessPaymentAsync(request.BookingId, paymentRequest);

            if (!result.Success)
            {
                _logger.LogWarning($"Payment failed for booking {request.BookingId}: {result.Message}");
                return BadRequest(new { success = false, message = result.Message });
            }

            _logger.LogInformation($"Payment successful for booking {request.BookingId}");

            return Ok(new
            {
                success = true,
                message = result.Message,
                data = new
                {
                    paymentId = result.Payment.PaymentId,
                    transactionId = result.Payment.TransactionId,
                    amount = result.Payment.Amount,
                    status = result.Payment.Status.ToString(),
                    paymentDate = result.Payment.PaymentDate
                }
            });
        }

        /// <summary>
        /// Get payment status for a booking
        /// </summary>
        [HttpGet("booking/{bookingId}")]
        [Authorize]
        public async Task<IActionResult> GetPaymentByBooking(int bookingId)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { success = false, message = "User not authenticated" });

            var booking = await _bookingService.GetBookingByIdAsync(bookingId);
            if (booking == null)
                return NotFound(new { success = false, message = "Booking not found" });

            var isAdmin = User.IsInRole("Admin");
            if (booking.UserId != userId && !isAdmin)
                return Forbid();

            var payment = await _paymentService.GetPaymentByBookingAsync(bookingId);
            if (payment == null)
                return NotFound(new { success = false, message = "No payment found for this booking" });

            return Ok(new
            {
                success = true,
                data = new
                {
                    paymentId = payment.PaymentId,
                    bookingId = payment.BookingId,
                    amount = payment.Amount,
                    status = payment.Status.ToString(),
                    paymentMethod = payment.PaymentMethod.ToString(),
                    transactionId = payment.TransactionId,
                    paymentDate = payment.PaymentDate,
                    completedDate = payment.CompletedDate
                }
            });
        }

        /// <summary>
        /// Refund a payment (Admin only)
        /// </summary>
        [HttpPost("{paymentId}/refund")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RefundPayment(int paymentId, [FromBody] RefundRequest request)
        {
            var result = await _paymentService.RefundPaymentAsync(paymentId, request.Reason ?? "Admin initiated refund");

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            _logger.LogInformation($"Payment {paymentId} refunded");

            return Ok(new { success = true, message = result.Message });
        }
    }

    public class PaymentRequestDto
    {
        public int BookingId { get; set; }
        public string CardNumber { get; set; } // NOT stored
        public string CardHolderName { get; set; } // NOT stored
        public int ExpiryMonth { get; set; } // NOT stored
        public int ExpiryYear { get; set; } // NOT stored
        public string CVV { get; set; } // NOT stored
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CreditCard;
    }

    public class RefundRequest
    {
        public string Reason { get; set; }
    }
}
