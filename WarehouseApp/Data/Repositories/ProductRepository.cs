using Microsoft.EntityFrameworkCore;
using WarehouseApp.Models;

namespace WarehouseApp.Data.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _ctx;
    public ProductRepository(AppDbContext ctx) => _ctx = ctx;

    public List<Product> GetAll() =>
        _ctx.Products.Include(p => p.Category).Include(p => p.Batches).AsNoTracking()
            .OrderBy(p => p.Name).ToList();

    public List<Product> GetByCategory(int categoryId) =>
        _ctx.Products.Include(p => p.Category).Include(p => p.Batches).AsNoTracking()
            .Where(p => p.CategoryId == categoryId)
            .OrderBy(p => p.Name).ToList();

    public List<Product> Search(string query) =>
        _ctx.Products.Include(p => p.Category).Include(p => p.Batches).AsNoTracking()
            .Where(p => p.Name.Contains(query)
                     || p.Article.Contains(query)
                     || (p.Description != null && p.Description.Contains(query)))
            .OrderBy(p => p.Name).ToList();

    public Product? GetById(int id) =>
        _ctx.Products.Include(p => p.Category).Include(p => p.Batches).FirstOrDefault(p => p.Id == id);

    public void Add(Product product) => _ctx.Products.Add(product);

    public void Update(Product product)
    {
        var tracked = _ctx.Products.Local.FirstOrDefault(p => p.Id == product.Id);
        if (tracked != null)
        {
            _ctx.Entry(tracked).CurrentValues.SetValues(product);

            _ctx.Entry(tracked).Property(p => p.CategoryId).CurrentValue = product.CategoryId;
        }
        else
        {
            _ctx.Products.Attach(product);
            _ctx.Entry(product).State = EntityState.Modified;
        }
    }

    public void Delete(Product product) => _ctx.Products.Remove(product);

    public void Save() => _ctx.SaveChanges();
}
