using Newtonsoft.Json;

namespace ShopGlobal.Api.Models;

public class Order
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("type")]
    public string Type { get; set; } = "order";

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("orderDate")]
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    [JsonProperty("status")]
    public string Status { get; set; } = "Pending";

    [JsonProperty("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonProperty("shippingAddress")]
    public Address ShippingAddress { get; set; } = new();

    // Intentionally embedding full customer profile snapshot
    [JsonProperty("customerSnapshot")]
    public Customer CustomerSnapshot { get; set; } = new();

    // Intentionally embedding full product details for every line item
    [JsonProperty("lineItems")]
    public List<OrderLineItem> LineItems { get; set; } = [];

    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;
}

public class OrderLineItem
{
    [JsonProperty("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("price")]
    public decimal Price { get; set; }

    [JsonProperty("subtotal")]
    public decimal Subtotal { get; set; }

    // Full product snapshot embedded
    [JsonProperty("productSnapshot")]
    public Product ProductSnapshot { get; set; } = new();
}
