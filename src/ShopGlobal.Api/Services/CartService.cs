using Microsoft.Azure.Cosmos;
using ShopGlobal.Api.Models;

namespace ShopGlobal.Api.Services;

public class CartService
{
    private readonly CosmosService _cosmosService;

    public CartService(CosmosService cosmosService)
    {
        _cosmosService = cosmosService;
    }

    public async Task<Cart> GetCartAsync(string customerId)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();

            // Intentionally using SELECT * and cross-partition query via /type
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'cart' AND c.customerId = @customerId")
                .WithParameter("@customerId", customerId);

            var iterator = container.GetItemQueryIterator<Cart>(query);
            var results = new List<Cart>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results.FirstOrDefault() ?? new Cart { CustomerId = customerId };
        });
    }

    public async Task<Cart> AddItemAsync(string customerId, CartItem item)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();
            var cart = await GetCartAsync(customerId);

            // Intentionally reading the full product and embedding a snapshot
            var productQuery = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'product' AND c.id = @productId")
                .WithParameter("@productId", item.ProductId);

            var productIterator = container.GetItemQueryIterator<Product>(productQuery);
            Product? product = null;

            while (productIterator.HasMoreResults)
            {
                var response = await productIterator.ReadNextAsync();
                product = response.FirstOrDefault();
            }

            if (product != null)
            {
                item.ProductSnapshot = product; // full product snapshot with all reviews
                item.PriceAtAdd = product.Price;
            }

            item.ItemId = Guid.NewGuid().ToString();
            cart.Items.Add(item);
            cart.TotalAmount = cart.Items.Sum(i => i.PriceAtAdd * i.Quantity);
            cart.UpdatedDate = DateTime.UtcNow;

            if (string.IsNullOrEmpty(cart.Id) || cart.Items.Count == 1)
            {
                cart.Id = Guid.NewGuid().ToString();
                cart.CustomerId = customerId;
                await container.CreateItemAsync(cart, new PartitionKey("cart"));
            }
            else
            {
                await container.ReplaceItemAsync(cart, cart.Id, new PartitionKey("cart"));
            }

            return cart;
        });
    }

    public async Task<Cart> RemoveItemAsync(string customerId, string itemId)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();
            var cart = await GetCartAsync(customerId);

            cart.Items.RemoveAll(i => i.ItemId == itemId);
            cart.TotalAmount = cart.Items.Sum(i => i.PriceAtAdd * i.Quantity);
            cart.UpdatedDate = DateTime.UtcNow;

            await container.ReplaceItemAsync(cart, cart.Id, new PartitionKey("cart"));
            return cart;
        });
    }

    public async Task<Order> CheckoutAsync(string customerId)
    {
        return await _cosmosService.ExecuteWithRetry(async () =>
        {
            var container = _cosmosService.GetContainer();
            var cart = await GetCartAsync(customerId);

            if (cart.Items.Count == 0)
                throw new InvalidOperationException("Cart is empty");

            // Intentionally loading full customer to embed in order
            var customerQuery = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'customer' AND c.id = @customerId")
                .WithParameter("@customerId", customerId);

            var customerIterator = container.GetItemQueryIterator<Customer>(customerQuery);
            Customer? customer = null;

            while (customerIterator.HasMoreResults)
            {
                var response = await customerIterator.ReadNextAsync();
                customer = response.FirstOrDefault();
            }

            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                CustomerId = customerId,
                OrderDate = DateTime.UtcNow,
                Status = "Confirmed",
                TotalAmount = cart.TotalAmount,
                CustomerSnapshot = customer ?? new Customer(), // full customer embedded
                Category = cart.Items.FirstOrDefault()?.ProductSnapshot?.Category ?? "Unknown",
                LineItems = cart.Items.Select(i => new OrderLineItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    Price = i.PriceAtAdd,
                    Subtotal = i.PriceAtAdd * i.Quantity,
                    ProductSnapshot = i.ProductSnapshot // full product embedded in line item
                }).ToList()
            };

            if (customer?.Addresses.Count > 0)
                order.ShippingAddress = customer.Addresses[0];

            // Create the order
            await container.CreateItemAsync(order, new PartitionKey("order"));

            // Intentionally updating inventory without ETags (read-modify-write race condition)
            foreach (var item in cart.Items)
            {
                try
                {
                    var invQuery = new QueryDefinition(
                        "SELECT * FROM c WHERE c.type = 'inventory' AND c.productId = @productId")
                        .WithParameter("@productId", item.ProductId);

                    var invIterator = container.GetItemQueryIterator<Inventory>(invQuery);

                    while (invIterator.HasMoreResults)
                    {
                        var response = await invIterator.ReadNextAsync();
                        var inventory = response.FirstOrDefault();
                        if (inventory != null)
                        {
                            var previousStock = inventory.TotalStock;
                            inventory.TotalStock -= item.Quantity;
                            inventory.LastUpdated = DateTime.UtcNow;

                            // Intentionally appending to unbounded audit log
                            inventory.AuditLog.Add(new StockAuditEntry
                            {
                                Action = "checkout",
                                QuantityChange = -item.Quantity,
                                PreviousStock = previousStock,
                                NewStock = inventory.TotalStock,
                                Reason = $"Order {order.Id}"
                            });

                            // No ETag check — last write wins
                            await container.ReplaceItemAsync(inventory, inventory.Id, new PartitionKey("inventory"));
                        }
                    }
                }
                catch { /* swallow inventory errors */ }
            }

            // Intentionally appending order to customer document
            if (customer != null)
            {
                customer.OrderHistory.Add(order);
                await container.ReplaceItemAsync(customer, customer.Id, new PartitionKey("customer"));
            }

            // Delete the cart
            await container.DeleteItemAsync<Cart>(cart.Id, new PartitionKey("cart"));

            return order;
        });
    }
}
