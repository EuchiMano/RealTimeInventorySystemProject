using RealTimeInventorySystem.Models;

namespace RealTimeInventorySystem.Repositories;

public interface IInventoryRepository
{
    Task<Inventory> GetStockAsync(long productId, long warehouseId);
    Task<Inventory> UpdateStockAsync(long productId, long warehouseId, int newQuantity);
    Task<bool> ProductExistsAsync(long productId);
    Task<bool> WarehouseExistsAsync(long warehouseId);
    Task SaveAsync();
}
