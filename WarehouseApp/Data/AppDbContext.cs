using Microsoft.EntityFrameworkCore;
using WarehouseApp.Models;

namespace WarehouseApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentItem> ShipmentItems => Set<ShipmentItem>();
    public DbSet<Supply> Supplies => Set<Supply>();
    public DbSet<SupplyItem> SupplyItems => Set<SupplyItem>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<WriteOff> WriteOffs => Set<WriteOff>();
    public DbSet<WarehouseCell> WarehouseCells => Set<WarehouseCell>();

    private readonly string _connectionString;

    public AppDbContext(string connectionString = "Data Source=warehouse.db")
    {
        _connectionString = connectionString;
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite(_connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Login).IsUnique();
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasOne(p => p.Category)
             .WithMany(c => c.Products)
             .HasForeignKey(p => p.CategoryId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Shipment>(e =>
        {
            e.HasOne(s => s.CreatedByUser)
             .WithMany()
             .HasForeignKey(s => s.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShipmentItem>(e =>
        {
            e.HasOne(si => si.Shipment)
             .WithMany(s => s.Items)
             .HasForeignKey(si => si.ShipmentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(si => si.Product)
             .WithMany()
             .HasForeignKey(si => si.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Supply>(e =>
        {
            e.HasOne(s => s.CreatedByUser)
             .WithMany()
             .HasForeignKey(s => s.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupplyItem>(e =>
        {
            e.HasOne(si => si.Supply)
             .WithMany(s => s.Items)
             .HasForeignKey(si => si.SupplyId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(si => si.Product)
             .WithMany()
             .HasForeignKey(si => si.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductBatch>(e =>
        {
            e.HasOne(b => b.Product)
             .WithMany(p => p.Batches)
             .HasForeignKey(b => b.ProductId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(b => b.Supply)
             .WithMany()
             .HasForeignKey(b => b.SupplyId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WriteOff>(e =>
        {
            e.HasOne(w => w.Product)
             .WithMany()
             .HasForeignKey(w => w.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WarehouseCell>(e =>
        {
            e.HasIndex(c => c.Code).IsUnique();
            e.HasOne(c => c.Product)
             .WithMany()
             .HasForeignKey(c => c.ProductId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
