using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarehouseApp.Models;

public class WarehouseCell
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(2)]
    public string RowLabel { get; set; } = string.Empty;

    public int ColIndex { get; set; }

    public int? ProductId { get; set; }
    public Product? Product { get; set; }
}
