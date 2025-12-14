using System;
using System.ComponentModel.DataAnnotations;

namespace TravelAgency.Models
{
    public class Discount
    {
        public int Id { get; set; }

        [Required]
        public int PackageId { get; set; }

        public Package Package { get; set; }

        [Required]
        [Range(1, 100)]
        public int Percentage { get; set; }

        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime EndDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}