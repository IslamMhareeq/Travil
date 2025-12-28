using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TRAVIL.Models
{
    // User Roles Enum
    public enum UserRole
    {
        Admin,
        User
    }

    // User Status Enum
    public enum UserStatus
    {
        Active,
        Inactive,
        Suspended,
        Deleted
    }

    // PostgreSQL User Model
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [Required]
        [StringLength(255)]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required]
        public UserRole Role { get; set; } = UserRole.User;

        [Required]
        public UserStatus Status { get; set; } = UserStatus.Active;

        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [StringLength(500)]
        public string Address { get; set; }

        [StringLength(100)]
        public string City { get; set; }

        [StringLength(100)]
        public string Country { get; set; }

        [StringLength(10)]
        public string PostalCode { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        [StringLength(500)]
        public string ProfileImageUrl { get; set; }

        public bool EmailVerified { get; set; } = false;

        public DateTime? EmailVerifiedAt { get; set; }

        // Navigation properties
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<WaitingListEntry> WaitingListEntries { get; set; } = new List<WaitingListEntry>();
    }

    // MongoDB User Model (Optional - for logging or analytics)
    [BsonIgnoreExtraElements]
    public class UserLog
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("userId")]
        public int UserId { get; set; }

        [BsonElement("email")]
        public string Email { get; set; }

        [BsonElement("action")]
        public string Action { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; }

        [BsonElement("ipAddress")]
        public string IpAddress { get; set; }

        [BsonElement("details")]
        public string Details { get; set; }
    }

    // Login DTO
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }

    // Register DTO
    public class RegisterRequest
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(100)]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }

        [Phone(ErrorMessage = "Invalid phone format")]
        public string PhoneNumber { get; set; }

        public string Address { get; set; }

        public string City { get; set; }

        public string Country { get; set; }

        public string PostalCode { get; set; }
    }

    // Login Response DTO
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Token { get; set; }
        public UserDto User { get; set; }
    }

    // User DTO (for API responses)
    public class UserDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public UserRole Role { get; set; }
        public UserStatus Status { get; set; }
        public string PhoneNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool EmailVerified { get; set; }
    }
}