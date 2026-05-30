using Microsoft.EntityFrameworkCore;
using NLog;
using WarehouseApp.Data;

namespace WarehouseApp.Services;

public enum HeatmapMode
{
    ShelfLife,

    Movement
}

public enum MovementLevel { None, Low, Medium, High }

public class CellInfo
{
    public int CellId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string RowLabel { get; set; } = string.Empty;
    public int ColIndex { get; set; }

    public bool IsEmpty => ProductId == null || Stock <= 0;
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Stock { get; set; }

    public int? ShelfLifeDays { get; set; }

    public int MovementUnits { get; set; }
    public MovementLevel Movement { get; set; } = MovementLevel.None;
}

public interface IHeatmapService
{
    List<CellInfo> GetCells(int movementPeriodDays = 30);
}

public class HeatmapService : IHeatmapService
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private readonly AppDbContext _ctx;

    public HeatmapService(AppDbContext ctx) => _ctx = ctx;

    public List<CellInfo> GetCells(int movementPeriodDays = 30)
    {
        EnsureStockedProductsPlaced();

        var cells = _ctx.WarehouseCells
            .Include(c => c.Product).ThenInclude(p => p!.Batches)
            .AsNoTracking()
            .OrderBy(c => c.RowLabel).ThenBy(c => c.ColIndex)
            .ToList();

        var since = DateTime.Now.AddDays(-movementPeriodDays);

        var movement = _ctx.ShipmentItems
            .Include(i => i.Shipment)
            .AsNoTracking()
            .Where(i => i.Shipment != null && i.Shipment.ShippedAt >= since)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(x => x.Quantity) })
            .ToDictionary(x => x.ProductId, x => x.Units);

        var today = DateTime.Today;
        var result = new CellInfo[cells.Count];

        Parallel.For(0, cells.Count, i =>
        {
            var c = cells[i];
            var info = new CellInfo
            {
                CellId = c.Id,
                Code = c.Code,
                RowLabel = c.RowLabel,
                ColIndex = c.ColIndex,
                ProductId = c.ProductId
            };

            if (c.Product != null)
            {
                info.ProductName = c.Product.Name;
                info.Stock = c.Product.StockQuantity;

                var liveBatches = c.Product.Batches?
                    .Where(b => b.Quantity > 0 && b.SaleDeadline.HasValue)
                    .ToList();
                if (liveBatches != null && liveBatches.Count > 0)
                    info.ShelfLifeDays = liveBatches
                        .Min(b => (int)Math.Floor((b.SaleDeadline!.Value.Date - today).TotalDays));

                int units = movement.TryGetValue(c.ProductId!.Value, out var u) ? u : 0;
                info.MovementUnits = units;
                info.Movement = info.Stock <= 0 ? MovementLevel.None : ClassifyMovement(units);
            }

            result[i] = info;
        });

        logger.Debug("Тепловая карта: {Count} ячеек, период движения {Days} дн.", result.Length, movementPeriodDays);
        return result.ToList();
    }

    /// <summary>Размещает товары с остатком, не привязанные к ячейкам, в свободные
    /// ячейки склада — чтобы они появлялись на тепловой карте.</summary>
    private void EnsureStockedProductsPlaced()
    {
        var placed = _ctx.WarehouseCells
            .Where(c => c.ProductId != null)
            .Select(c => c.ProductId!.Value)
            .ToHashSet();
        var unplaced = _ctx.Products
            .Where(p => p.StockQuantity > 0)
            .Select(p => p.Id)
            .ToList()
            .Where(id => !placed.Contains(id))
            .ToList();
        if (unplaced.Count == 0) return;

        var free = _ctx.WarehouseCells
            .Where(c => c.ProductId == null)
            .OrderBy(c => c.RowLabel).ThenBy(c => c.ColIndex)
            .ToList();

        int fi = 0;
        bool changed = false;
        foreach (var id in unplaced)
        {
            if (fi >= free.Count) break;
            free[fi].ProductId = id;
            fi++;
            changed = true;
        }

        if (changed) _ctx.SaveChanges();
    }

    private static MovementLevel ClassifyMovement(int units)
    {
        if (units <= 0) return MovementLevel.Low;
        if (units < 10) return MovementLevel.Low;
        if (units <= 30) return MovementLevel.Medium;
        return MovementLevel.High;
    }
}
