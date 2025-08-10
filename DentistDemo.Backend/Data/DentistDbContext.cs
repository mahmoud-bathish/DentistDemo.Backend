using DentistDemo.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace DentistDemo.Backend.Data
{
    public class DentistDbContext : DbContext
    {
        public DentistDbContext(DbContextOptions<DentistDbContext> options) : base(options)
        {
        }

        public DbSet<Booking> Bookings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Booking entity
            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PatientName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DateTime).IsRequired();
                entity.Property(e => e.Status).HasDefaultValue(BookingStatus.Pending);
                entity.Property(e => e.ReasonForVisit).HasMaxLength(500); // Removed IsRequired()

                // Indexes for efficient queries
                entity.HasIndex(e => e.DateTime);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.PhoneNumber);
            });
        }
    }
}
