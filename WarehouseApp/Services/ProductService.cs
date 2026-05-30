using NLog;
using WarehouseApp.Data.Repositories;
using WarehouseApp.Models;

namespace WarehouseApp.Services;

public interface IProductService
{
    List<Product> GetAll();
    List<Product> GetByCategory(int categoryId);
    List<Product> Search(string query);
    Product? GetById(int id);
    OperationResult Create(Product product);
    OperationResult Update(Product product);
    OperationResult Delete(int id);
}

public class ProductService : IProductService
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly IProductRepository _repo;
    public ProductService(IProductRepository repo) => _repo = repo;

    public List<Product> GetAll() => _repo.GetAll();
    public List<Product> GetByCategory(int categoryId) => _repo.GetByCategory(categoryId);
    public List<Product> Search(string query)
    {
        logger.Trace("Поиск товаров по запросу '{Query}'", query);
        return _repo.Search(query);
    }
    public Product? GetById(int id) => _repo.GetById(id);

    public OperationResult Create(Product product)
    {
        logger.Trace("Создание товара '{Name}'", product.Name);

        if (string.IsNullOrWhiteSpace(product.Name))
            return OperationResult.Fail("Название товара обязательно.");
        if (string.IsNullOrWhiteSpace(product.Article))
            product.Article = GenerateNextArticle();
        if (product.PurchasePrice < 0)
        {
            logger.Warn("Отказ в создании товара '{Name}': отрицательная цена {Price}",
                product.Name, product.PurchasePrice);
            return OperationResult.Fail("Цена не может быть отрицательной.");
        }
        if (product.StockQuantity < 0)
        {
            logger.Warn("Отказ в создании товара '{Name}': отрицательный остаток {Stock}",
                product.Name, product.StockQuantity);
            return OperationResult.Fail("Остаток не может быть отрицательным.");
        }

        try
        {
            _repo.Add(product);
            _repo.Save();
            logger.Info("Создан товар '{Name}' (артикул {Article}, цена {Price}, остаток {Stock})",
                product.Name, product.Article, product.PurchasePrice, product.StockQuantity);
            return OperationResult.Ok("Товар успешно создан.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Ошибка сохранения товара '{Name}'", product.Name);
            throw;
        }
    }

    public OperationResult Update(Product product)
    {
        logger.Trace("Обновление товара id={Id} '{Name}'", product.Id, product.Name);

        if (string.IsNullOrWhiteSpace(product.Name))
            return OperationResult.Fail("Название товара обязательно.");
        if (string.IsNullOrWhiteSpace(product.Article))
            product.Article = GenerateNextArticle();
        if (product.PurchasePrice < 0)
        {
            logger.Warn("Отказ в обновлении товара '{Name}': отрицательная цена {Price}",
                product.Name, product.PurchasePrice);
            return OperationResult.Fail("Цена не может быть отрицательной.");
        }
        if (product.StockQuantity < 0)
        {
            logger.Warn("Отказ в обновлении товара '{Name}': отрицательный остаток {Stock}",
                product.Name, product.StockQuantity);
            return OperationResult.Fail("Остаток не может быть отрицательным.");
        }

        try
        {
            _repo.Update(product);
            _repo.Save();
            logger.Info("Обновлён товар '{Name}' (id={Id})", product.Name, product.Id);
            return OperationResult.Ok("Товар успешно обновлён.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Ошибка обновления товара '{Name}' (id={Id})", product.Name, product.Id);
            throw;
        }
    }

    private string GenerateNextArticle()
    {
        int maxNumeric = 0;
        foreach (var item in _repo.GetAll())
        {
            var digits = new string((item.Article ?? string.Empty).Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var value) && value > maxNumeric)
                maxNumeric = value;
        }
        return (maxNumeric + 1).ToString();
    }

    public OperationResult Delete(int id)
    {
        logger.Trace("Удаление товара id={Id}", id);

        var p = _repo.GetById(id);
        if (p == null)
        {
            logger.Warn("Попытка удалить несуществующий товар id={Id}", id);
            return OperationResult.Fail("Товар не найден.");
        }
        try
        {
            _repo.Delete(p);
            _repo.Save();
            logger.Info("Удалён товар '{Name}' (id={Id}, артикул {Article})",
                p.Name, id, p.Article);
            return OperationResult.Ok("Товар успешно удалён.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Ошибка удаления товара '{Name}' (id={Id})", p.Name, id);
            throw;
        }
    }
}
