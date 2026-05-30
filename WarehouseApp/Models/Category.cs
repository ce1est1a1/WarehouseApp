using System.ComponentModel.DataAnnotations;

namespace WarehouseApp.Models;

public class Category
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Product> Products { get; set; } = new List<Product>();

    public override string ToString() => Name;
}
