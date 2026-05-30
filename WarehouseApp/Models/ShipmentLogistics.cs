using System.Text.Json;
using System.Text.Json.Serialization;

namespace WarehouseApp.Models;

public enum CheckOutcome
{
    Clean,

    Risk,

    Error
}

public enum DealDecision
{
    Allowed,

    NeedsReview,

    NotChecked
}

public enum WeatherRisk
{
    Low,
    Medium,
    High,
    None
}

public class ShipmentLogistics
{
    public string CounterpartyType { get; set; } = "Покупатель";
    public string CompanyName { get; set; } = string.Empty;
    public string Inn { get; set; } = string.Empty;

    public CheckOutcome TaxOutcome { get; set; } = CheckOutcome.Clean;
    public CheckOutcome BankruptcyOutcome { get; set; } = CheckOutcome.Clean;
    public CheckOutcome DirectorOutcome { get; set; } = CheckOutcome.Clean;

    public string TaxNote { get; set; } = string.Empty;
    public string BankruptcyNote { get; set; } = string.Empty;
    public string DirectorNote { get; set; } = string.Empty;

    public DealDecision Decision { get; set; } = DealDecision.NotChecked;
    public DateTime? CheckedAt { get; set; }
    public bool CheckPerformed { get; set; }

    public bool WeatherLoaded { get; set; }
    public string WeatherCity { get; set; } = string.Empty;
    public DateTime? ForecastDate { get; set; }
    public double Temperature { get; set; }
    public double FeelsLike { get; set; }
    public int Humidity { get; set; }
    public double WindSpeed { get; set; }
    public string WindDirection { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public WeatherRisk Risk { get; set; } = WeatherRisk.Low;
    public string WeatherError { get; set; } = string.Empty;

    public string RouteFrom { get; set; } = string.Empty;
    public string RouteTo { get; set; } = string.Empty;
    public int DistanceKm { get; set; }
    public string RouteError { get; set; } = string.Empty;

    public double DestLat { get; set; }
    public double DestLon { get; set; }
    public bool HasCoords { get; set; }

    public string ThermoRecommendation { get; set; } = string.Empty;
    public string PackagingRecommendation { get; set; } = string.Empty;
    public string InsuranceRecommendation { get; set; } = string.Empty;
    public bool RecommendationsApplied { get; set; }

    [JsonIgnore]
    public string DecisionText => Decision switch
    {
        DealDecision.Allowed => "можно продолжить сделку",
        DealDecision.NeedsReview => "требуется решение администратора",
        _ => "проверка не выполнялась"
    };

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = false };

    public string ToJson() => JsonSerializer.Serialize(this, Opts);

    public static ShipmentLogistics? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ShipmentLogistics>(json); }
        catch { return null; }
    }
}

public static class CheckOutcomeExtensions
{
    public static string DisplayText(this CheckOutcome o) => o switch
    {
        CheckOutcome.Clean => "не найдено",
        CheckOutcome.Risk => "найден риск",
        _ => "ошибка проверки"
    };
}
