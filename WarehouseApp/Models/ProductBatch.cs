using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarehouseApp.Models;

public class ProductBatch
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }

    [Column("ExpiryDate")]
    public DateTime? SaleDeadline { get; set; }

    public decimal PurchasePrice { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.Now;

    public int? SupplyId { get; set; }
    public Supply? Supply { get; set; }

    [NotMapped]
    public bool IsOverdue => SaleDeadline.HasValue && SaleDeadline.Value.Date < DateTime.Today;

    [NotMapped]
    public double PeriodProgress
    {
        get
        {
            if (!SaleDeadline.HasValue) return 0;
            var total = (SaleDeadline.Value.Date - ReceivedAt.Date).TotalDays;
            if (total <= 0) return DateTime.Today >= SaleDeadline.Value.Date ? 1.0 : 0.0;
            var elapsed = (DateTime.Today - ReceivedAt.Date).TotalDays;
            return Math.Max(0, Math.Min(1, elapsed / total));
        }
    }

    [NotMapped]
    public int DiscountPercent
    {
        get
        {
            if (!SaleDeadline.HasValue || IsOverdue) return 0;
            double p = PeriodProgress;
            if (p < 2.0 / 3.0) return 0;

            double pct = 30 + (p - 2.0 / 3.0) * 120.0;
            return (int)Math.Round(Math.Min(70, Math.Max(30, pct)));
        }
    }

    [NotMapped]
    public bool IsDiscounted => !IsOverdue && DiscountPercent > 0;

    [NotMapped]
    public string DeadlineDisplay => SaleDeadline.HasValue
        ? SaleDeadline.Value.ToString("dd.MM.yyyy")
        : "Бессрочный";

    [NotMapped]
    public string StatusDisplay
    {
        get
        {
            if (!SaleDeadline.HasValue) return "Бессрочный";
            if (IsOverdue) return "Просрочен";
            if (IsDiscounted) return $"Скидка {DiscountPercent}% (до {SaleDeadline.Value:dd.MM.yyyy})";
            return $"До {SaleDeadline.Value:dd.MM.yyyy}";
        }
    }
}
