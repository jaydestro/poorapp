using Newtonsoft.Json;

namespace ShopGlobal.Api.Models;

public class Product
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("type")]
    public string Type { get; set; } = "product";

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    [JsonProperty("price")]
    public decimal Price { get; set; }

    [JsonProperty("sku")]
    public string Sku { get; set; } = string.Empty;

    [JsonProperty("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    [JsonProperty("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Intentionally embedding ALL reviews in the product document
    [JsonProperty("reviews")]
    public List<ProductReview> Reviews { get; set; } = [];

    // Intentionally embedding recommendation links
    [JsonProperty("recommendationLinks")]
    public List<string> RecommendationLinks { get; set; } = [];

    // Intentionally embedding per-region inventory counts
    [JsonProperty("inventoryCounts")]
    public Dictionary<string, int> InventoryCounts { get; set; } = new();
}

public class ProductReview
{
    [JsonProperty("reviewId")]
    public string ReviewId { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonProperty("rating")]
    public int Rating { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("body")]
    public string Body { get; set; } = string.Empty;

    [JsonProperty("date")]
    public DateTime Date { get; set; } = DateTime.UtcNow;
}
