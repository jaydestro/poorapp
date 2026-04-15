using Microsoft.Azure.Cosmos;
using ShopGlobal.Api.Models;

namespace ShopGlobal.Api.Services;

public class ProductService
{
    private readonly CosmosService _cosmosService;

    public ProductService(CosmosService cosmosService)
    {
        _cosmosService = cosmosService;
    }

    public async Task<Product?> GetProductAsync(string productId)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();

            // Intentionally using a query instead of point read
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'product' AND c.id = @id")
                .WithParameter("@id", productId);

            var iterator = container.GetItemQueryIterator<Product>(query);
            Product? product = null;

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                product = response.FirstOrDefault();
            }

            return product;
        });
    }

    public async Task<List<Product>> SearchProductsAsync(string searchTerm)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();

            // Intentionally using CONTAINS with LOWER for search — no full-text index
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'product' AND (CONTAINS(LOWER(c.name), LOWER(@term)) OR CONTAINS(LOWER(c.description), LOWER(@term)))")
                .WithParameter("@term", searchTerm);

            var iterator = container.GetItemQueryIterator<Product>(query);
            var results = new List<Product>();

            // Intentionally returning ALL results with no pagination
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        });
    }

    public async Task<List<Product>> GetProductsByCategoryAsync(string category)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();

            // SELECT * — no projection
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'product' AND c.category = @category")
                .WithParameter("@category", category);

            var iterator = container.GetItemQueryIterator<Product>(query);
            var results = new List<Product>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        });
    }
}
