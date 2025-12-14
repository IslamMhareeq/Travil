using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TravelAgency.Models
{
    public class Package
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Destination is required")]
        [MaxLength(100)]
        public string Destination { get; set; }

        [Required(ErrorMessage = "Country is required")]
        [MaxLength(100)]
        public string Country { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, 99999.99)]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Number of rooms is required")]
        [Range(1, 1000)]
        public int RoomsAvailable { get; set; }

        [Required]
        public string PackageType { get; set; } // family, honeymoon, adventure, cruise, luxury, budget

        public int AgeLimit { get; set; } = 0;

        [Required]
        public string Description { get; set; }

        public string ImageUrl { get; set; } = "/images/placeholder.jpg";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<Discount> Discounts { get; set; } = new List<Discount>();
    }
}