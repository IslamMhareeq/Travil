using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TRAVEL.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string recipientEmail, string subject, string body, bool isHtml = true)
        {
            try
            {
                var emailSettings = _configuration.GetSection("EMAIL_CONFIGURATION");
                string host = emailSettings["HOST"];
                int port = int.Parse(emailSettings["PORT"]);
                string senderEmail = emailSettings["EMAIL"];
                string password = emailSettings["PASSWORD"];
                bool enableSSL = bool.Parse(emailSettings["EnableSSL"] ?? "true");

                using (SmtpClient smtpClient = new SmtpClient(host, port))
                {
                    smtpClient.EnableSsl = enableSSL;
                    smtpClient.UseDefaultCredentials = false;
                    smtpClient.Credentials = new NetworkCredential(senderEmail, password);
                    smtpClient.Timeout = 10000; // 10 seconds timeout

                    using (MailMessage mailMessage = new MailMessage(senderEmail, recipientEmail))
                    {
                        mailMessage.Subject = subject;
                        mailMessage.Body = body;
                        mailMessage.IsBodyHtml = isHtml;
                        mailMessage.From = new MailAddress(senderEmail, "Travel Agency Service");

                        await smtpClient.SendMailAsync(mailMessage);

                        _logger.LogInformation($"Email sent successfully to {recipientEmail}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending email to {recipientEmail}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendWelcomeEmailAsync(string email, string firstName)
        {
            string subject = "Welcome to Travel Agency Service!";
            string body = $@"
                <html>
                    <body style='font-family: Arial, sans-serif; color: #333;'>
                        <h2>Welcome to Travel Agency Service, {firstName}!</h2>
                        <p>Thank you for registering with us. We're excited to help you plan your next adventure.</p>
                        
                        <p>Here's what you can do with your account:</p>
                        <ul>
                            <li>Browse our extensive collection of travel packages</li>
                            <li>Book up to 3 trips at the same time</li>
                            <li>Manage your bookings and payments</li>
                            <li>Join waiting lists for fully booked packages</li>
                            <li>Share your travel experiences with ratings and reviews</li>
                        </ul>
                        
                        <p>Happy travels!</p>
                        <p><strong>Travel Agency Service Team</strong></p>
                    </body>
                </html>";

            return await SendEmailAsync(email, subject, body, true);
        }

        public async Task<bool> SendBookingConfirmationAsync(string email, string bookingReference, string destination, DateTime startDate)
        {
            string subject = $"Booking Confirmation - {destination}";
            string body = $@"
                <html>
                    <body style='font-family: Arial, sans-serif; color: #333;'>
                        <h2>Booking Confirmation</h2>
                        <p>Your booking has been confirmed!</p>
                        
                        <table style='border-collapse: collapse; width: 100%; margin-top: 20px;'>
                            <tr style='background-color: #f0f0f0;'>
                                <td style='padding: 10px; border: 1px solid #ddd;'><strong>Booking Reference</strong></td>
                                <td style='padding: 10px; border: 1px solid #ddd;'>{bookingReference}</td>
                            </tr>
                            <tr>
                                <td style='padding: 10px; border: 1px solid #ddd;'><strong>Destination</strong></td>
                                <td style='padding: 10px; border: 1px solid #ddd;'>{destination}</td>
                            </tr>
                            <tr style='background-color: #f0f0f0;'>
                                <td style='padding: 10px; border: 1px solid #ddd;'><strong>Departure Date</strong></td>
                                <td style='padding: 10px; border: 1px solid #ddd;'>{startDate:MMMM dd, yyyy}</td>
                            </tr>
                        </table>
                        
                        <p style='margin-top: 20px;'>You can download your itinerary from your personal dashboard.</p>
                        <p><strong>Travel Agency Service Team</strong></p>
                    </body>
                </html>";

            return await SendEmailAsync(email, subject, body, true);
        }

        public async Task<bool> SendWaitingListNotificationAsync(string email, string destination, int roomsAvailable)
        {
            string subject = $"Great News! Room Available - {destination}";
            string body = $@"
                <html>
                    <body style='font-family: Arial, sans-serif; color: #333;'>
                        <h2>Room Available!</h2>
                        <p>Good news! A room has become available for <strong>{destination}</strong>.</p>
                        <p>We currently have <strong>{roomsAvailable} room(s)</strong> available.</p>
                        
                        <p>This opportunity is reserved for you for the next 24 hours.</p>
                        <p><a href='https://travelagency.com/book' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Book Now</a></p>
                        
                        <p><strong>Travel Agency Service Team</strong></p>
                    </body>
                </html>";

            return await SendEmailAsync(email, subject, body, true);
        }

        public async Task<bool> SendTripReminderAsync(string email, string destination, DateTime departureDate, int daysRemaining)
        {
            string subject = $"Trip Reminder - {destination} ({daysRemaining} days away)";
            string body = $@"
                <html>
                    <body style='font-family: Arial, sans-serif; color: #333;'>
                        <h2>Trip Reminder</h2>
                        <p>Your trip to <strong>{destination}</strong> is coming up in <strong>{daysRemaining} days</strong>!</p>
                        
                        <p><strong>Departure Date:</strong> {departureDate:MMMM dd, yyyy}</p>
                        
                        <p>Don't forget to:</p>
                        <ul>
                            <li>Check your itinerary</li>
                            <li>Confirm your payment</li>
                            <li>Arrange transportation to the airport</li>
                            <li>Pack your bags!</li>
                        </ul>
                        
                        <p>You can download your complete itinerary from your dashboard.</p>
                        <p><strong>Travel Agency Service Team</strong></p>
                    </body>
                </html>";

            return await SendEmailAsync(email, subject, body, true);
        }

        public async Task<bool> SendPaymentReceiptAsync(string email, string bookingReference, decimal amount, DateTime paymentDate)
        {
            string subject = $"Payment Receipt - {bookingReference}";
            string body = $@"
                <html>
                    <body style='font-family: Arial, sans-serif; color: #333;'>
                        <h2>Payment Receipt</h2>
                        <p>Thank you for your payment!</p>
                        
                        <table style='border-collapse: collapse; width: 100%; margin-top: 20px;'>
                            <tr style='background-color: #f0f0f0;'>
                                <td style='padding: 10px; border: 1px solid #ddd;'><strong>Booking Reference</strong></td>
                                <td style='padding: 10px; border: 1px solid #ddd;'>{bookingReference}</td>
                            </tr>
                            <tr>
                                <td style='padding: 10px; border: 1px solid #ddd;'><strong>Amount Paid</strong></td>
                                <td style='padding: 10px; border: 1px solid #ddd;'>${amount:F2}</td>
                            </tr>
                            <tr style='background-color: #f0f0f0;'>
                                <td style='padding: 10px; border: 1px solid #ddd;'><strong>Payment Date</strong></td>
                                <td style='padding: 10px; border: 1px solid #ddd;'>{paymentDate:MMMM dd, yyyy HH:mm:ss}</td>
                            </tr>
                        </table>
                        
                        <p style='margin-top: 20px;'>Your booking has been confirmed.</p>
                        <p><strong>Travel Agency Service Team</strong></p>
                    </body>
                </html>";

            return await SendEmailAsync(email, subject, body, true);
        }

        public async Task<bool> SendCancellationConfirmationAsync(string email, string bookingReference, string destination)
        {
            string subject = $"Cancellation Confirmed - {destination}";
            string body = $@"
                <html>
                    <body style='font-family: Arial, sans-serif; color: #333;'>
                        <h2>Cancellation Confirmed</h2>
                        <p>Your booking has been cancelled.</p>
                        
                        <table style='border-collapse: collapse; width: 100%; margin-top: 20px;'>
                            <tr style='background-color: #f0f0f0;'>
                                <td style='padding: 10px; border: 1px solid #ddd;'><strong>Booking Reference</strong></td>
                                <td style='padding: 10px; border: 1px solid #ddd;'>{bookingReference}</td>
                            </tr>
                            <tr>
                                <td style='padding: 10px; border: 1px solid #ddd;'><strong>Destination</strong></td>
                                <td style='padding: 10px; border: 1px solid #ddd;'>{destination}</td>
                            </tr>
                        </table>
                        
                        <p style='margin-top: 20px;'>If you have any questions, please contact our support team.</p>
                        <p><strong>Travel Agency Service Team</strong></p>
                    </body>
                </html>";

            return await SendEmailAsync(email, subject, body, true);
        }
    }
}