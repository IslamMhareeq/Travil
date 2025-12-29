using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TRAVEL.Data;
using TRAVEL.Models;

namespace TRAVEL.Services
{
    public interface IPaymentService
    {
        Task<PaymentResult> ProcessPaymentAsync(int bookingId, PaymentRequest request);
        Task<PaymentResult> RefundPaymentAsync(int paymentId, string reason);
        Task<Payment> GetPaymentByBookingAsync(int bookingId);
        Task<Payment> GetPaymentByIdAsync(int paymentId);
    }

    public class PaymentService : IPaymentService
    {
        private readonly TravelDbContext _context;
        private readonly IBookingService _bookingService;
        private readonly IEmailService _emailService;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            TravelDbContext context,
            IBookingService bookingService,
            IEmailService emailService,
            ILogger<PaymentService> logger)
        {
            _context = context;
            _bookingService = bookingService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<PaymentResult> ProcessPaymentAsync(int bookingId, PaymentRequest request)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.TravelPackage)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                return new PaymentResult { Success = false, Message = "Booking not found" };

            if (booking.Status == BookingStatus.Cancelled)
                return new PaymentResult { Success = false, Message = "Cannot process payment for cancelled booking" };

            if (booking.Payment != null && booking.Payment.Status == PaymentStatus.Completed)
                return new PaymentResult { Success = false, Message = "Payment already completed" };

            // Validate card (basic validation - do NOT store card details)
            if (!ValidateCardNumber(request.CardNumber))
                return new PaymentResult { Success = false, Message = "Invalid card number" };

            if (!ValidateExpiryDate(request.ExpiryMonth, request.ExpiryYear))
                return new PaymentResult { Success = false, Message = "Card has expired" };

            if (!ValidateCVV(request.CVV))
                return new PaymentResult { Success = false, Message = "Invalid CVV" };

            // Simulate payment processing (in production, integrate with payment gateway)
            var transactionId = GenerateTransactionId();
            var paymentSuccess = SimulatePaymentGateway(request);

            if (!paymentSuccess)
            {
                // Create failed payment record
                var failedPayment = new Payment
                {
                    BookingId = bookingId,
                    Amount = booking.TotalPrice,
                    Status = PaymentStatus.Failed,
                    PaymentMethod = request.PaymentMethod,
                    TransactionId = transactionId,
                    PaymentDate = DateTime.UtcNow,
                    FailureReason = "Payment declined by bank"
                };

                if (booking.Payment != null)
                {
                    _context.Payments.Remove(booking.Payment);
                }

                _context.Payments.Add(failedPayment);
                await _context.SaveChangesAsync();

                _logger.LogWarning($"Payment failed for booking {booking.BookingReference}");
                return new PaymentResult { Success = false, Message = "Payment was declined. Please try again.", Payment = failedPayment };
            }

            // Create successful payment
            var payment = new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalPrice,
                Status = PaymentStatus.Completed,
                PaymentMethod = request.PaymentMethod,
                TransactionId = transactionId,
                PaymentDate = DateTime.UtcNow,
                CompletedDate = DateTime.UtcNow
            };

            if (booking.Payment != null)
            {
                _context.Payments.Remove(booking.Payment);
            }

            _context.Payments.Add(payment);

            // Confirm the booking
            booking.Status = BookingStatus.Confirmed;
            booking.ConfirmedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send payment receipt
            await _emailService.SendPaymentReceiptAsync(
                booking.User.Email,
                booking.BookingReference,
                payment.Amount,
                payment.PaymentDate);

            // Send booking confirmation
            await _emailService.SendBookingConfirmationAsync(
                booking.User.Email,
                booking.BookingReference,
                booking.TravelPackage.Destination,
                booking.TravelPackage.StartDate);

            _logger.LogInformation($"Payment completed for booking {booking.BookingReference}, transaction {transactionId}");

            return new PaymentResult
            {
                Success = true,
                Message = "Payment successful! Your booking is confirmed.",
                Payment = payment
            };
        }

        public async Task<PaymentResult> RefundPaymentAsync(int paymentId, string reason)
        {
            var payment = await _context.Payments
                .Include(p => p.Booking)
                    .ThenInclude(b => b.User)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.TravelPackage)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null)
                return new PaymentResult { Success = false, Message = "Payment not found" };

            if (payment.Status != PaymentStatus.Completed)
                return new PaymentResult { Success = false, Message = "Only completed payments can be refunded" };

            // Process refund (simulate)
            payment.Status = PaymentStatus.Refunded;
            payment.FailureReason = $"Refunded: {reason}";

            // Cancel the booking
            payment.Booking.Status = BookingStatus.Cancelled;
            payment.Booking.CancelledDate = DateTime.UtcNow;
            payment.Booking.CancellationReason = reason;

            // Return rooms
            payment.Booking.TravelPackage.AvailableRooms += payment.Booking.NumberOfRooms;

            await _context.SaveChangesAsync();

            // Notify user
            await _emailService.SendCancellationConfirmationAsync(
                payment.Booking.User.Email,
                payment.Booking.BookingReference,
                payment.Booking.TravelPackage.Destination);

            _logger.LogInformation($"Payment {paymentId} refunded for booking {payment.Booking.BookingReference}");

            return new PaymentResult
            {
                Success = true,
                Message = "Refund processed successfully",
                Payment = payment
            };
        }

        public async Task<Payment> GetPaymentByBookingAsync(int bookingId)
        {
            return await _context.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.BookingId == bookingId);
        }

        public async Task<Payment> GetPaymentByIdAsync(int paymentId)
        {
            return await _context.Payments
                .Include(p => p.Booking)
                    .ThenInclude(b => b.User)
                .Include(p => p.Booking)
                    .ThenInclude(b => b.TravelPackage)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        }

        // Private validation methods
        private bool ValidateCardNumber(string cardNumber)
        {
            if (string.IsNullOrEmpty(cardNumber))
                return false;

            // Remove spaces and dashes
            cardNumber = cardNumber.Replace(" ", "").Replace("-", "");

            // Check if all digits
            if (!long.TryParse(cardNumber, out _))
                return false;

            // Check length (13-19 digits for most cards)
            if (cardNumber.Length < 13 || cardNumber.Length > 19)
                return false;

            // Luhn algorithm
            int sum = 0;
            bool alternate = false;
            for (int i = cardNumber.Length - 1; i >= 0; i--)
            {
                int n = int.Parse(cardNumber[i].ToString());
                if (alternate)
                {
                    n *= 2;
                    if (n > 9)
                        n -= 9;
                }
                sum += n;
                alternate = !alternate;
            }
            return sum % 10 == 0;
        }

        private bool ValidateExpiryDate(int month, int year)
        {
            if (month < 1 || month > 12)
                return false;

            var expiry = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
            return expiry >= DateTime.UtcNow;
        }

        private bool ValidateCVV(string cvv)
        {
            if (string.IsNullOrEmpty(cvv))
                return false;

            return cvv.Length >= 3 && cvv.Length <= 4 && int.TryParse(cvv, out _);
        }

        private string GenerateTransactionId()
        {
            return $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
        }

        private bool SimulatePaymentGateway(PaymentRequest request)
        {
            // In production, this would integrate with Stripe, PayPal, etc.
            // Simulate 95% success rate
            return new Random().Next(100) < 95;
        }
    }

    public class PaymentRequest
    {
        public string CardNumber { get; set; } // NOT stored
        public string CardHolderName { get; set; } // NOT stored
        public int ExpiryMonth { get; set; } // NOT stored
        public int ExpiryYear { get; set; } // NOT stored
        public string CVV { get; set; } // NOT stored
        public PaymentMethod PaymentMethod { get; set; }
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Payment Payment { get; set; }
    }
}
