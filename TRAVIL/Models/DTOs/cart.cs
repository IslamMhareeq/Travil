using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// NOTE: Change namespace to match your project (TRAVEL.Models or TRAVIL.Models)
namespace TRAVEL.Models
{
    /// <summary>
    /// Shopping Cart for managing trips before checkout
    /// </summary>
    [Table("Carts")]
    public class Cart
    {
        [Key]
        public int CartId { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();

        [NotMapped]
        public decimal TotalAmount => CalculateTotalAmount();

        [NotMapped]
        public int TotalItems => Items?.Count ?? 0;

        private decimal CalculateTotalAmount()
        {
            decimal total = 0;
            if (Items != null)
            {
                foreach (var item in Items)
                {
                    total += item.Price * item.NumberOfRooms;
                }
            }
            return total;
        }
    }

    /// <summary>
    /// Individual item in the shopping cart
    /// </summary>
    [Table("CartItems")]
    public class CartItem
    {
        [Key]
        public int CartItemId { get; set; }

        [Required]
        public int CartId { get; set; }

        [ForeignKey("CartId")]
        public virtual Cart Cart { get; set; }

        [Required]
        public int PackageId { get; set; }

        [ForeignKey("PackageId")]
        public virtual TravelPackage Package { get; set; }

        [Range(1, 10)]
        public int NumberOfRooms { get; set; } = 1;

        [Range(1, 40)]
        public int NumberOfGuests { get; set; } = 2;

        /// <summary>
        /// Price at the time of adding to cart (captures any discounts)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [StringLength(500)]
        public string SpecialRequests { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public decimal Subtotal => Price * NumberOfRooms;
    }
}