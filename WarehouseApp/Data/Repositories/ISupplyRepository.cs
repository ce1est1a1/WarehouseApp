using WarehouseApp.Models;

namespace WarehouseApp.Data.Repositories;

public interface ISupplyRepository
{
    List<Supply> GetAll();
    List<Supply> Search(string query);
    Supply? GetById(int id);
    void Add(Supply supply);
    void Save();
}
