using NLog;
using WarehouseApp.Data;
using WarehouseApp.Data.Repositories;
using WarehouseApp.Models;

namespace WarehouseApp.Services;

public interface ISupplyService
{
    List<Supply> GetAll();
    List<Supply> Search(string query);
    Supply? GetById(int id);

    OperationResult<Supply> CreateSupply(string name, string supplier, DateTime supplyDate,
        List<(int ProductId, decimal PurchasePrice, int Quantity, DateTime? SaleDeadline)> items, int userId,
        string? checkJson = null);
}

public class SupplyService : ISupplyService
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly ISupplyRepository _supplyRepo;
    private readonly IProductRepository _prodRepo;
    private readonly ICurrencyService? _currencyService;
    private readonly AppDbContext? _ctx;

    public SupplyService(ISupplyRepository supplyRepo, IProductRepository prodRepo,
        ICurrencyService? currencyService = null, AppDbContext? ctx = null)
    {
        _supplyRepo = supplyRepo;
        _prodRepo = prodRepo;
        _currencyService = currencyService;
        _ctx = ctx;
    }

    public List<Supply> GetAll() => _supplyRepo.GetAll();
    public List<Supply> Search(string query) => _supplyRepo.Search(query);
    public Supply? GetById(int id) => _supplyRepo.GetById(id);

    public OperationResult<Supply> CreateSupply(string name, string supplier, DateTime supplyDate,
        List<(int ProductId, decimal PurchasePrice, int Quantity, DateTime? SaleDeadline)> items, int userId,
        string? checkJson = null)
    {
        logger.Debug("Регистрация поставки: название='{Name}', поставщик='{Supplier}', дата={Date}, позиций={Count}",
            name, supplier, supplyDate, items?.Count ?? 0);

        if (string.IsNullOrWhiteSpace(name))
            return OperationResult<Supply>.Fail("Введите название поставки.");
        if (items == null || items.Count == 0)
            return OperationResult<Supply>.Fail("Добавьте хотя бы одну позицию в поставку.");

        foreach (var (productId, purchasePrice, quantity, saleDeadline) in items)
        {
            if (quantity <= 0)
                return OperationResult<Supply>.Fail("Количество товара должно быть больше нуля.");
            if (purchasePrice < 0)
                return OperationResult<Supply>.Fail("Закупочная цена не может быть отрицательной.");

            var product = _prodRepo.GetById(productId);
            if (product == null)
            {
                logger.Warn("Регистрация поставки отклонена: товар id={Id} не найден", productId);
                return OperationResult<Supply>.Fail($"Товар с ID={productId} не найден в базе данных.");
            }

            if (saleDeadline.HasValue && saleDeadline.Value.Date <= supplyDate.Date)
            {
                logger.Warn("Регистрация поставки отклонена: срок реализации товара '{Name}' истекает не позже даты поставки ({Date:d})",
                    product.Name, saleDeadline.Value);
                return OperationResult<Supply>.Fail(
                    $"Срок реализации товара \"{product.Name}\" должен быть позже даты поставки.");
            }
        }

        decimal totalCost = items.Sum(i => i.PurchasePrice * i.Quantity);

        var supply = new Supply
        {
            Name = name.Trim(),
            Supplier = (supplier ?? string.Empty).Trim(),
            TotalCost = totalCost,
            SuppliedAt = supplyDate,
            CreatedByUserId = userId,
            CheckJson = checkJson
        };

        if (_currencyService != null)
        {
            supply.UsdRate = _currencyService.Settings.UsdRate;
            supply.EurRate = _currencyService.Settings.EurRate;
            supply.UsdtRate = _currencyService.Settings.UsdtRate;
        }

        try
        {
            foreach (var (productId, purchasePrice, quantity, saleDeadline) in items)
            {
                supply.Items.Add(new SupplyItem
                {
                    ProductId = productId,
                    PurchasePrice = purchasePrice,
                    Quantity = quantity,
                    SaleDeadline = saleDeadline
                });

                var product = _prodRepo.GetById(productId)!;
                product.StockQuantity += quantity;

                product.Batches.Add(new ProductBatch
                {
                    ProductId = productId,
                    Quantity = quantity,
                    SaleDeadline = saleDeadline,
                    PurchasePrice = purchasePrice,
                    ReceivedAt = supplyDate
                });

                logger.Trace("Поставка '{Supply}': +{Qty} ед. товара '{Name}', цена {Price}, срок реализации до {Deadline}",
                    name, quantity, product.Name, purchasePrice, saleDeadline?.ToString("d") ?? "не указан");

                _prodRepo.Update(product);
            }

            _supplyRepo.Add(supply);
            _supplyRepo.Save();
            _prodRepo.Save();

            PlaceProductsInFreeCells(items.Select(i => i.ProductId).Distinct());

            logger.Info("Зарегистрирована поставка '{Name}' (поставщик '{Supplier}', сумма {Total}, позиций {Count}, пользователь id={UserId})",
                supply.Name, supply.Supplier, supply.TotalCost, supply.Items.Count, userId);

            return OperationResult<Supply>.Ok(supply, "Поставка успешно зарегистрирована.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Ошибка при регистрации поставки '{Name}'", name);
            throw;
        }
    }

    /// <summary>Размещает поставленные товары в свободных ячейках склада,
    /// чтобы они отображались на тепловой карте.</summary>
    private void PlaceProductsInFreeCells(IEnumerable<int> productIds)
    {
        if (_ctx == null) return;

        var placed = _ctx.WarehouseCells
            .Where(c => c.ProductId != null)
            .Select(c => c.ProductId!.Value)
            .ToHashSet();
        var free = _ctx.WarehouseCells
            .Where(c => c.ProductId == null)
            .OrderBy(c => c.RowLabel).ThenBy(c => c.ColIndex)
            .ToList();

        int fi = 0;
        bool changed = false;
        foreach (var pid in productIds)
        {
            if (placed.Contains(pid)) continue;
            if (fi >= free.Count) break;
            free[fi].ProductId = pid;
            placed.Add(pid);
            fi++;
            changed = true;
        }

        if (changed) _ctx.SaveChanges();
    }
}
