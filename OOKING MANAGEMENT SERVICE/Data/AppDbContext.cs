using BookingManagementService.Models;
using Microsoft.EntityFrameworkCore;

namespace BookingManagementService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Resource> Resources { get; set; }
    public DbSet<User> Users { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User constraints
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Id).HasMaxLength(50);
            entity.Property(u => u.Name).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
        });

        // Resource constraints
        modelBuilder.Entity<Resource>(entity =>
        {
            entity.Property(r => r.Id).HasMaxLength(50);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(100);
            entity.Property(r => r.Type).IsRequired().HasMaxLength(50);
        });

        // Booking constraints & relationships
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(b => b.ResourceId).IsRequired().HasMaxLength(50);
            entity.Property(b => b.UserId).IsRequired().HasMaxLength(50);

            entity.HasOne(b => b.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Resource)
                .WithMany(r => r.Bookings)
                .HasForeignKey(b => b.ResourceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Composite index for overlap checks and date range filtering
            entity.HasIndex(b => new { b.ResourceId, b.Status, b.StartDateTime, b.EndDateTime });
        });
    }
}