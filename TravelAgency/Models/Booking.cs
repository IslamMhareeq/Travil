using System;
using System.ComponentModel.DataAnnotations;

namespace TravelAgency.Models
{
    public class Booking
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        public ApplicationUser User { get; set; }

        [Required]
        public int PackageId { get; set; }

        public Package Package { get; set; }

        public DateTime BookingDate { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled

        public string PaymentStatus { get; set; } = "Pending"; // Pending, Completed, Failed

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}