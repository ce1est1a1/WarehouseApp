using WarehouseApp.Models;

namespace WarehouseApp.Data.Repositories;

public interface IUserRepository
{
    User? GetByLogin(string login);
    void Add(User user);
    bool LoginExists(string login);
    void Save();
}
