using WarehouseApp.Models;

namespace WarehouseApp.Data.Repositories;

public interface IProductRepository
{
    List<Product> GetAll();
    List<Product> GetByCategory(int categoryId);
    List<Product> Search(string query);
    Product? GetById(int id);
    void Add(Product product);
    void Update(Product product);
    void Delete(Product product);
    void Save();
}
