using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using WarehouseApp;
using WarehouseApp.Data;
using WarehouseApp.Data.Repositories;
using WarehouseApp.Models;
using WarehouseApp.Services;
using WarehouseApp.Tests.Infrastructure;
using Xunit;

namespace WarehouseApp.Tests.Smoke;

/// <summary>
/// Смоук-тесты: быстрая проверка, что ключевые узлы приложения поднимаются
/// и сквозной сценарий «поставка → карта → отгрузка → отчёт» работает.
/// </summary>
public sealed class SmokeTests
{
    [Fact]
    public void Smoke_AppServices_BootsIocContainerAndResolvesAllServices()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"wh_smoke_{Guid.NewGuid():N}.db");
        try
        {
            var app = new AppServices(dbPath);

            app.DbContext.Should().NotBeNull();

            foreach (var prop in typeof(AppServices).GetProperties().Where(p => p.PropertyType.IsInterface))
                prop.GetValue(app).Should().NotBeNull($"сервис {prop.Name} должен быть разрешён IoC-контейнером");
        }
        finally
        {
            try { File.Delete(dbPath); } catch { /* временный файл */ }
        }
    }

    [Fact]
    public void Smoke_SupplyHeatmapShipmentReport_HappyPath()
    {
        using var db = new TestDb();
        var ctx = db.Context;

        var user = AddUser(ctx);
        var product = new Product { Article = "SM-1", Name = "Молоко", Unit = "шт.", PurchasePrice = 50m, StockQuantity = 0 };
        ctx.Products.Add(product);
        ctx.WarehouseCells.Add(new WarehouseCell { Code = "A-01", RowLabel = "A", ColIndex = 1 });
        ctx.SaveChanges();

        var prodRepo = new ProductRepository(ctx);
        var supplyRepo = new SupplyRepository(ctx);
        var shipRepo = new ShipmentRepository(ctx);
        var writeOffRepo = new WriteOffRepository(ctx);

        // 1. Поставка увеличивает остаток, создаёт партию и размещает товар на карте
        var supply = new SupplyService(supplyRepo, prodRepo, currencyService: null, ctx: ctx);
        var supplyResult = supply.CreateSupply("Поставка 1", "Поставщик", DateTime.Today,
            new List<(int, decimal, int, DateTime?)> { (product.Id, 50m, 10, DateTime.Today.AddDays(120)) },
            user.Id);
        supplyResult.Success.Should().BeTrue();

        ctx.ChangeTracker.Clear();
        prodRepo.GetById(product.Id)!.StockQuantity.Should().Be(10);

        // 2. Тепловая карта показывает товар в ячейке
        var heatmap = new HeatmapService(ctx).GetCells();
        heatmap.Should().Contain(c => c.Code == "A-01" && c.ProductName == "Молоко" && c.Stock == 10 && !c.IsEmpty);

        // 3. Отгрузка списывает остаток
        ctx.ChangeTracker.Clear();
        var shipment = new ShipmentService(shipRepo, prodRepo, currencyService: null);
        var shipResult = shipment.CreateShipment("Отгрузка 1", "Покупатель", "г. Москва, Тверская, 1",
            new List<(int, decimal, int)> { (product.Id, 80m, 3) }, user.Id);
        shipResult.Success.Should().BeTrue();

        ctx.ChangeTracker.Clear();
        prodRepo.GetById(product.Id)!.StockQuantity.Should().Be(7);

        // 4. Отчёт за период содержит отгрузку
        var report = new ReportService(shipRepo, writeOffRepo, currencyService: null);
        report.GetShipmentsByPeriod(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1))
            .Should().Contain(s => s.Name == "Отгрузка 1");
    }

    [Fact]
    public void Smoke_CounterpartyCheckAndInsuranceRecommendation()
    {
        var counterparty = new CounterpartyService();

        var clean = new ShipmentLogistics { Inn = "7712345678" };
        counterparty.Check(clean).Success.Should().BeTrue();
        clean.Decision.Should().Be(DealDecision.Allowed);

        var risky = new ShipmentLogistics { Inn = "7700000001" };
        counterparty.Check(risky);
        risky.Decision.Should().Be(DealDecision.NeedsReview);

        var logistics = new LogisticsService();

        var hot = new ShipmentLogistics { WeatherLoaded = true, Risk = WeatherRisk.High };
        logistics.BuildRecommendations(hot, Array.Empty<string>(), totalCost: 1_000m);
        hot.InsuranceRecommendation.Should().Be("Рекомендуется");

        var mild = new ShipmentLogistics { WeatherLoaded = true, Risk = WeatherRisk.Low };
        logistics.BuildRecommendations(mild, Array.Empty<string>(), totalCost: 1_000m);
        mild.InsuranceRecommendation.Should().Be("Не требуется");
    }

    [Fact]
    public void Smoke_WeatherService_RegionNotResolved_ReturnsErrorWithoutCrash()
    {
        var weather = new WeatherService(new FakeGeoService(), new AppSettings());
        var logistics = new ShipmentLogistics();

        var result = weather.GetForecast(logistics, lat: null, lon: null, cityLabel: "???", date: DateTime.Today.AddDays(2));

        result.Success.Should().BeFalse();
        logistics.WeatherLoaded.Should().BeFalse();
    }

    private static User AddUser(AppDbContext ctx)
    {
        var user = new User { Login = Guid.NewGuid().ToString("N"), PasswordHash = "hash", Role = UserRole.Administrator };
        ctx.Users.Add(user);
        ctx.SaveChanges();
        return user;
    }
}
