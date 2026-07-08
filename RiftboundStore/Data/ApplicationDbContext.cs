using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RiftboundStore.Models;

namespace RiftboundStore.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Card>(b =>
        {
            b.HasIndex(c => c.Name);
            b.HasIndex(c => new { c.Number, c.Edition, c.Language, c.IsFoil })
                .IsUnique(false);
        });

        builder.Entity<CartItem>(b =>
        {
            b.HasIndex(c => new { c.UserId, c.CardId }).IsUnique();
            b.HasOne(c => c.Card).WithMany().HasForeignKey(c => c.CardId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Order>(b =>
        {
            b.Property(o => o.DonationAmount).HasColumnType("decimal(10,2)");
            b.HasMany(o => o.Items).WithOne(i => i.Order!)
                .HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(o => o.CreatedAt);
        });

        builder.Entity<OrderItem>(b =>
        {
            b.HasOne(i => i.Card).WithMany().HasForeignKey(i => i.CardId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
