using FluentAssertions;
using WarehouseApp.Models;
using WarehouseApp.Services;
using Xunit;

namespace WarehouseApp.Tests.Unit;

public sealed class LogisticsServiceTests
{
    private readonly LogisticsService _sut = new();

    [Fact]
    public void BuildRecommendations_HighWeatherRisk_RecommendsThermoContainerAndInsurance()
    {
        var logistics = new ShipmentLogistics
        {
            WeatherLoaded = true,
            Risk = WeatherRisk.High,
            DistanceKm = 100,
            ForecastDate = DateTime.Today.AddDays(2),
            RouteFrom = "Москва",
            RouteTo = "Санкт-Петербург"
        };

        _sut.BuildRecommendations(logistics, productNames: Array.Empty<string>(), totalCost: 1_000m);

        logistics.ThermoRecommendation.Should().Be("Рекомендуется");
        logistics.PackagingRecommendation.Should().Be("Стандартная упаковка");
        logistics.InsuranceRecommendation.Should().Be("Рекомендуется");
        logistics.ForecastDate!.Value.Date.Should().Be(DateTime.Today.AddDays(2));
    }

    [Fact]
    public void BuildRecommendations_LowWeatherRisk_DoesNotRequireExtraProtection()
    {
        var logistics = new ShipmentLogistics
        {
            WeatherLoaded = true,
            Risk = WeatherRisk.Low,
            DistanceKm = 100,
            ForecastDate = DateTime.Today.AddDays(2)
        };

        _sut.BuildRecommendations(logistics, productNames: Array.Empty<string>(), totalCost: 1_000m);

        logistics.ThermoRecommendation.Should().Be("Не требуется");
        logistics.PackagingRecommendation.Should().Be("Стандартная упаковка");
        logistics.InsuranceRecommendation.Should().Be("Не требуется");
    }

    [Fact]
    public void BuildRecommendations_WeatherNotLoaded_UsesDefaultRecommendations()
    {
        var logistics = new ShipmentLogistics
        {
            WeatherLoaded = false,
            Risk = WeatherRisk.None,
            DistanceKm = 100,
            ForecastDate = null
        };

        _sut.BuildRecommendations(logistics, productNames: Array.Empty<string>(), totalCost: 1_000m);

        logistics.ThermoRecommendation.Should().Be("Не требуется");
        logistics.PackagingRecommendation.Should().Be("Стандартная упаковка");
        logistics.InsuranceRecommendation.Should().Be("Не требуется");
    }
}
