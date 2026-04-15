using Microsoft.Azure.Cosmos;
using ShopGlobal.Api.Models;

namespace ShopGlobal.Api.Services;

public class CustomerService
{
    private readonly CosmosService _cosmosService;

    public CustomerService(CosmosService cosmosService)
    {
        _cosmosService = cosmosService;
    }

    public async Task<Customer?> GetCustomerAsync(string customerId)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();

            // Intentionally using query instead of point read
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'customer' AND c.id = @id")
                .WithParameter("@id", customerId);

            var iterator = container.GetItemQueryIterator<Customer>(query);
            Customer? customer = null;

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                customer = response.FirstOrDefault();
            }

            if (customer != null)
            {
                // Intentionally loading ALL orders and embedding them in the customer document
                var orderQuery = new QueryDefinition(
                    "SELECT * FROM c WHERE c.type = 'order' AND c.customerId = @customerId")
                    .WithParameter("@customerId", customerId);

                var orderIterator = container.GetItemQueryIterator<Order>(orderQuery);
                var orders = new List<Order>();

                while (orderIterator.HasMoreResults)
                {
                    var response = await orderIterator.ReadNextAsync();
                    orders.AddRange(response);
                }

                // Replace the customer document with updated order history
                customer.OrderHistory = orders;
                await container.ReplaceItemAsync(customer, customer.Id, new PartitionKey("customer"));
            }

            return customer;
        });
    }

    public async Task<Customer> UpdateCustomerAsync(string customerId, Customer updated)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();

            // Read current customer first
            var existing = await GetCustomerAsync(customerId);
            if (existing == null)
                throw new InvalidOperationException("Customer not found");

            updated.Id = customerId;
            updated.Type = "customer";

            // Replace entire document
            var response = await container.ReplaceItemAsync(updated, customerId, new PartitionKey("customer"));
            return response.Resource;
        });
    }

    public async Task<List<Order>> GetCustomerOrdersAsync(string customerId)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();

            // SELECT * with no pagination
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'order' AND c.customerId = @customerId")
                .WithParameter("@customerId", customerId);

            var iterator = container.GetItemQueryIterator<Order>(query);
            var results = new List<Order>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        });
    }

    public async Task<Customer?> FindCustomerByEmailAsync(string email)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();

            // Cross-partition query to find by email
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'customer' AND c.email = @email")
                .WithParameter("@email", email);

            var iterator = container.GetItemQueryIterator<Customer>(query);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var customer = response.FirstOrDefault();
                if (customer != null) return customer;
            }

            return null;
        });
    }
}
