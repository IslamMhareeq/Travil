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
    public interface IBookingService
    {
        Task<BookingResult> CreateBookingAsync(int userId, int packageId, int numberOfRooms, int numberOfGuests);
        Task<BookingResult> ConfirmBookingAsync(int bookingId);
        Task<BookingResult> CancelBookingAsync(int bookingId, string reason);
        Task<List<Booking>> GetUserBookingsAsync(int userId);
        Task<Booking> GetBookingByIdAsync(int bookingId);
        Task<List<Booking>> GetAllBookingsAsync();
        Task<int> GetActiveBookingCountAsync(int userId);
        Task<bool> CanUserBookAsync(int userId);

        // Waiting List
        Task<WaitingListResult> JoinWaitingListAsync(int userId, int packageId, int numberOfRooms);
        Task<bool> LeaveWaitingListAsync(int userId, int packageId);
        Task<List<WaitingListEntry>> GetWaitingListAsync(int packageId);
        Task<List<WaitingListEntry>> GetUserWaitingListAsync(int userId);
        Task<bool> IsUserNextInWaitingListAsync(int userId, int packageId);
        Task ProcessWaitingListAsync(int packageId);

        // Admin
        Task<List<Booking>> GetBookingsByPackageAsync(int packageId);
        Task SendTripRemindersAsync();
    }

    public class BookingService : IBookingService
    {
        private readonly TravelDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<BookingService> _logger;
        private const int MAX_ACTIVE_BOOKINGS = 3;

        public BookingService(
            TravelDbContext context,
            IEmailService emailService,
            ILogger<BookingService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<BookingResult> CreateBookingAsync(int userId, int packageId, int numberOfRooms, int numberOfGuests)
        {
            // Get user
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return new BookingResult { Success = false, Message = "User not found" };

            // Get package
            var package = await _context.TravelPackages
                .Include(p => p.WaitingList)
                .FirstOrDefaultAsync(p => p.PackageId == packageId);

            if (package == null)
                return new BookingResult { Success = false, Message = "Package not found" };

            // Check if package is active
            if (!package.IsActive)
                return new BookingResult { Success = false, Message = "This package is no longer available" };

            // Check if booking deadline has passed
            if (package.StartDate <= DateTime.UtcNow.AddDays(1))
                return new BookingResult { Success = false, Message = "Booking deadline has passed for this trip" };

            // Check user's active bookings (max 3)
            var activeBookings = await GetActiveBookingCountAsync(userId);
            if (activeBookings >= MAX_ACTIVE_BOOKINGS)
                return new BookingResult { Success = false, Message = $"You cannot have more than {MAX_ACTIVE_BOOKINGS} active bookings" };

            // Check if user already has a booking for this package
            var existingBooking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.UserId == userId &&
                                         b.PackageId == packageId &&
                                         (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            if (existingBooking != null)
                return new BookingResult { Success = false, Message = "You already have a booking for this package" };

            // Check room availability
            if (package.AvailableRooms < numberOfRooms)
            {
                // Check if user is next in waiting list (if there's a waiting list)
                if (package.WaitingList.Any())
                {
                    var isNext = await IsUserNextInWaitingListAsync(userId, packageId);
                    if (!isNext)
                        return new BookingResult { Success = false, Message = "Package is fully booked. Please join the waiting list." };
                }
                else
                {
                    return new BookingResult { Success = false, Message = "Not enough rooms available" };
                }
            }

            // Calculate total price
            var unitPrice = package.DiscountedPrice ?? package.Price;
            var totalPrice = unitPrice * numberOfRooms;

            // Generate booking reference
            var bookingReference = GenerateBookingReference();

            // Create booking
            var booking = new Booking
            {
                UserId = userId,
                PackageId = packageId,
                NumberOfRooms = numberOfRooms,
                NumberOfGuests = numberOfGuests,
                Status = BookingStatus.Pending,
                TotalPrice = totalPrice,
                BookingDate = DateTime.UtcNow,
                BookingReference = bookingReference
            };

            // Start transaction to ensure atomicity
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Reduce available rooms
                package.AvailableRooms -= numberOfRooms;

                // Remove from waiting list if applicable
                var waitingEntry = await _context.WaitingListEntries
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.PackageId == packageId);
                if (waitingEntry != null)
                    _context.WaitingListEntries.Remove(waitingEntry);

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Booking created: {bookingReference} for user {userId}, package {packageId}");

                return new BookingResult
                {
                    Success = true,
                    Message = "Booking created successfully",
                    Booking = booking
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error creating booking: {ex.Message}");
                return new BookingResult { Success = false, Message = "An error occurred while creating your booking" };
            }
        }

        public async Task<BookingResult> ConfirmBookingAsync(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.TravelPackage)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                return new BookingResult { Success = false, Message = "Booking not found" };

            if (booking.Status != BookingStatus.Pending)
                return new BookingResult { Success = false, Message = "Booking is not in pending status" };

            booking.Status = BookingStatus.Confirmed;
            booking.ConfirmedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send confirmation email
            await _emailService.SendBookingConfirmationAsync(
                booking.User.Email,
                booking.BookingReference,
                booking.TravelPackage.Destination,
                booking.TravelPackage.StartDate);

            _logger.LogInformation($"Booking confirmed: {booking.BookingReference}");

            return new BookingResult
            {
                Success = true,
                Message = "Booking confirmed successfully",
                Booking = booking
            };
        }

        public async Task<BookingResult> CancelBookingAsync(int bookingId, string reason)
        {
            var booking = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.TravelPackage)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
                return new BookingResult { Success = false, Message = "Booking not found" };

            if (booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.Completed)
                return new BookingResult { Success = false, Message = "Booking cannot be cancelled" };

            // Check cancellation policy (e.g., cannot cancel within 3 days of departure)
            var daysUntilTrip = (booking.TravelPackage.StartDate - DateTime.UtcNow).TotalDays;
            if (daysUntilTrip < 3)
                return new BookingResult { Success = false, Message = "Cannot cancel booking within 3 days of departure" };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Return rooms to availability
                booking.TravelPackage.AvailableRooms += booking.NumberOfRooms;

                booking.Status = BookingStatus.Cancelled;
                booking.CancelledDate = DateTime.UtcNow;
                booking.CancellationReason = reason;

                await _context.SaveChangesAsync();

                // Process waiting list
                await ProcessWaitingListAsync(booking.PackageId);

                await transaction.CommitAsync();

                // Send cancellation email
                await _emailService.SendCancellationConfirmationAsync(
                    booking.User.Email,
                    booking.BookingReference,
                    booking.TravelPackage.Destination);

                _logger.LogInformation($"Booking cancelled: {booking.BookingReference}");

                return new BookingResult
                {
                    Success = true,
                    Message = "Booking cancelled successfully",
                    Booking = booking
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error cancelling booking: {ex.Message}");
                return new BookingResult { Success = false, Message = "An error occurred while cancelling your booking" };
            }
        }

        public async Task<List<Booking>> GetUserBookingsAsync(int userId)
        {
            return await _context.Bookings
                .Include(b => b.TravelPackage)
                    .ThenInclude(p => p.Images)
                .Include(b => b.Payment)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();
        }

        public async Task<Booking> GetBookingByIdAsync(int bookingId)
        {
            return await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.TravelPackage)
                    .ThenInclude(p => p.Images)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        }

        public async Task<List<Booking>> GetAllBookingsAsync()
        {
            return await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.TravelPackage)
                .Include(b => b.Payment)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();
        }

        public async Task<int> GetActiveBookingCountAsync(int userId)
        {
            return await _context.Bookings
                .CountAsync(b => b.UserId == userId &&
                                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
        }

        public async Task<bool> CanUserBookAsync(int userId)
        {
            var activeCount = await GetActiveBookingCountAsync(userId);
            return activeCount < MAX_ACTIVE_BOOKINGS;
        }

        // Waiting List Methods
        public async Task<WaitingListResult> JoinWaitingListAsync(int userId, int packageId, int numberOfRooms)
        {
            // Check if user already in waiting list
            var existing = await _context.WaitingListEntries
                .FirstOrDefaultAsync(w => w.UserId == userId && w.PackageId == packageId);

            if (existing != null)
                return new WaitingListResult { Success = false, Message = "You are already in the waiting list" };

            // Check if user already has a booking
            var hasBooking = await _context.Bookings
                .AnyAsync(b => b.UserId == userId &&
                              b.PackageId == packageId &&
                              (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));

            if (hasBooking)
                return new WaitingListResult { Success = false, Message = "You already have a booking for this package" };

            // Get current position
            var currentCount = await _context.WaitingListEntries
                .CountAsync(w => w.PackageId == packageId);

            var entry = new WaitingListEntry
            {
                UserId = userId,
                PackageId = packageId,
                NumberOfRooms = numberOfRooms,
                DateAdded = DateTime.UtcNow,
                Position = (currentCount + 1).ToString()
            };

            _context.WaitingListEntries.Add(entry);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User {userId} joined waiting list for package {packageId} at position {entry.Position}");

            return new WaitingListResult
            {
                Success = true,
                Message = $"You have been added to the waiting list at position {entry.Position}",
                Entry = entry
            };
        }

        public async Task<bool> LeaveWaitingListAsync(int userId, int packageId)
        {
            var entry = await _context.WaitingListEntries
                .FirstOrDefaultAsync(w => w.UserId == userId && w.PackageId == packageId);

            if (entry == null)
                return false;

            _context.WaitingListEntries.Remove(entry);
            await _context.SaveChangesAsync();

            // Reorder positions
            var remainingEntries = await _context.WaitingListEntries
                .Where(w => w.PackageId == packageId)
                .OrderBy(w => w.DateAdded)
                .ToListAsync();

            for (int i = 0; i < remainingEntries.Count; i++)
            {
                remainingEntries[i].Position = (i + 1).ToString();
            }

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<List<WaitingListEntry>> GetWaitingListAsync(int packageId)
        {
            return await _context.WaitingListEntries
                .Include(w => w.User)
                .Where(w => w.PackageId == packageId)
                .OrderBy(w => w.DateAdded)
                .ToListAsync();
        }

        public async Task<List<WaitingListEntry>> GetUserWaitingListAsync(int userId)
        {
            return await _context.WaitingListEntries
                .Include(w => w.TravelPackage)
                .Where(w => w.UserId == userId)
                .OrderBy(w => w.DateAdded)
                .ToListAsync();
        }

        public async Task<bool> IsUserNextInWaitingListAsync(int userId, int packageId)
        {
            var firstInLine = await _context.WaitingListEntries
                .Where(w => w.PackageId == packageId)
                .OrderBy(w => w.DateAdded)
                .FirstOrDefaultAsync();

            return firstInLine?.UserId == userId;
        }

        public async Task ProcessWaitingListAsync(int packageId)
        {
            var package = await _context.TravelPackages.FindAsync(packageId);
            if (package == null || package.AvailableRooms <= 0)
                return;

            // Get next person in waiting list
            var nextInLine = await _context.WaitingListEntries
                .Include(w => w.User)
                .Where(w => w.PackageId == packageId && !w.IsNotified)
                .OrderBy(w => w.DateAdded)
                .FirstOrDefaultAsync();

            if (nextInLine == null)
                return;

            // Check if enough rooms available
            if (package.AvailableRooms >= nextInLine.NumberOfRooms)
            {
                // Notify user
                await _emailService.SendWaitingListNotificationAsync(
                    nextInLine.User.Email,
                    package.Destination,
                    package.AvailableRooms);

                nextInLine.IsNotified = true;
                nextInLine.DateNotified = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Notified user {nextInLine.UserId} about availability for package {packageId}");
            }
        }

        public async Task<List<Booking>> GetBookingsByPackageAsync(int packageId)
        {
            return await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Payment)
                .Where(b => b.PackageId == packageId)
                .OrderByDescending(b => b.BookingDate)
                .ToListAsync();
        }

        public async Task SendTripRemindersAsync()
        {
            var reminderDays = new[] { 7, 5, 3, 1 };
            var now = DateTime.UtcNow;

            foreach (var days in reminderDays)
            {
                var targetDate = now.AddDays(days).Date;

                var bookings = await _context.Bookings
                    .Include(b => b.User)
                    .Include(b => b.TravelPackage)
                    .Where(b => b.Status == BookingStatus.Confirmed &&
                               b.TravelPackage.StartDate.Date == targetDate)
                    .ToListAsync();

                foreach (var booking in bookings)
                {
                    await _emailService.SendTripReminderAsync(
                        booking.User.Email,
                        booking.TravelPackage.Destination,
                        booking.TravelPackage.StartDate,
                        days);

                    _logger.LogInformation($"Sent {days}-day reminder to user {booking.UserId} for booking {booking.BookingReference}");
                }
            }
        }

        private string GenerateBookingReference()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var random = new Random().Next(1000, 9999);
            return $"TRV-{timestamp}-{random}";
        }
    }

    public class BookingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Booking Booking { get; set; }
    }

    public class WaitingListResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public WaitingListEntry Entry { get; set; }
    }
}
