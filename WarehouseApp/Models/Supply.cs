using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarehouseApp.Models;

public class Supply
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Supplier { get; set; } = string.Empty;

    public decimal TotalCost { get; set; }

    public DateTime SuppliedAt { get; set; } = DateTime.Now;

    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public decimal UsdRate { get; set; }
    public decimal EurRate { get; set; }
    public decimal UsdtRate { get; set; }

    public ICollection<SupplyItem> Items { get; set; } = new List<SupplyItem>();

    public string? CheckJson { get; set; }

    private ShipmentLogistics? _checkCache;

    [NotMapped]
    public ShipmentLogistics? Check
    {
        get => _checkCache ??= ShipmentLogistics.FromJson(CheckJson);
        set { _checkCache = value; CheckJson = value?.ToJson(); }
    }

    [NotMapped]
    public string DisplayDate => SuppliedAt.ToString("dd.MM.yyyy");

    [NotMapped]
    public string DisplayCost => TotalCost >= 10000000
        ? $"{TotalCost:N0}... р."
        : $"{TotalCost:N0} р.";

    internal decimal? GetStoredRate(string currency) => currency switch
    {
        "USD" => UsdRate > 0 ? UsdRate : null,
        "EUR" => EurRate > 0 ? EurRate : null,
        "USDT" => UsdtRate > 0 ? UsdtRate : null,
        _ => null
    };
}

public class SupplyItem
{
    [Key]
    public int Id { get; set; }

    public int SupplyId { get; set; }
    public Supply? Supply { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal PurchasePrice { get; set; }

    public int Quantity { get; set; }

    [Column("ExpiryDate")]
    public DateTime? SaleDeadline { get; set; }

    [NotMapped]
    public decimal Subtotal => PurchasePrice * Quantity;

    [NotMapped]
    public string DisplayPrice => $"{PurchasePrice:N0} р.";
}
