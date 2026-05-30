using Microsoft.EntityFrameworkCore;
using WarehouseApp.Models;

namespace WarehouseApp.Data.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _ctx;
    public CategoryRepository(AppDbContext ctx) => _ctx = ctx;

    public List<Category> GetAll() =>
        _ctx.Categories.AsNoTracking().OrderBy(c => c.Name).ToList();

    public Category? GetById(int id) =>
        _ctx.Categories.FirstOrDefault(c => c.Id == id);

    public Category? GetByName(string name) =>
        _ctx.Categories.AsNoTracking().FirstOrDefault(c => c.Name == name);

    public void Add(Category category) => _ctx.Categories.Add(category);

    public void Update(Category category) => _ctx.Categories.Update(category);

    public void Delete(Category category) => _ctx.Categories.Remove(category);

    public void Save() => _ctx.SaveChanges();
}
