using Microsoft.EntityFrameworkCore;
using RealTimeInventorySystem.Data;
using RealTimeInventorySystem.Models;

namespace RealTimeInventorySystem.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly InventoryDbContext _context;

    public InventoryRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<Inventory> GetStockAsync(long productId, long warehouseId)
    {
        return await _context.Inventories
            .Where(i => i.ProductId == productId && i.WarehouseId == warehouseId)
            .FirstOrDefaultAsync();
    }

    public async Task<Inventory> UpdateStockAsync(long productId, long warehouseId, int newQuantity)
    {
        var inventory = await GetStockAsync(productId, warehouseId);
        
        if (inventory == null)
        {
            // Create new inventory record if doesn't exist
            inventory = new Inventory
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                Quantity = newQuantity,
                LastUpdated = DateTime.UtcNow
            };
            _context.Inventories.Add(inventory);
        }
        else
        {
            // Update existing inventory
            inventory.Quantity = newQuantity;
            inventory.LastUpdated = DateTime.UtcNow;
            inventory.Version++; // Increment version for optimistic concurrency
            _context.Inventories.Update(inventory);
        }

        return inventory;
    }

    public async Task<bool> ProductExistsAsync(long productId)
    {
        return await _context.Products.AnyAsync(p => p.ProductId == productId);
    }

    public async Task<bool> WarehouseExistsAsync(long warehouseId)
    {
        return await _context.Warehouses.AnyAsync(w => w.WarehouseId == warehouseId);
    }

    public async Task SaveAsync()
    {
        await _context.SaveChangesAsync();
    }
}
