using Microsoft.EntityFrameworkCore;

namespace Shared;

public sealed class TicketDbContext(DbContextOptions<TicketDbContext> options) : DbContext(options)
{
    public DbSet<EventItem> Events => Set<EventItem>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.UserId).HasMaxLength(200).IsRequired();
            entity.Property(b => b.Status).HasConversion<string>();
            entity.Property(b => b.RejectionReason).HasMaxLength(500);
        });
    }
}
