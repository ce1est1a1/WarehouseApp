using Microsoft.EntityFrameworkCore;
using WarehouseApp.Models;

namespace WarehouseApp.Data.Repositories;

public class SupplyRepository : ISupplyRepository
{
    private readonly AppDbContext _ctx;
    public SupplyRepository(AppDbContext ctx) => _ctx = ctx;

    public List<Supply> GetAll() =>
        _ctx.Supplies
            .Include(s => s.CreatedByUser)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .AsNoTracking()
            .OrderByDescending(s => s.SuppliedAt).ToList();

    public List<Supply> Search(string query) =>
        _ctx.Supplies
            .Include(s => s.CreatedByUser)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .AsNoTracking()
            .Where(s => s.Name.Contains(query) || s.Supplier.Contains(query))
            .OrderByDescending(s => s.SuppliedAt).ToList();

    public Supply? GetById(int id) =>
        _ctx.Supplies
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.CreatedByUser)
            .FirstOrDefault(s => s.Id == id);

    public void Add(Supply supply) => _ctx.Supplies.Add(supply);

    public void Save() => _ctx.SaveChanges();
}
