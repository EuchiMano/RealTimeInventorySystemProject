using System;
using Microsoft.EntityFrameworkCore;
using RealTimeInventorySystem.Data;
using RealTimeInventorySystem.DTOs;
using RealTimeInventorySystem.Models;

namespace RealTimeInventorySystem.Services;

public class InventoryService
{
    private readonly InventoryDbContext _db;

    public InventoryService(
        InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<InventoryResponse?> GetInventoryAsync(
        long productId,
        long warehouseId)
    {
        var inventory = await _db.Inventories
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ProductId == productId &&
                x.WarehouseId == warehouseId);

        if (inventory is null)
            return null;

        return new InventoryResponse
        {
            InventoryId = inventory.InventoryId,
            ProductId = inventory.ProductId,
            WarehouseId = inventory.WarehouseId,
            Quantity = inventory.Quantity,
            ReservedQuantity = inventory.ReservedQuantity,
            AvailableQuantity = inventory.AvailableQuantity,
            Version = inventory.Version,
            LastUpdated = inventory.LastUpdated
        };
    }

    public async Task UpdateInventoryAsync(
        long productId,
        UpdateStockRequest request)
    {
        var inventory = await _db.Inventories
            .FirstOrDefaultAsync(x =>
                x.ProductId == productId &&
                x.WarehouseId == request.WarehouseId);

        if (inventory is null)
        {
            // If request.Version is not zero, consider this a missing record
            if (request.Version != 0)
                throw new InvalidOperationException("Inventory not found");

            // Create a new inventory record (fallback for tests/demo)
            inventory = new Inventory
            {
                ProductId = productId,
                WarehouseId = request.WarehouseId,
                Quantity = 0,
                ReservedQuantity = 0,
                Version = 0,
                LastUpdated = DateTime.UtcNow
            };

            _db.Inventories.Add(inventory);
        }

        if (inventory.Version != request.Version)
            throw new DbUpdateConcurrencyException(
                "Inventory has been modified by another process.");

        // Apply new absolute quantity (this is a "set" operation)
        var oldQuantity = inventory.Quantity;
        inventory.Quantity = request.Quantity;
        inventory.Version++;
        inventory.LastUpdated = DateTime.UtcNow;

        // Only create a movement if quantity actually changed
        var delta = request.Quantity - oldQuantity;
        if (delta != 0)
        {
            var movement = new InventoryMovement
            {
                ProductId = inventory.ProductId,
                WarehouseId = inventory.WarehouseId,
                MovementType = delta >= 0 ? "IN" : "OUT",
                Quantity = Math.Abs(delta),
                Reason = request.Reason ?? "Manual adjustment",
                ReferenceNumber = request.ReferenceNumber ?? Guid.NewGuid().ToString(),
                UserId = request.UserId ?? "system",
                Notes = request.Notes ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _db.InventoryMovements.Add(movement);
        }

        // Rely on EF Core to save both the inventory update and movement together
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<InventoryResponse>>
    GetProductInventoriesAsync(long productId)
    {
        return await _db.Inventories
            .AsNoTracking()
            .Where(x => x.ProductId == productId)
            .Select(x => new InventoryResponse
            {
                InventoryId = x.InventoryId,
                ProductId = x.ProductId,
                WarehouseId = x.WarehouseId,
                Quantity = x.Quantity,
                ReservedQuantity = x.ReservedQuantity,
                AvailableQuantity = x.Quantity - x.ReservedQuantity,
                Version = x.Version,
                LastUpdated = x.LastUpdated
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryMovementResponse>>
    GetMovementsAsync(long productId)
    {
        return await _db.InventoryMovements
            .AsNoTracking()
            .Where(x => x.ProductId == productId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new InventoryMovementResponse
            {
                MovementId = x.MovementId,
                ProductId = x.ProductId,
                WarehouseId = x.WarehouseId,
                MovementType = x.MovementType,
                Quantity = x.Quantity,
                Reason = x.Reason,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();
    }

    public async Task RegisterMovementAsync(
    CreateInventoryMovementRequest request)
    {
        var inventory = await _db.Inventories
            .FirstOrDefaultAsync(x =>
                x.ProductId == request.ProductId &&
                x.WarehouseId == request.WarehouseId);

        if (inventory is null)
        {
            // Create inventory record if missing (fallback behavior)
            inventory = new Inventory
            {
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                Quantity = 0,
                ReservedQuantity = 0,
                Version = 0,
                LastUpdated = DateTime.UtcNow
            };

            _db.Inventories.Add(inventory);
        }

        switch (request.MovementType.ToUpperInvariant())
        {
            case "IN":
                inventory.Quantity += request.Quantity;
                break;

            case "OUT":

                if (inventory.AvailableQuantity < request.Quantity)
                    throw new InvalidOperationException(
                        "Insufficient stock.");

                inventory.Quantity -= request.Quantity;
                break;

            default:
                throw new InvalidOperationException(
                    "Invalid movement type.");
        }

        inventory.Version++;
        inventory.LastUpdated = DateTime.UtcNow;

        var movement = new InventoryMovement
        {
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            MovementType = request.MovementType,
            Quantity = request.Quantity,
            Reason = request.Reason,
            ReferenceNumber = request.ReferenceNumber,
            UserId = request.UserId,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _db.InventoryMovements.Add(movement);

        await _db.SaveChangesAsync();
    }
}
