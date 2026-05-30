using WarehouseApp.Models;

namespace WarehouseApp.Data.Repositories;

public interface IShipmentRepository
{
    List<Shipment> GetAll();
    List<Shipment> Search(string query);
    Shipment? GetById(int id);
    void Add(Shipment shipment);
    void Save();
}
