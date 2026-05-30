using NLog;
using WarehouseApp.Data.Repositories;
using WarehouseApp.Models;

namespace WarehouseApp.Services;

public interface ICategoryService
{
    List<Category> GetAll();
    Category? GetById(int id);
    OperationResult Create(string name);
    OperationResult Update(int id, string name);
    OperationResult Delete(int id);
}

public class CategoryService : ICategoryService
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly ICategoryRepository _repo;
    public CategoryService(ICategoryRepository repo) => _repo = repo;

    public List<Category> GetAll() => _repo.GetAll();
    public Category? GetById(int id) => _repo.GetById(id);

    public OperationResult Create(string name)
    {
        logger.Trace("Создание категории '{Name}'", name);

        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Fail("Введите название категории.");
        if (_repo.GetByName(name.Trim()) != null)
        {
            logger.Warn("Отказ: категория '{Name}' уже существует", name);
            return OperationResult.Fail("Категория с таким названием уже существует.");
        }

        try
        {
            _repo.Add(new Category { Name = name.Trim() });
            _repo.Save();
            logger.Info("Создана категория '{Name}'", name.Trim());
            return OperationResult.Ok("Категория создана.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Ошибка создания категории '{Name}'", name);
            throw;
        }
    }

    public OperationResult Update(int id, string name)
    {
        logger.Trace("Обновление категории id={Id} -> '{Name}'", id, name);

        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Fail("Введите название категории.");
        var cat = _repo.GetById(id);
        if (cat == null)
        {
            logger.Warn("Попытка обновить несуществующую категорию id={Id}", id);
            return OperationResult.Fail("Категория не найдена.");
        }
        var dup = _repo.GetByName(name.Trim());
        if (dup != null && dup.Id != id)
        {
            logger.Warn("Отказ в обновлении категории id={Id}: имя '{Name}' уже занято", id, name);
            return OperationResult.Fail("Категория с таким названием уже существует.");
        }

        try
        {
            string oldName = cat.Name;
            cat.Name = name.Trim();
            _repo.Update(cat);
            _repo.Save();
            logger.Info("Категория id={Id} переименована: '{Old}' -> '{New}'", id, oldName, cat.Name);
            return OperationResult.Ok("Категория обновлена.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Ошибка обновления категории id={Id}", id);
            throw;
        }
    }

    public OperationResult Delete(int id)
    {
        logger.Trace("Удаление категории id={Id}", id);

        var cat = _repo.GetById(id);
        if (cat == null)
        {
            logger.Warn("Попытка удалить несуществующую категорию id={Id}", id);
            return OperationResult.Fail("Категория не найдена.");
        }
        try
        {
            _repo.Delete(cat);
            _repo.Save();
            logger.Info("Удалена категория '{Name}' (id={Id})", cat.Name, id);
            return OperationResult.Ok("Категория удалена.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Ошибка удаления категории '{Name}' (id={Id})", cat.Name, id);
            throw;
        }
    }
}
