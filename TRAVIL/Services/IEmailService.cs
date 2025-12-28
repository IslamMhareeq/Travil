namespace TRAVEL.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string recipientEmail, string subject, string body, bool isHtml = true);
        Task<bool> SendWelcomeEmailAsync(string email, string firstName);
        Task<bool> SendBookingConfirmationAsync(string email, string bookingReference, string destination, DateTime startDate);
        Task<bool> SendWaitingListNotificationAsync(string email, string destination, int roomsAvailable);
        Task<bool> SendTripReminderAsync(string email, string destination, DateTime departureDate, int daysRemaining);
        Task<bool> SendPaymentReceiptAsync(string email, string bookingReference, decimal amount, DateTime paymentDate);
        Task<bool> SendCancellationConfirmationAsync(string email, string bookingReference, string destination);
    }
}
