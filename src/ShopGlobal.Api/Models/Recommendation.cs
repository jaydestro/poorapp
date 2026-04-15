using Newtonsoft.Json;

namespace ShopGlobal.Api.Models;

public class Recommendation
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("type")]
    public string Type { get; set; } = "recommendation";

    [JsonProperty("sourceProductId")]
    public string SourceProductId { get; set; } = string.Empty;

    [JsonProperty("sourceProductName")]
    public string SourceProductName { get; set; } = string.Empty;

    [JsonProperty("recommendedProducts")]
    public List<RecommendedProduct> RecommendedProducts { get; set; } = [];

    // Intentionally embedding full purchase history arrays
    [JsonProperty("purchaseHistory")]
    public List<PurchaseHistoryEntry> PurchaseHistory { get; set; } = [];

    [JsonProperty("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class RecommendedProduct
{
    [JsonProperty("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonProperty("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonProperty("score")]
    public double Score { get; set; }

    [JsonProperty("coPurchaseCount")]
    public int CoPurchaseCount { get; set; }
}

public class PurchaseHistoryEntry
{
    [JsonProperty("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("purchaseDate")]
    public DateTime PurchaseDate { get; set; }

    [JsonProperty("coProducts")]
    public List<string> CoProducts { get; set; } = [];
}
