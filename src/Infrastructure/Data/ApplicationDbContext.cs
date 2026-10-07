using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var p = b.Entity<Product>();
        p.ToTable("Product");
        p.Property(x => x.ProductName).HasMaxLength(255).IsRequired();
        p.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        p.Property(x => x.ModifiedBy).HasMaxLength(100);
        p.Property(x => x.CreatedOn).HasColumnType("datetime");
        p.Property(x => x.ModifiedOn).HasColumnType("datetime");
        p.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        var i = b.Entity<Item>();
        i.ToTable("Item", t => t.HasCheckConstraint("CK_Item_Quantity", "[Quantity] >= 0"));
        i.HasIndex(x => x.ProductId);
        var u = b.Entity<User>();
        u.Property(x => x.Username).HasMaxLength(100).IsRequired();
        u.HasIndex(x => x.Username).IsUnique();
        u.Property(x => x.Role).HasMaxLength(20);
        var t = b.Entity<RefreshToken>();
        t.Property(x => x.Hash).HasMaxLength(64);
        t.HasIndex(x => x.Hash).IsUnique();
        t.HasIndex(x => x.Family);
        t.Property(x => x.Version).IsRowVersion();
        t.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
    }
}
