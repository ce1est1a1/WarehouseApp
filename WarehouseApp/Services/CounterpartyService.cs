using NLog;
using WarehouseApp.Models;

namespace WarehouseApp.Services;

public interface ICounterpartyService
{
    bool ValidateInn(string inn, out string error);

    OperationResult<ShipmentLogistics> Check(ShipmentLogistics logistics);
}

public class CounterpartyService : ICounterpartyService
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private static readonly Dictionary<string, (CheckOutcome tax, CheckOutcome bank, CheckOutcome dir, string note)> KnownRisks = new()
    {
        ["7700000001"] = (CheckOutcome.Risk, CheckOutcome.Clean, CheckOutcome.Clean, "Задолженность по налогам свыше 1 млн ₽."),
        ["7700000002"] = (CheckOutcome.Clean, CheckOutcome.Risk, CheckOutcome.Clean, "Введена процедура банкротства (наблюдение)."),
        ["7700000003"] = (CheckOutcome.Clean, CheckOutcome.Clean, CheckOutcome.Risk, "Директор в реестре дисквалифицированных лиц."),
        ["0000000000"] = (CheckOutcome.Error, CheckOutcome.Error, CheckOutcome.Error, "Сервис реестра временно недоступен."),
    };

    public bool ValidateInn(string inn, out string error)
    {
        error = string.Empty;
        inn = (inn ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(inn))
        {
            error = "Введите ИНН контрагента.";
            return false;
        }
        if (!inn.All(char.IsDigit))
        {
            error = "ИНН должен содержать только цифры.";
            return false;
        }
        if (inn.Length != 10 && inn.Length != 12)
        {
            error = "ИНН должен содержать 10 (юр. лицо) или 12 (ИП) цифр.";
            return false;
        }
        return true;
    }

    public OperationResult<ShipmentLogistics> Check(ShipmentLogistics logistics)
    {
        var inn = (logistics.Inn ?? string.Empty).Trim();
        logger.Debug("Проверка контрагента по ИНН {Inn}", inn);

        if (!ValidateInn(inn, out var error))
            return OperationResult<ShipmentLogistics>.Fail(error);

        try
        {
            if (KnownRisks.TryGetValue(inn, out var known))
            {
                logistics.TaxOutcome = known.tax;
                logistics.BankruptcyOutcome = known.bank;
                logistics.DirectorOutcome = known.dir;
                logistics.TaxNote = OutcomeNote(known.tax, "налоговым задолженностям", known.note);
                logistics.BankruptcyNote = OutcomeNote(known.bank, "банкротству", known.note);
                logistics.DirectorNote = OutcomeNote(known.dir, "дисквалификации директора", known.note);
            }
            else
            {
                logistics.TaxOutcome = CheckOutcome.Clean;
                logistics.BankruptcyOutcome = CheckOutcome.Clean;
                logistics.DirectorOutcome = CheckOutcome.Clean;
                logistics.TaxNote = "Задолженности по налогам и сборам не обнаружены.";
                logistics.BankruptcyNote = "Сведения о банкротстве отсутствуют.";
                logistics.DirectorNote = "Дисквалифицированные лица не найдены.";
            }

            logistics.CheckPerformed = true;
            logistics.CheckedAt = DateTime.Now;
            logistics.Decision = ComputeDecision(logistics);

            logger.Info("Контрагент ИНН {Inn}: решение {Decision}", inn, logistics.Decision);
            return OperationResult<ShipmentLogistics>.Ok(logistics, "Проверка контрагента выполнена.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Ошибка проверки контрагента ИНН {Inn}", inn);
            logistics.TaxOutcome = logistics.BankruptcyOutcome = logistics.DirectorOutcome = CheckOutcome.Error;
            logistics.Decision = DealDecision.NeedsReview;
            return OperationResult<ShipmentLogistics>.Fail("Не удалось выполнить проверку. Повторите попытку позже.");
        }
    }

    private static string OutcomeNote(CheckOutcome outcome, string subject, string riskNote) => outcome switch
    {
        CheckOutcome.Risk => riskNote,
        CheckOutcome.Error => "Не удалось получить данные по " + subject + ". Повторите проверку.",
        _ => "По " + subject + " замечаний нет."
    };

    public static DealDecision ComputeDecision(ShipmentLogistics l)
    {
        if (!l.CheckPerformed) return DealDecision.NotChecked;
        bool anyRisk = l.TaxOutcome == CheckOutcome.Risk
            || l.BankruptcyOutcome == CheckOutcome.Risk
            || l.DirectorOutcome == CheckOutcome.Risk;
        return anyRisk ? DealDecision.NeedsReview : DealDecision.Allowed;
    }
}
