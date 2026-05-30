using FluentAssertions;
using WarehouseApp.Data;
using WarehouseApp.Data.Repositories;
using WarehouseApp.Models;
using WarehouseApp.Services;
using WarehouseApp.Tests.Infrastructure;
using Xunit;

namespace WarehouseApp.Tests.Unit;

public sealed class SupplyServiceTests
{
    [Fact]
    public void CreateSupply_EmptyName_ReturnsValidationError()
    {
        using var db = new TestDb();
        var product = AddProduct(db, "P-001", "Товар", stock: 0);
        var service = CreateService(db.Context);

        var result = service.CreateSupply("", "Поставщик", DateTime.Today,
            new List<(int ProductId, decimal PurchasePrice, int Quantity, DateTime? SaleDeadline)>
            {
                (product.Id, 100m, 1, DateTime.Today.AddDays(10))
            },
            userId: 1);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("название");
    }

    [Fact]
    public void CreateSupply_WithoutItems_ReturnsValidationError()
    {
        using var db = new TestDb();
        var service = CreateService(db.Context);

        var result = service.CreateSupply("Поставка", "Поставщик", DateTime.Today,
            new List<(int ProductId, decimal PurchasePrice, int Quantity, DateTime? SaleDeadline)>(),
            userId: 1);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("позици");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateSupply_NonPositiveQuantity_ReturnsValidationError(int quantity)
    {
        using var db = new TestDb();
        var product = AddProduct(db, "P-002", "Товар", stock: 0);
        var service = CreateService(db.Context);

        var result = service.CreateSupply("Поставка", "Поставщик", DateTime.Today,
            new List<(int ProductId, decimal PurchasePrice, int Quantity, DateTime? SaleDeadline)>
            {
                (product.Id, 100m, quantity, DateTime.Today.AddDays(10))
            },
            userId: 1);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Количество");
    }

    [Fact]
    public void CreateSupply_NegativePurchasePrice_ReturnsValidationError()
    {
        using var db = new TestDb();
        var product = AddProduct(db, "P-003", "Товар", stock: 0);
        var service = CreateService(db.Context);

        var result = service.CreateSupply("Поставка", "Поставщик", DateTime.Today,
            new List<(int ProductId, decimal PurchasePrice, int Quantity, DateTime? SaleDeadline)>
            {
                (product.Id, -1m, 1, DateTime.Today.AddDays(10))
            },
            userId: 1);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("не может быть отрицательной");
    }

    [Fact]
    public void CreateSupply_UnknownProduct_ReturnsValidationError()
    {
        using var db = new TestDb();
        var service = CreateService(db.Context);

        var result = service.CreateSupply("Поставка", "Поставщик", DateTime.Today,
            new List<(int ProductId, decimal PurchasePrice, int Quantity, DateTime? SaleDeadline)>
            {
                (999, 100m, 1, DateTime.Today.AddDays(10))
            },
            userId: 1);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("не найден");
    }

    [Fact]
    public void CreateSupply_SaleDeadlineNotAfterSupplyDate_ReturnsValidationError()
    {
        using var db = new TestDb();
        var product = AddProduct(db, "P-004", "Товар", stock: 0);
        var service = CreateService(db.Context);

        var result = service.CreateSupply("Поставка", "Поставщик", DateTime.Today,
            new List<(int ProductId, decimal PurchasePrice, int Quantity, DateTime? SaleDeadline)>
            {
                (product.Id, 100m, 1, DateTime.Today)
            },
            userId: 1);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("должен быть позже даты поставки");
    }

    [Fact]
    public void CreateSupply_ValidSupply_CreatesSupplyItemBatchAndIncreasesStock()
    {
        using var db = new TestDb();
        var user = AddUser(db);
        var product = AddProduct(db, "P-005", "Новый товар", stock: 3);
        var service = CreateService(db.Context);

        var result = service.CreateSupply("Поставка", "Поставщик", DateTime.Today,
            new List<(int ProductId, decimal PurchasePrice, int Quantity, DateTime? SaleDeadline)>
            {
                (product.Id, 120m, 7, DateTime.Today.AddDays(14))
            },
            user.Id);

        result.Success.Should().BeTrue();
        db.Context.ChangeTracker.Clear();

        var savedProduct = db.Context.Products.Single(p => p.Id == product.Id);
        savedProduct.StockQuantity.Should().Be(10);
        db.Context.Supplies.Should().ContainSingle(s => s.Name == "Поставка" && s.TotalCost == 840m);
        db.Context.SupplyItems.Should().ContainSingle(i => i.ProductId == product.Id && i.Quantity == 7);
        db.Context.ProductBatches.Should().ContainSingle(b => b.ProductId == product.Id && b.Quantity == 7 && b.PurchasePrice == 120m);
    }

    [Fact]
    public void CreateSupply_ForProductWithoutCell_PlacesProductInFreeWarehouseCell()
    {
        using var db = new TestDb();
        var user = AddUser(db);
        var product = AddProduct(db, "P-006", "Товар для карты", stock: 0);
        db.Context.WarehouseCells.Add(new WarehouseCell { Code = "A-01", RowLabel = "A", ColIndex = 1 });
        db.Context.SaveChanges();
        var service = CreateService(db.Context);

        var result = service.CreateSupply("Поставка для карты", "Поставщик", DateTime.Today,
            new List<(int ProductId, decimal PurchasePrice, int Quantity, DateTime? SaleDeadline)>
            {
                (product.Id, 100m, 10, DateTime.Today.AddDays(20))
            },
            user.Id);

        result.Success.Should().BeTrue();
        db.Context.ChangeTracker.Clear();

        var cell = db.Context.WarehouseCells.Single(c => c.Code == "A-01");
        cell.ProductId.Should().Be(product.Id, "товар после поставки должен быть размещён в свободной ячейке, иначе тепловая карта его не покажет");

        var heatmapCell = new HeatmapService(db.Context).GetCells().Single(c => c.Code == "A-01");
        heatmapCell.ProductId.Should().Be(product.Id);
        heatmapCell.ProductName.Should().Be("Товар для карты");
        heatmapCell.Stock.Should().Be(10);
        heatmapCell.IsEmpty.Should().BeFalse();
    }

    private static ISupplyService CreateService(AppDbContext ctx)
    {
        var supplyRepo = new SupplyRepository(ctx);
        var productRepo = new ProductRepository(ctx);

        foreach (var ctor in typeof(SupplyService).GetConstructors())
        {
            object?[] args;
            try
            {
                args = ctor.GetParameters()
                    .Select(p => BuildConstructorArgument(p.ParameterType, ctx, supplyRepo, productRepo))
                    .ToArray();
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            try
            {
                return (ISupplyService)ctor.Invoke(args);
            }
            catch
            {
                // Try next overload if present.
            }
        }

        throw new InvalidOperationException("No supported SupplyService constructor was found.");
    }

    private static object? BuildConstructorArgument(
        Type parameterType,
        AppDbContext ctx,
        ISupplyRepository supplyRepo,
        IProductRepository productRepo)
    {
        if (parameterType == typeof(ISupplyRepository)) return supplyRepo;
        if (parameterType == typeof(IProductRepository)) return productRepo;
        if (parameterType == typeof(AppDbContext)) return ctx;
        if (parameterType == typeof(ICurrencyService)) return null;

        throw new InvalidOperationException($"Unsupported SupplyService constructor parameter: {parameterType.FullName}");
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
