using FluentAssertions;
using WarehouseApp.Models;
using WarehouseApp.Services;
using WarehouseApp.Tests.Infrastructure;
using Xunit;

namespace WarehouseApp.Tests.Unit;

public sealed class WeatherServiceTests
{
    [Fact]
    public void GetForecast_WhenCoordinatesAndCityCannotBeResolved_ReturnsValidationErrorWithoutExternalApiCall()
    {
        var geo = new FakeGeoService(geocodeResult: null, extractedCity: null);
        var settings = new AppSettings();
        var sut = new WeatherService(geo, settings);
        var logistics = new ShipmentLogistics();

        var result = sut.GetForecast(logistics, lat: null, lon: null, cityLabel: "Неизвестный адрес", date: DateTime.Today.AddDays(2));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Не удалось определить регион получателя");
        logistics.WeatherLoaded.Should().BeFalse();
        logistics.WeatherError.Should().Contain("Не удалось определить регион получателя");
    }
}
