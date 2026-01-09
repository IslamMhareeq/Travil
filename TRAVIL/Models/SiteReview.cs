using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRAVEL.Models
{
    /// <summary>
    /// Site Review - User feedback about the overall TRAVIL service
    /// Displayed in "What Users Think About Our Service" section on homepage
    /// </summary>
    [Table("SiteReviews")]
    public class SiteReview
    {
        [Key]
        public int SiteReviewId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        [StringLength(1000)]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsApproved { get; set; } = true; // Admin can moderate

        public bool IsVisible { get; set; } = true; // Show on homepage

        // Navigation property
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}