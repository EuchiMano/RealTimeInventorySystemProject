using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RealTimeInventorySystem.DTOs;
using RealTimeInventorySystem.Services;

namespace RealTimeInventorySystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly InventoryService _inventoryService;

    public InventoryController(
        InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("{productId:long}/{warehouseId:long}")]
    public async Task<IActionResult> GetInventory(
        long productId,
        long warehouseId)
    {
        var result =
            await _inventoryService.GetInventoryAsync(
                productId,
                warehouseId);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [EnableRateLimiting("InventoryPolicy")]
    [HttpPatch("{productId:long}")]
    public async Task<IActionResult> UpdateInventory(
        long productId,
        UpdateStockRequest request)
    {
        if (request.Quantity < 0)
        {
            return BadRequest(new { Message = "Quantity cannot be negative." });
        }

        try
        {
            await _inventoryService.UpdateInventoryAsync(
                productId,
                request);

            return NoContent();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict(new
            {
                Message = ex.Message
            });
        }
    }

    [HttpGet("product/{productId:long}")]
    public async Task<IActionResult>
    GetProductInventories(long productId)
    {
        var inventories =
            await _inventoryService
                .GetProductInventoriesAsync(productId);

        return Ok(inventories);
    }

    [HttpGet("{productId:long}/movements")]
    public async Task<IActionResult>
        GetMovements(long productId)
    {
        var movements =
            await _inventoryService
                .GetMovementsAsync(productId);

        return Ok(movements);
    }

    [EnableRateLimiting("InventoryPolicy")]
    [HttpPost("movements")]
    public async Task<IActionResult>
        RegisterMovement(
            CreateInventoryMovementRequest request)
    {
        try
        {
            await _inventoryService
                .RegisterMovementAsync(request);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                Message = ex.Message
            });
        }
    }
}
