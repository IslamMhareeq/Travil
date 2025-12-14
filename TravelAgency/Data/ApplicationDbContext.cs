using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using TravelAgency.Models;

namespace TravelAgency.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<Package> Packages { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Discount> Discounts { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<WebsiteRating> WebsiteRatings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Relationships
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Package)
                .WithMany(p => p.Bookings)
                .HasForeignKey(b => b.PackageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Package)
                .WithMany(p => p.Reviews)
                .HasForeignKey(r => r.PackageId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Discount>()
                .HasOne(d => d.Package)
                .WithMany(p => p.Discounts)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraints
            modelBuilder.Entity<Booking>()
                .HasIndex(b => new { b.UserId, b.PackageId })
                .IsUnique();

            // Seed sample packages
            SeedPackages(modelBuilder);
        }

        private void SeedPackages(ModelBuilder modelBuilder)
        {
            var packages = new List<Package>
            {
                new() { Id = 1, Destination = "Paris", Country = "France", StartDate = new(2026, 3, 15), EndDate = new(2026, 3, 22), Price = 1200, RoomsAvailable = 10, PackageType = "family", Description = "Romantic Paris getaway with Eiffel Tower tour", ImageUrl = "/images/paris.jpg" },
                new() { Id = 2, Destination = "Tokyo", Country = "Japan", StartDate = new(2026, 4, 1), EndDate = new(2026, 4, 10), Price = 1800, RoomsAvailable = 8, PackageType = "adventure", Description = "Experience traditional Japanese culture", ImageUrl = "/images/tokyo.jpg" },
                new() { Id = 3, Destination = "Maldives", Country = "Maldives", StartDate = new(2026, 5, 10), EndDate = new(2026, 5, 17), Price = 2500, RoomsAvailable = 5, PackageType = "honeymoon", Description = "Luxury island honeymoon package", ImageUrl = "/images/maldives.jpg" },
                new() { Id = 4, Destination = "Dubai", Country = "UAE", StartDate = new(2026, 2, 15), EndDate = new(2026, 2, 22), Price = 950, RoomsAvailable = 15, PackageType = "luxury", Description = "Luxury shopping and desert safari", ImageUrl = "/images/dubai.jpg" },
                new() { Id = 5, Destination = "Barcelona", Country = "Spain", StartDate = new(2026, 6, 1), EndDate = new(2026, 6, 8), Price = 1100, RoomsAvailable = 12, PackageType = "family", Description = "Beach and architecture experience", ImageUrl = "/images/barcelona.jpg" },
                new() { Id = 6, Destination = "Sydney", Country = "Australia", StartDate = new(2026, 7, 10), EndDate = new(2026, 7, 20), Price = 1600, RoomsAvailable = 10, PackageType = "adventure", Description = "Great Barrier Reef and Opera House", ImageUrl = "/images/sydney.jpg" },
                new() { Id = 7, Destination = "Rome", Country = "Italy", StartDate = new(2026, 3, 20), EndDate = new(2026, 3, 27), Price = 1050, RoomsAvailable = 12, PackageType = "family", Description = "Ancient history and Italian cuisine", ImageUrl = "/images/rome.jpg" },
                new() { Id = 8, Destination = "Bangkok", Country = "Thailand", StartDate = new(2026, 2, 1), EndDate = new(2026, 2, 8), Price = 800, RoomsAvailable = 20, PackageType = "adventure", Description = "Street food and Buddhist temples", ImageUrl = "/images/bangkok.jpg" },
                new() { Id = 9, Destination = "Iceland", Country = "Iceland", StartDate = new(2026, 8, 5), EndDate = new(2026, 8, 12), Price = 1900, RoomsAvailable = 8, PackageType = "adventure", Description = "Northern lights and waterfalls", ImageUrl = "/images/iceland.jpg" },
                new() { Id = 10, Destination = "Bali", Country = "Indonesia", StartDate = new(2026, 4, 15), EndDate = new(2026, 4, 25), Price = 900, RoomsAvailable = 18, PackageType = "family", Description = "Tropical paradise and rice terraces", ImageUrl = "/images/bali.jpg" },
                new() { Id = 11, Destination = "New York", Country = "USA", StartDate = new(2026, 5, 1), EndDate = new(2026, 5, 8), Price = 1400, RoomsAvailable = 10, PackageType = "luxury", Description = "City that never sleeps experience", ImageUrl = "/images/newyork.jpg" },
                new() { Id = 12, Destination = "Santorini", Country = "Greece", StartDate = new(2026, 6, 10), EndDate = new(2026, 6, 17), Price = 1300, RoomsAvailable = 6, PackageType = "honeymoon", Description = "White buildings and sunset views", ImageUrl = "/images/santorini.jpg" },
                new() { Id = 13, Destination = "London", Country = "UK", StartDate = new(2026, 3, 1), EndDate = new(2026, 3, 8), Price = 1200, RoomsAvailable = 14, PackageType = "family", Description = "Royal palaces and cultural museums", ImageUrl = "/images/london.jpg" },
                new() { Id = 14, Destination = "Amsterdam", Country = "Netherlands", StartDate = new(2026, 4, 20), EndDate = new(2026, 4, 27), Price = 950, RoomsAvailable = 12, PackageType = "family", Description = "Canals and cycling culture", ImageUrl = "/images/amsterdam.jpg" },
                new() { Id = 15, Destination = "Cairo", Country = "Egypt", StartDate = new(2026, 2, 10), EndDate = new(2026, 2, 20), Price = 850, RoomsAvailable = 12, PackageType = "adventure", Description = "Pyramids and Nile River cruise", ImageUrl = "/images/cairo.jpg" },
                new() { Id = 16, Destination = "Singapore", Country = "Singapore", StartDate = new(2026, 5, 15), EndDate = new(2026, 5, 22), Price = 1100, RoomsAvailable = 10, PackageType = "luxury", Description = "Modern city-state exploration", ImageUrl = "/images/singapore.jpg" },
                new() { Id = 17, Destination = "Vietnam", Country = "Vietnam", StartDate = new(2026, 3, 5), EndDate = new(2026, 3, 15), Price = 650, RoomsAvailable = 16, PackageType = "budget", Description = "Ha Long Bay and street markets", ImageUrl = "/images/vietnam.jpg" },
                new() { Id = 18, Destination = "Canada", Country = "Canada", StartDate = new(2026, 7, 1), EndDate = new(2026, 7, 10), Price = 1700, RoomsAvailable = 8, PackageType = "adventure", Description = "Niagara Falls and Rocky Mountains", ImageUrl = "/images/canada.jpg" },
                new() { Id = 19, Destination = "Morocco", Country = "Morocco", StartDate = new(2026, 4, 10), EndDate = new(2026, 4, 20), Price = 700, RoomsAvailable = 14, PackageType = "budget", Description = "Marrakech markets and Sahara Desert", ImageUrl = "/images/morocco.jpg" },
                new() { Id = 20, Destination = "Switzerland", Country = "Switzerland", StartDate = new(2026, 8, 10), EndDate = new(2026, 8, 18), Price = 2000, RoomsAvailable = 6, PackageType = "luxury", Description = "Alpine hiking and scenic trains", ImageUrl = "/images/switzerland.jpg" },
                new() { Id = 21, Destination = "Mexico City", Country = "Mexico", StartDate = new(2026, 2, 20), EndDate = new(2026, 2, 28), Price = 850, RoomsAvailable = 12, PackageType = "family", Description = "Aztec ruins and colonial architecture", ImageUrl = "/images/mexcity.jpg" },
                new() { Id = 22, Destination = "Portugal", Country = "Portugal", StartDate = new(2026, 5, 25), EndDate = new(2026, 6, 2), Price = 700, RoomsAvailable = 15, PackageType = "budget", Description = "Lisbon beaches and cork forests", ImageUrl = "/images/portugal.jpg" },
                new() { Id = 23, Destination = "Peru", Country = "Peru", StartDate = new(2026, 6, 15), EndDate = new(2026, 6, 25), Price = 1200, RoomsAvailable = 10, PackageType = "adventure", Description = "Machu Picchu and Amazon rainforest", ImageUrl = "/images/peru.jpg" },
                new() { Id = 24, Destination = "South Korea", Country = "South Korea", StartDate = new(2026, 3, 25), EndDate = new(2026, 4, 2), Price = 1100, RoomsAvailable = 12, PackageType = "family", Description = "K-pop culture and palaces", ImageUrl = "/images/korea.jpg" },
                new() { Id = 25, Destination = "Caribbean Cruise", Country = "Caribbean", StartDate = new(2026, 7, 20), EndDate = new(2026, 7, 30), Price = 1800, RoomsAvailable = 4, PackageType = "cruise", Description = "European canal cruise experience", ImageUrl = "/images/cruise.jpg" },
            };

            modelBuilder.Entity<Package>().HasData(packages);
        }
    }
}