using System.Text.RegularExpressions;
using NLog;
using WarehouseApp.Data.Repositories;
using WarehouseApp.Models;

namespace WarehouseApp.Services;

public interface IShipmentService
{
    List<Shipment> GetAll();
    List<Shipment> Search(string query);
    Shipment? GetById(int id);
    OperationResult<Shipment> CreateShipment(string name, string recipient, string address,
        List<(int ProductId, decimal Price, int Quantity)> items, int userId,
        string? logisticsJson = null);
}

public class ShipmentService : IShipmentService
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly IShipmentRepository _shipRepo;
    private readonly IProductRepository _prodRepo;
    private readonly ICurrencyService? _currencyService;

    public ShipmentService(IShipmentRepository shipRepo, IProductRepository prodRepo,
        ICurrencyService? currencyService = null)
    {
        _shipRepo = shipRepo;
        _prodRepo = prodRepo;
        _currencyService = currencyService;
    }

    public List<Shipment> GetAll() => _shipRepo.GetAll();
    public List<Shipment> Search(string query) => _shipRepo.Search(query);
    public Shipment? GetById(int id) => _shipRepo.GetById(id);

    public OperationResult<Shipment> CreateShipment(string name, string recipient, string address,
        List<(int ProductId, decimal Price, int Quantity)> items, int userId,
        string? logisticsJson = null)
    {
        logger.Debug("Оформление отгрузки: название='{Name}', получатель='{Recipient}', позиций={Count}",
            name, recipient, items?.Count ?? 0);

        if (string.IsNullOrWhiteSpace(name))
            return OperationResult<Shipment>.Fail("Введите название отгрузки.");
        if (string.IsNullOrWhiteSpace(recipient))
            return OperationResult<Shipment>.Fail("Поле «Получатель» обязательно для заполнения.");
        if (string.IsNullOrWhiteSpace(address))
            return OperationResult<Shipment>.Fail("Поле «Адрес» обязательно для заполнения.");
        if (items == null || items.Count == 0)
            return OperationResult<Shipment>.Fail("Добавьте хотя бы один товар в отгрузку.");

        foreach (var (productId, price, quantity) in items)
        {
            if (quantity <= 0)
                return OperationResult<Shipment>.Fail("Количество товара должно быть больше нуля.");
            if (price < 0)
                return OperationResult<Shipment>.Fail("Цена товара не может быть отрицательной.");

            var product = _prodRepo.GetById(productId);
            if (product == null)
                return OperationResult<Shipment>.Fail("Товар не найден в базе данных.");
            if (product.StockQuantity < quantity)
            {
                logger.Warn("Оформление отгрузки отклонено: недостаточно товара '{Name}' (запрошено {Req}, на складе {Stock})",
                    product.Name, quantity, product.StockQuantity);
                return OperationResult<Shipment>.Fail(
                    $"Недостаточно товара \"{product.Name}\" на складе.\n" +
                    $"Запрошено: {quantity} {product.Unit}, на складе: {product.StockQuantity} {product.Unit}");
            }
        }

        decimal totalCost = items.Sum(i => i.Price * i.Quantity);

        var shipment = new Shipment
        {
            Name = NormalizeShipmentName(name.Trim()),
            Recipient = recipient.Trim(),
            Address = address.Trim(),
            TotalCost = totalCost,
            CreatedByUserId = userId,
            LogisticsJson = logisticsJson
        };

        if (_currencyService != null)
        {
            shipment.UsdRate = _currencyService.Settings.UsdRate;
            shipment.EurRate = _currencyService.Settings.EurRate;
            shipment.UsdtRate = _currencyService.Settings.UsdtRate;
        }

        try
        {
            foreach (var (productId, price, quantity) in items)
            {
                var product = _prodRepo.GetById(productId)!;

                decimal totalPurchaseCost = DeductFromBatchesFifo(product, quantity);
                decimal avgPurchaseCost = quantity > 0 ? totalPurchaseCost / quantity : product.PurchasePrice;

                logger.Trace("FIFO по товару '{Name}': списано {Qty} ед., итого себестоимость {Cost}, средняя {Avg}",
                    product.Name, quantity, totalPurchaseCost, avgPurchaseCost);

                shipment.Items.Add(new ShipmentItem
                {
                    ProductId = productId,
                    Price = price,
                    Quantity = quantity,
                    PurchaseCost = avgPurchaseCost
                });

                product.StockQuantity -= quantity;
                _prodRepo.Update(product);
            }

            _shipRepo.Add(shipment);
            _shipRepo.Save();
            _prodRepo.Save();

            logger.Info("Оформлена отгрузка '{Name}' (получатель '{Recipient}', сумма {Total}, позиций {Count}, пользователь id={UserId})",
                shipment.Name, shipment.Recipient, shipment.TotalCost, shipment.Items.Count, userId);

            return OperationResult<Shipment>.Ok(shipment, "Отгрузка успешно оформлена.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Ошибка при оформлении отгрузки '{Name}'", name);
            throw;
        }
    }

    private static decimal DeductFromBatchesFifo(Product product, int quantity)
    {
        var batches = product.Batches?
            .Where(b => b.Quantity > 0 && !b.IsOverdue)
            .OrderBy(b => b.SaleDeadline ?? DateTime.MaxValue)
            .ThenBy(b => b.ReceivedAt)
            .ToList();

        if (batches == null || batches.Count == 0)
            return product.PurchasePrice * quantity;

        decimal totalCost = 0;
        int remaining = quantity;

        foreach (var batch in batches)
        {
            if (remaining <= 0) break;
            int take = Math.Min(remaining, batch.Quantity);
            batch.Quantity -= take;
            totalCost += batch.PurchasePrice * take;
            remaining -= take;
        }

        if (remaining > 0)
        {
            logger.Warn("FIFO: партий товара '{Name}' не хватило, {Remaining} ед. списано по каталожной цене",
                product.Name, remaining);
            totalCost += product.PurchasePrice * remaining;
        }

        return totalCost;
    }

    private string NormalizeShipmentName(string name)
    {
        var match = Regex.Match(name, @"^Отгрузка\s+(\d+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
            return name;

        int maxNumber = 0;
        foreach (var shipment in _shipRepo.GetAll())
        {
            var m = Regex.Match(shipment.Name ?? string.Empty, @"^Отгрузка\s+(\d+)$", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var number) && number > maxNumber)
                maxNumber = number;
        }

        return $"Отгрузка {maxNumber + 1}";
    }
}
