using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarehouseApp.Models;

public class Shipment
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Recipient { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    public decimal TotalCost { get; set; }

    public DateTime ShippedAt { get; set; } = DateTime.Now;

    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public decimal UsdRate { get; set; }
    public decimal EurRate { get; set; }
    public decimal UsdtRate { get; set; }

    public ICollection<ShipmentItem> Items { get; set; } = new List<ShipmentItem>();

    public string? LogisticsJson { get; set; }

    private ShipmentLogistics? _logisticsCache;

    [NotMapped]
    public ShipmentLogistics? Logistics
    {
        get => _logisticsCache ??= ShipmentLogistics.FromJson(LogisticsJson);
        set { _logisticsCache = value; LogisticsJson = value?.ToJson(); }
    }

    [NotMapped]
    public string DisplayDate => ShippedAt.ToString("dd.MM.yyyy");

    [NotMapped]
    public string DisplayCost => TotalCost >= 10000000
        ? $"{TotalCost:N0}... р."
        : $"{TotalCost:N0} р.";

    [NotMapped]
    public decimal TotalPurchaseCost => Items?.Sum(i => i.PurchaseCost * i.Quantity) ?? 0;

    [NotMapped]
    public decimal Profit => TotalCost - TotalPurchaseCost;

    internal decimal? GetStoredRate(string currency) => currency switch
    {
        "USD" => UsdRate > 0 ? UsdRate : null,
        "EUR" => EurRate > 0 ? EurRate : null,
        "USDT" => UsdtRate > 0 ? UsdtRate : null,
        _ => null
    };
}

public class ShipmentItem
{
    [Key]
    public int Id { get; set; }

    public int ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal Price { get; set; }

    public decimal PurchaseCost { get; set; }

    public int Quantity { get; set; }

    [NotMapped]
    public decimal Subtotal => Price * Quantity;

    [NotMapped]
    public string DisplayPrice => $"{Price:N0} р.";
}
