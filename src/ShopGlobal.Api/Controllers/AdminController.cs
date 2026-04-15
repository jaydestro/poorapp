using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using ShopGlobal.Api.Models;
using ShopGlobal.Api.Services;

namespace ShopGlobal.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly CosmosService _cosmosService;
    private readonly RecommendationService _recommendationService;

    public AdminController(CosmosService cosmosService, RecommendationService recommendationService)
    {
        _cosmosService = cosmosService;
        _recommendationService = recommendationService;
    }

    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        await _cosmosService.InitializeDatabaseAsync();
        var container = _cosmosService.GetContainer();
        var random = new Random(42);

        var categories = new[] { "Electronics", "Clothing", "Home", "Books", "Food" };
        var regions = new[] { "East US", "West Europe", "Southeast Asia" };
        var productIds = new List<string>();

        var productNames = new Dictionary<string, string[]>
        {
            ["Electronics"] = ["Wireless Headphones", "4K Monitor", "Mechanical Keyboard", "USB-C Hub", "Portable SSD", "Smart Watch", "Bluetooth Speaker", "Webcam HD Pro", "LED Desk Lamp", "Gaming Mouse"],
            ["Clothing"] = ["Slim Fit Jeans", "Cotton Crew T-Shirt", "Merino Wool Sweater", "Running Shoes", "Canvas Backpack", "Leather Belt", "Aviator Sunglasses", "Rain Jacket", "Cashmere Scarf", "Hiking Boots"],
            ["Home"] = ["French Press Coffee Maker", "Memory Foam Pillow", "Cast Iron Skillet", "Bamboo Cutting Board", "Scented Candle Set", "Ceramic Plant Pots", "Throw Blanket", "Wall Clock", "Spice Rack Organizer", "Stainless Tumbler"],
            ["Books"] = ["Clean Code", "Designing Data-Intensive Apps", "The Pragmatic Programmer", "System Design Interview", "Atomic Habits", "Deep Work", "Zero to One", "Thinking Fast and Slow", "The Art of War", "Sapiens"],
            ["Food"] = ["Organic Coffee Beans", "Dark Chocolate Truffles", "Matcha Green Tea", "Trail Mix Variety Pack", "Hot Sauce Collection", "Olive Oil Extra Virgin", "Dried Mango Slices", "Almond Butter", "Sea Salt Caramels", "Gourmet Popcorn Set"]
        };

        // Seed 50 products (10 per category) — still one at a time
        foreach (var cat in categories)
        {
            for (int i = 0; i < 10; i++)
            {
                var product = new Product
                {
                    Id = $"{cat.ToLower()}-{i:D3}",
                    Name = productNames[cat][i],
                    Description = $"A wonderful {cat.ToLower()} product. {productNames[cat][i]} features premium quality materials, excellent craftsmanship, and reliable performance. Perfect for everyday use.",
                    Category = cat,
                    Price = Math.Round((decimal)(random.NextDouble() * 200 + 5), 2),
                    Sku = $"SKU-{cat[..3].ToUpper()}-{i:D3}",
                    ImageUrl = $"https://images.shopglobal.example/products/{cat.ToLower()}-{i}.jpg",
                    Reviews = Enumerable.Range(0, random.Next(2, 8)).Select(r => new ProductReview
                    {
                        CustomerId = $"reviewer-{random.Next(100)}",
                        CustomerName = $"Reviewer {random.Next(100)}",
                        Rating = random.Next(3, 6),
                        Title = new[] { "Great product!", "Love it!", "Solid quality", "Worth the price", "Highly recommend" }[random.Next(5)],
                        Body = "This product exceeded my expectations. The quality is outstanding and I would definitely recommend it to others.",
                        Date = DateTime.UtcNow.AddDays(-random.Next(90))
                    }).ToList(),
                    InventoryCounts = regions.ToDictionary(r => r, _ => random.Next(10, 200))
                };

                productIds.Add(product.Id);
                await container.UpsertItemAsync(product, new PartitionKey("product"));
            }
        }

        // Seed the demo customer
        var customer = new Customer
        {
            Id = "demo-customer-001",
            FirstName = "Alex",
            LastName = "Johnson",
            Email = "alex.johnson@shopglobal.example",
            Phone = "+1-555-0142",
            Addresses =
            [
                new Address { Label = "Home", Street = "742 Evergreen Terrace", City = "Springfield", State = "IL", ZipCode = "62704", Country = "US" },
                new Address { Label = "Work", Street = "1600 Amphitheatre Pkwy", City = "Mountain View", State = "CA", ZipCode = "94043", Country = "US" }
            ],
            Preferences = new CustomerPreferences
            {
                Currency = "USD",
                Language = "en",
                Newsletter = true,
                FavoriteCategories = ["Electronics", "Books", "Home"]
            },
            PaymentMethods =
            [
                new PaymentMethod { Type = "Visa", Last4 = "4242", ExpiryMonth = 12, ExpiryYear = 2027, IsDefault = true },
                new PaymentMethod { Type = "Mastercard", Last4 = "8888", ExpiryMonth = 6, ExpiryYear = 2028, IsDefault = false }
            ],
            OrderHistory = []
        };
        await container.UpsertItemAsync(customer, new PartitionKey("customer"));

        // Seed inventory for each product
        foreach (var productId in productIds)
        {
            var stock = random.Next(50, 500);
            var inventory = new Inventory
            {
                ProductId = productId,
                ProductName = $"Product {productId}",
                TotalStock = stock,
                RegionStock = regions.ToDictionary(r => r, _ => random.Next(10, 200)),
                AuditLog =
                [
                    new StockAuditEntry { Action = "initial_stock", Region = "all", QuantityChange = stock, PreviousStock = 0, NewStock = stock, Reason = "Initial seeding" }
                ]
            };
            await container.UpsertItemAsync(inventory, new PartitionKey("inventory"));
        }

        // Seed 20 orders for the demo customer
        for (int i = 0; i < 20; i++)
        {
            var numItems = random.Next(1, 4);
            var lineItems = new List<OrderLineItem>();

            for (int j = 0; j < numItems; j++)
            {
                var pid = productIds[random.Next(productIds.Count)];
                var qty = random.Next(1, 3);
                var price = Math.Round((decimal)(random.NextDouble() * 150 + 10), 2);
                lineItems.Add(new OrderLineItem
                {
                    ProductId = pid,
                    Quantity = qty,
                    Price = price,
                    Subtotal = price * qty,
                    ProductSnapshot = new Product { Id = pid, Name = $"Product {pid}", Category = categories[random.Next(5)] }
                });
            }

            var order = new Order
            {
                CustomerId = "demo-customer-001",
                OrderDate = DateTime.UtcNow.AddDays(-random.Next(180)),
                Status = new[] { "Confirmed", "Shipped", "Delivered", "Delivered", "Delivered" }[random.Next(5)],
                TotalAmount = lineItems.Sum(li => li.Subtotal),
                Category = categories[random.Next(5)],
                LineItems = lineItems,
                CustomerSnapshot = new Customer { Id = "demo-customer-001", FirstName = "Alex", LastName = "Johnson", Email = "alex.johnson@shopglobal.example" },
                ShippingAddress = new Address { Street = "742 Evergreen Terrace", City = "Springfield", State = "IL", ZipCode = "62704", Country = "US" }
            };
            await container.UpsertItemAsync(order, new PartitionKey("order"));
        }

        return Ok(new
        {
            products = productIds.Count,
            customers = 1,
            inventory = productIds.Count,
            orders = 20,
            message = "Seeding complete. All documents created one at a time."
        });
    }

    [HttpGet("analytics/top-products")]
    public async Task<IActionResult> TopProducts()
    {
        return await _cosmosService.ExecuteWithRetry<IActionResult>(async () =>
        {
            var container = _cosmosService.GetContainer();
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30).ToString("o");

            // Intentionally loading ALL recent orders and aggregating client-side
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'order' AND c.orderDate > @startDate")
                .WithParameter("@startDate", thirtyDaysAgo);

            var iterator = container.GetItemQueryIterator<Order>(query);
            var allOrders = new List<Order>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                allOrders.AddRange(response);
            }

            // Client-side aggregation
            var topProducts = allOrders
                .SelectMany(o => o.LineItems)
                .GroupBy(li => li.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    TotalQuantity = g.Sum(li => li.Quantity),
                    TotalRevenue = g.Sum(li => li.Subtotal),
                    OrderCount = g.Count()
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(20)
                .ToList();

            return Ok(topProducts);
        });
    }

    [HttpGet("analytics/revenue")]
    public async Task<IActionResult> Revenue()
    {
        return await _cosmosService.ExecuteWithRetry<IActionResult>(async () =>
        {
            var container = _cosmosService.GetContainer();
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30).ToString("o");

            // Intentionally using SELECT * and doing aggregation client-side
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'order' AND c.orderDate > @startDate")
                .WithParameter("@startDate", thirtyDaysAgo);

            var iterator = container.GetItemQueryIterator<Order>(query);
            var allOrders = new List<Order>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                allOrders.AddRange(response);
            }

            var revenue = allOrders
                .GroupBy(o => o.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    Total = g.Count(),
                    Revenue = g.Sum(o => o.TotalAmount)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            return Ok(revenue);
        });
    }

    [HttpGet("analytics/active-customers")]
    public async Task<IActionResult> ActiveCustomers()
    {
        return await _cosmosService.ExecuteWithRetry<IActionResult>(async () =>
        {
            var container = _cosmosService.GetContainer();

            // Intentionally loading ALL orders to find active customers
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = 'order'");

            var iterator = container.GetItemQueryIterator<Order>(query);
            var allOrders = new List<Order>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                allOrders.AddRange(response);
            }

            var activeCustomers = allOrders
                .GroupBy(o => o.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    OrderCount = g.Count(),
                    TotalSpent = g.Sum(o => o.TotalAmount),
                    LastOrder = g.Max(o => o.OrderDate)
                })
                .OrderByDescending(x => x.OrderCount)
                .Take(20)
                .ToList();

            return Ok(activeCustomers);
        });
    }
}
