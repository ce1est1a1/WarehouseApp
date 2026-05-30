using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarehouseApp.Models;

public class WriteOff
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int BatchId { get; set; }

    public int Quantity { get; set; }

    public decimal PurchasePrice { get; set; }

    [NotMapped]
    public decimal TotalLoss => PurchasePrice * Quantity;

    public DateTime WrittenOffAt { get; set; } = DateTime.Now;

    [MaxLength(500)]
    public string Reason { get; set; } = "Истёк срок реализации";
}
