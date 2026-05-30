using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WarehouseApp.Models;

public enum UserRole
{
    Administrator = 0,
    Storekeeper = 1
}

public class User
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Login { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Storekeeper;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [NotMapped]
    public bool IsAdmin => Role == UserRole.Administrator;

    [NotMapped]
    public bool IsStorekeeper => Role == UserRole.Storekeeper;

    [NotMapped]
    public string RoleDisplayName => Role switch
    {
        UserRole.Administrator => "Администратор",
        UserRole.Storekeeper => "Кладовщик",
        _ => "Неизвестно"
    };
}
