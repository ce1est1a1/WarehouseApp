using WarehouseApp.Models;

namespace WarehouseApp.Data.Repositories;

public interface ICategoryRepository
{
    List<Category> GetAll();
    Category? GetById(int id);
    Category? GetByName(string name);
    void Add(Category category);
    void Update(Category category);
    void Delete(Category category);
    void Save();
}
