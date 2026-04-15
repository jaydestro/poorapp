using Microsoft.AspNetCore.Mvc;
using ShopGlobal.Api.Models;
using ShopGlobal.Api.Services;

namespace ShopGlobal.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly InventoryService _inventoryService;

    public InventoryController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("{productId}")]
    public async Task<IActionResult> GetInventory(string productId)
    {
        var inventory = await _inventoryService.GetInventoryAsync(productId);
        if (inventory == null) return NotFound();
        return Ok(inventory);
    }

    [HttpPut("{productId}/adjust")]
    public async Task<IActionResult> AdjustStock(string productId, [FromBody] StockAdjustmentRequest request)
    {
        var inventory = await _inventoryService.AdjustStockAsync(productId, request);
        return Ok(inventory);
    }
}
