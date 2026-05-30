using WarehouseApp.Models;

namespace WarehouseApp.Data.Repositories;

public interface IWriteOffRepository
{
    List<WriteOff> GetAll();
    List<WriteOff> GetByPeriod(DateTime from, DateTime to);
}
