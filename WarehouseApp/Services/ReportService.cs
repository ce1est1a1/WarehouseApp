using System.Text;
using NLog;
using WarehouseApp.Data.Repositories;
using WarehouseApp.Models;

namespace WarehouseApp.Services;

public interface IReportService
{
    List<Shipment> GetShipmentsByPeriod(DateTime from, DateTime to);
    List<WriteOff> GetWriteOffsByPeriod(DateTime from, DateTime to);
    string ExportToCsv(List<Shipment> shipments);
    string ExportWriteOffsToCsv(List<WriteOff> writeOffs);
}

public class ReportService : IReportService
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly IShipmentRepository _shipRepo;
    private readonly IWriteOffRepository _writeOffRepo;
    private readonly ICurrencyService? _currencyService;

    public ReportService(IShipmentRepository shipRepo, IWriteOffRepository writeOffRepo, ICurrencyService? currencyService = null)
    {
        _shipRepo = shipRepo;
        _writeOffRepo = writeOffRepo;
        _currencyService = currencyService;
    }

    public List<Shipment> GetShipmentsByPeriod(DateTime from, DateTime to)
    {
        logger.Debug("Построение отчёта по отгрузкам за период {From:d} - {To:d}", from, to);

        var result = _shipRepo.GetAll()
            .Where(s => s.ShippedAt.Date >= from.Date && s.ShippedAt.Date <= to.Date)
            .OrderByDescending(s => s.ShippedAt)
            .ToList();

        logger.Info("Отчёт за период {From:d} - {To:d}: найдено {Count} отгрузок",
            from, to, result.Count);
        return result;
    }

    public List<WriteOff> GetWriteOffsByPeriod(DateTime from, DateTime to)
    {
        logger.Debug("Построение отчёта по списаниям за период {From:d} - {To:d}", from, to);
        var result = _writeOffRepo.GetByPeriod(from, to);
        logger.Info("Отчёт по списаниям {From:d} - {To:d}: найдено {Count} записей", from, to, result.Count);
        return result;
    }

    public string ExportWriteOffsToCsv(List<WriteOff> writeOffs)
    {
        logger.Info("Экспорт списаний в CSV ({Count} записей)", writeOffs.Count);
        var sb = new StringBuilder();
        sb.AppendLine("Дата;Товар;Количество;Закупочная цена;Общий убыток;Причина");
        foreach (var w in writeOffs)
        {
            sb.AppendLine($"{w.WrittenOffAt:dd.MM.yyyy};{Escape(w.Product?.Name ?? "—")};{w.Quantity};{w.PurchasePrice:N2};{w.TotalLoss:N2};{Escape(w.Reason)}");
        }
        return sb.ToString();
    }

    public string ExportToCsv(List<Shipment> shipments)
    {
        logger.Info("Экспорт отчёта в CSV ({Count} отгрузок)", shipments.Count);

        var settings = _currencyService?.Settings;
        string currency = settings?.Currency ?? "RUB";
        string currencyLabel = currency == "RUB" ? "₽" : currency;

        var sb = new StringBuilder();
        sb.AppendLine($"Дата;Покупатель;Адрес;Сумма отгрузки ({currencyLabel});Себестоимость ({currencyLabel});Прибыль ({currencyLabel})");
        foreach (var s in shipments)
        {
            decimal sum = ConvertRubToCurrent(s.TotalCost, s, settings);
            decimal cost = ConvertRubToCurrent(s.TotalPurchaseCost, s, settings);
            decimal profit = ConvertRubToCurrent(s.Profit, s, settings);

            sb.AppendLine($"{s.ShippedAt:dd.MM.yyyy};" +
                          $"{Escape(s.Recipient)};" +
                          $"{Escape(s.Address)};" +
                          $"{sum:N2};" +
                          $"{cost:N2};" +
                          $"{profit:N2}");
        }
        return sb.ToString();
    }

    private static decimal ConvertRubToCurrent(decimal rub, Shipment s, AppSettings? settings)
    {
        if (settings == null || settings.Currency == "RUB") return rub;
        decimal rate = s.GetStoredRate(settings.Currency) ?? settings.GetRate(settings.Currency);
        if (rate <= 0) return rub;
        return Math.Round(rub / rate, 2);
    }

    private static string Escape(string val)
    {
        if (val.Contains(';') || val.Contains('"'))
            return $"\"{val.Replace("\"", "\"\"")}\"";
        return val;
    }
}
