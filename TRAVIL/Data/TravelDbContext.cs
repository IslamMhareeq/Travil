using Microsoft.AspNetCore.Mvc.ViewEngines;
using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using TRAVIL.Models;

namespace TRAVIL.Data
{
    public class TravelDbContext : DbContext
    {
        public TravelDbContext(DbContextOptions<TravelDbContext> options) : base(options)
        {
        }

        // DbSets
        public DbSet<User> Users { get; set; }
        public DbSet<TravelPackage> TravelPackages { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<WaitingListEntry> WaitingListEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User Configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValue(DateTime.UtcNow);
            });

            // TravelPackage Configuration
            modelBuilder.Entity<TravelPackage>(entity =>
            {
                entity.HasKey(e => e.PackageId);
                entity.Property(e => e.Destination).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Country).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Price).HasPrecision(10, 2);
                entity.Property(e => e.DiscountedPrice).HasPrecision(10, 2);
                entity.Property(e => e.CreatedAt).HasDefaultValue(DateTime.UtcNow);
            });

            // Booking Configuration
            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasKey(e => e.BookingId);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Bookings)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.TravelPackage)
                    .WithMany(p => p.Bookings)
                    .HasForeignKey(e => e.PackageId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.BookingDate).HasDefaultValue(DateTime.UtcNow);
            });

            // Payment Configuration
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.PaymentId);
                entity.HasOne(e => e.Booking)
                    .WithOne(b => b.Payment)
                    .HasForeignKey<Payment>(e => e.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.Amount).HasPrecision(10, 2);
                entity.Property(e => e.PaymentDate).HasDefaultValue(DateTime.UtcNow);
            });

            // Review Configuration
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasKey(e => e.ReviewId);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Reviews)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.TravelPackage)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(e => e.PackageId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.Property(e => e.CreatedAt).HasDefaultValue(DateTime.UtcNow);
            });

            // WaitingListEntry Configuration
            modelBuilder.Entity<WaitingListEntry>(entity =>
            {
                entity.HasKey(e => e.WaitingListId);
                entity.HasOne(e => e.User)
                    .WithMany(u => u.WaitingListEntries)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.TravelPackage)
                    .WithMany(p => p.WaitingList)
                    .HasForeignKey(e => e.PackageId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(e => e.DateAdded).HasDefaultValue(DateTime.UtcNow);
            });
        }
    }
}