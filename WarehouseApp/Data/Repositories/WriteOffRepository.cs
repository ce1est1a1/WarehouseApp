using Microsoft.EntityFrameworkCore;
using WarehouseApp.Models;

namespace WarehouseApp.Data.Repositories;

public class WriteOffRepository : IWriteOffRepository
{
    private readonly AppDbContext _db;

    public WriteOffRepository(AppDbContext db) => _db = db;

    public List<WriteOff> GetAll() =>
        _db.WriteOffs.Include(w => w.Product).OrderByDescending(w => w.WrittenOffAt).ToList();

    public List<WriteOff> GetByPeriod(DateTime from, DateTime to) =>
        _db.WriteOffs
            .Include(w => w.Product)
            .Where(w => w.WrittenOffAt.Date >= from.Date && w.WrittenOffAt.Date <= to.Date)
            .OrderByDescending(w => w.WrittenOffAt)
            .ToList();
}
