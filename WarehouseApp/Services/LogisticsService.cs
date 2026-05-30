using WarehouseApp.Models;

namespace WarehouseApp.Services;

public interface ILogisticsService
{
    void BuildRecommendations(ShipmentLogistics logistics, IEnumerable<string> productNames, decimal totalCost);
}

public class LogisticsService : ILogisticsService
{
    public void BuildRecommendations(ShipmentLogistics logistics, IEnumerable<string> productNames, decimal totalCost)
    {
        bool highWeather = logistics.WeatherLoaded && logistics.Risk == WeatherRisk.High;

        logistics.ThermoRecommendation = highWeather ? "Рекомендуется" : "Не требуется";
        logistics.PackagingRecommendation = "Стандартная упаковка";
        logistics.InsuranceRecommendation = highWeather ? "Рекомендуется" : "Не требуется";
    }
}
