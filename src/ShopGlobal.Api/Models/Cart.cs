using Newtonsoft.Json;

namespace ShopGlobal.Api.Models;

public class Cart
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("type")]
    public string Type { get; set; } = "cart";

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [JsonProperty("updatedDate")]
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    // Intentionally embedding full product snapshots, not references
    [JsonProperty("items")]
    public List<CartItem> Items { get; set; } = [];

    [JsonProperty("totalAmount")]
    public decimal TotalAmount { get; set; }
}

public class CartItem
{
    [JsonProperty("itemId")]
    public string ItemId { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("priceAtAdd")]
    public decimal PriceAtAdd { get; set; }

    // Full product snapshot embedded
    [JsonProperty("productSnapshot")]
    public Product ProductSnapshot { get; set; } = new();
}
