using FluentAssertions;
using WarehouseApp.Models;
using WarehouseApp.Services;
using WarehouseApp.Tests.Infrastructure;
using Xunit;

namespace WarehouseApp.Tests.Unit;

public sealed class HeatmapServiceTests
{
    [Fact]
    public void GetCells_EmptyWarehouseCell_IsEmpty()
    {
        using var db = new TestDb();
        db.Context.WarehouseCells.Add(new WarehouseCell { Code = "A-01", RowLabel = "A", ColIndex = 1 });
        db.Context.SaveChanges();

        var cells = new HeatmapService(db.Context).GetCells();

        cells.Should().ContainSingle();
        cells[0].IsEmpty.Should().BeTrue();
        cells[0].Movement.Should().Be(MovementLevel.None);
    }

    [Fact]
    public void GetCells_ProductWithPositiveStock_IsNotEmptyAndReturnsProductDetails()
    {
        using var db = new TestDb();
        var product = AddProduct(db, "A-100", "Йогурт", stock: 20);
        db.Context.WarehouseCells.Add(new WarehouseCell { Code = "A-01", RowLabel = "A", ColIndex = 1, ProductId = product.Id });
        db.Context.ProductBatches.Add(new ProductBatch
        {
            ProductId = product.Id,
            Quantity = 20,
            SaleDeadline = DateTime.Today.AddDays(5),
            PurchasePrice = 100m,
            ReceivedAt = DateTime.Today
        });
        db.Context.SaveChanges();

        var cell = new HeatmapService(db.Context).GetCells().Single();

        cell.IsEmpty.Should().BeFalse();
        cell.ProductName.Should().Be("Йогурт");
        cell.Stock.Should().Be(20);
        cell.ShelfLifeDays.Should().BeInRange(4, 5);
    }

    [Fact]
    public void GetCells_ShelfLifeUsesNearestActiveBatchAndIgnoresZeroQuantityBatches()
    {
        using var db = new TestDb();
        var product = AddProduct(db, "A-101", "Сыр", stock: 10);
        db.Context.WarehouseCells.Add(new WarehouseCell { Code = "A-01", RowLabel = "A", ColIndex = 1, ProductId = product.Id });
        db.Context.ProductBatches.AddRange(
            new ProductBatch
            {
                ProductId = product.Id,
                Quantity = 0,
                SaleDeadline = DateTime.Today.AddDays(1),
                PurchasePrice = 100m,
                ReceivedAt = DateTime.Today
            },
            new ProductBatch
            {
                ProductId = product.Id,
                Quantity = 5,
                SaleDeadline = DateTime.Today.AddDays(7),
                PurchasePrice = 100m,
                ReceivedAt = DateTime.Today
            },
            new ProductBatch
            {
                ProductId = product.Id,
                Quantity = 5,
                SaleDeadline = DateTime.Today.AddDays(30),
                PurchasePrice = 100m,
                ReceivedAt = DateTime.Today
            });
        db.Context.SaveChanges();

        var cell = new HeatmapService(db.Context).GetCells().Single();

        cell.ShelfLifeDays.Should().BeInRange(6, 7);
    }

    [Theory]
    [InlineData(0, MovementLevel.Low)]
    [InlineData(9, MovementLevel.Low)]
    [InlineData(10, MovementLevel.Medium)]
    [InlineData(30, MovementLevel.Medium)]
    [InlineData(31, MovementLevel.High)]
    public void GetCells_MovementUnitsWithinPeriod_ClassifiesMovement(int units, MovementLevel expected)
    {
        using var db = new TestDb();
        var user = AddUser(db);
        var product = AddProduct(db, "A-102", "Товар движения", stock: 50);
        db.Context.WarehouseCells.Add(new WarehouseCell { Code = "A-01", RowLabel = "A", ColIndex = 1, ProductId = product.Id });

        if (units > 0)
        {
            db.Context.Shipments.Add(new Shipment
            {
                Name = "Отгрузка",
                Recipient = "Покупатель",
                ShippedAt = DateTime.Now.AddDays(-3),
                CreatedByUserId = user.Id,
                Items = { new ShipmentItem { ProductId = product.Id, Quantity = units, Price = 100m, PurchaseCost = 50m } }
            });
        }

        db.Context.SaveChanges();

        var cell = new HeatmapService(db.Context).GetCells().Single();

        cell.MovementUnits.Should().Be(units);
        cell.Movement.Should().Be(expected);
    }

    [Fact]
    public void GetCells_OldShipmentsOutsidePeriod_AreNotIncludedInMovement()
    {
        using var db = new TestDb();
        var user = AddUser(db);
        var product = AddProduct(db, "A-103", "Товар старой отгрузки", stock: 50);
        db.Context.WarehouseCells.Add(new WarehouseCell { Code = "A-01", RowLabel = "A", ColIndex = 1, ProductId = product.Id });
        db.Context.Shipments.Add(new Shipment
        {
            Name = "Старая отгрузка",
            Recipient = "Покупатель",
            ShippedAt = DateTime.Now.AddDays(-45),
            CreatedByUserId = user.Id,
            Items = { new ShipmentItem { ProductId = product.Id, Quantity = 100, Price = 100m, PurchaseCost = 50m } }
        });
        db.Context.SaveChanges();

        var cell = new HeatmapService(db.Context).GetCells().Single();

        cell.MovementUnits.Should().Be(0);
        cell.Movement.Should().Be(MovementLevel.Low);
    }

    private static User AddUser(TestDb db)
    {
        var user = new User { Login = Guid.NewGuid().ToString("N"), PasswordHash = "hash", Role = UserRole.Administrator };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();
        return user;
    }

    private static Product AddProduct(TestDb db, string article, string name, int stock)
    {
        var product = new Product
        {
            Article = article,
            Name = name,
            Unit = "шт.",
            PurchasePrice = 100m,
            StockQuantity = stock
        };
        db.Context.Products.Add(product);
        db.Context.SaveChanges();
        return product;
    }
}
