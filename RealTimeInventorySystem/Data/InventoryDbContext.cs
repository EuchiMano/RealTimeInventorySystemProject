using Microsoft.EntityFrameworkCore;
using RealTimeInventorySystem.Models;

namespace RealTimeInventorySystem.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<Inventory> Inventories { get; set; }
    public DbSet<InventoryMovement> InventoryMovements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .ToTable("Products")
            .HasKey(x => x.ProductId);

        modelBuilder.Entity<Product>()
            .HasIndex(x => x.ProductCode)
            .IsUnique();

        modelBuilder.Entity<Warehouse>()
            .ToTable("Warehouses")
            .HasKey(x => x.WarehouseId);

        modelBuilder.Entity<Inventory>()
            .ToTable("Inventory")
            .HasKey(x => x.InventoryId);

        modelBuilder.Entity<Inventory>()
            .HasIndex(x => new
            {
                x.ProductId,
                x.WarehouseId
            })
            .IsUnique();

        modelBuilder.Entity<Inventory>()
            .HasOne(x => x.Product)
            .WithMany(x => x.Inventories)
            .HasForeignKey(x => x.ProductId);

        modelBuilder.Entity<Inventory>()
            .HasOne(x => x.Warehouse)
            .WithMany(x => x.Inventories)
            .HasForeignKey(x => x.WarehouseId);

        modelBuilder.Entity<Inventory>()
            .Property(x => x.Version)
            .IsConcurrencyToken();

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.ToTable("InventoryMovements");
            entity.HasKey(e => e.MovementId);
            entity.Property(e => e.MovementType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Reason).HasMaxLength(100);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(50);
            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(255);

            entity.HasIndex(e => new { e.ProductId, e.WarehouseId });
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.MovementType);

            entity.HasOne(e => e.Product)
                .WithMany(p => p.Movements)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Warehouse)
                .WithMany(w => w.Movements)
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
