using Microsoft.EntityFrameworkCore;
using WarehouseApp.Models;

namespace WarehouseApp.Data.Repositories;

public class ShipmentRepository : IShipmentRepository
{
    private readonly AppDbContext _ctx;
    public ShipmentRepository(AppDbContext ctx) => _ctx = ctx;

    public List<Shipment> GetAll() =>
        _ctx.Shipments
            .Include(s => s.CreatedByUser)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .AsNoTracking()
            .OrderByDescending(s => s.ShippedAt).ToList();

    public List<Shipment> Search(string query) =>
        _ctx.Shipments
            .Include(s => s.CreatedByUser)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .AsNoTracking()
            .Where(s => s.Name.Contains(query) || s.Recipient.Contains(query))
            .OrderByDescending(s => s.ShippedAt).ToList();

    public Shipment? GetById(int id) =>
        _ctx.Shipments.Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.CreatedByUser)
            .FirstOrDefault(s => s.Id == id);

    public void Add(Shipment shipment) => _ctx.Shipments.Add(shipment);

    public void Save() => _ctx.SaveChanges();
}
