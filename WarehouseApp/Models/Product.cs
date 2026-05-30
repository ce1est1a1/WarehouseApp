using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarehouseApp.Models;

public class Product
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Article { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    [MaxLength(50)]
    public string Unit { get; set; } = "шт.";

    public decimal PurchasePrice { get; set; }

    public int StockQuantity { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImagePath { get; set; }

    [MaxLength(300)]
    public string? ExtraField1 { get; set; }

    [MaxLength(300)]
    public string? ExtraField2 { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<ProductBatch> Batches { get; set; } = new List<ProductBatch>();

    [NotMapped]
    public DateTime? NearestDeadline => Batches?
        .Where(b => b.SaleDeadline.HasValue && b.Quantity > 0)
        .OrderBy(b => b.SaleDeadline)
        .FirstOrDefault()?.SaleDeadline;

    [NotMapped]
    public bool HasOverdueBatches => Batches?.Any(b => b.IsOverdue && b.Quantity > 0) == true;

    [NotMapped]
    public bool HasDiscountedBatches => Batches?.Any(b => b.IsDiscounted && b.Quantity > 0) == true;

    [NotMapped]
    public int OverdueQuantity => Batches?.Where(b => b.IsOverdue && b.Quantity > 0).Sum(b => b.Quantity) ?? 0;

    [NotMapped]
    public decimal OverdueLoss => Batches?
        .Where(b => b.IsOverdue && b.Quantity > 0)
        .Sum(b => b.PurchasePrice * b.Quantity) ?? 0;

    [NotMapped]
    public int DiscountPercent
    {
        get
        {
            if (Batches == null) return 0;
            var active = Batches.Where(b => b.Quantity > 0 && !b.IsOverdue).ToList();
            if (active.Count == 0) return 0;
            return active.Max(b => b.DiscountPercent);
        }
    }

    [NotMapped]
    public decimal DiscountedPrice => DiscountPercent > 0
        ? Math.Round(PurchasePrice * (100 - DiscountPercent) / 100m, 2)
        : PurchasePrice;

    [NotMapped]
    public string DeadlineDisplayShort
    {
        get
        {
            if (Batches == null || !Batches.Any(b => b.Quantity > 0)) return "";
            if (HasOverdueBatches) return "Просрочен!";
            var nearest = NearestDeadline;
            if (!nearest.HasValue) return "";
            if (HasDiscountedBatches) return $"Скидка до {nearest.Value:dd.MM}";
            return $"до {nearest.Value:dd.MM.yyyy}";
        }
    }

    [NotMapped]
    public string DisplayPrice => PurchasePrice >= 10000000
        ? $"{PurchasePrice:N0}... р"
        : $"{PurchasePrice:N0} р.";

    [NotMapped]
    public string DisplayStock => StockQuantity >= 10000
        ? $"{StockQuantity}... {Unit}"
        : $"{StockQuantity} {Unit}";

    [NotMapped]
    public string TruncatedName => Name.Length > 25
        ? Name[..22] + "..."
        : Name;
}
