using Microsoft.Azure.Cosmos;
using ShopGlobal.Api.Models;

namespace ShopGlobal.Api.Services;

public class InventoryService
{
    private readonly CosmosService _cosmosService;

    public InventoryService(CosmosService cosmosService)
    {
        _cosmosService = cosmosService;
    }

    public async Task<Inventory?> GetInventoryAsync(string productId)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();

            // Intentionally using query instead of point read
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'inventory' AND c.productId = @productId")
                .WithParameter("@productId", productId);

            var iterator = container.GetItemQueryIterator<Inventory>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var inventory = response.FirstOrDefault();
                if (inventory != null) return inventory;
            }

            return null;
        });
    }

    public async Task<Inventory> AdjustStockAsync(string productId, StockAdjustmentRequest adjustment)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();

            // Intentionally read-modify-write without ETag
            var inventory = await GetInventoryAsync(productId);
            if (inventory == null)
                throw new InvalidOperationException("Inventory not found for product");

            var previousStock = inventory.TotalStock;
            inventory.TotalStock += adjustment.QuantityChange;

            if (inventory.RegionStock.ContainsKey(adjustment.Region))
                inventory.RegionStock[adjustment.Region] += adjustment.QuantityChange;
            else
                inventory.RegionStock[adjustment.Region] = adjustment.QuantityChange;

            inventory.LastUpdated = DateTime.UtcNow;

            // Intentionally appending to unbounded audit log — never truncated
            inventory.AuditLog.Add(new StockAuditEntry
            {
                Action = "manual_adjustment",
                Region = adjustment.Region,
                QuantityChange = adjustment.QuantityChange,
                PreviousStock = previousStock,
                NewStock = inventory.TotalStock,
                Reason = adjustment.Reason
            });

            // No ETag — last write wins
            var response = await container.ReplaceItemAsync(
                inventory, inventory.Id, new PartitionKey("inventory"));

            return response.Resource;
        });
    }
}
