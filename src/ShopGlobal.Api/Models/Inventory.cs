using Newtonsoft.Json;

namespace ShopGlobal.Api.Models;

public class Inventory
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("type")]
    public string Type { get; set; } = "inventory";

    [JsonProperty("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonProperty("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonProperty("totalStock")]
    public int TotalStock { get; set; }

    [JsonProperty("regionStock")]
    public Dictionary<string, int> RegionStock { get; set; } = new();

    [JsonProperty("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Intentionally embedding unbounded audit log
    [JsonProperty("auditLog")]
    public List<StockAuditEntry> AuditLog { get; set; } = [];
}

public class StockAuditEntry
{
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonProperty("action")]
    public string Action { get; set; } = string.Empty;

    [JsonProperty("region")]
    public string Region { get; set; } = string.Empty;

    [JsonProperty("quantityChange")]
    public int QuantityChange { get; set; }

    [JsonProperty("previousStock")]
    public int PreviousStock { get; set; }

    [JsonProperty("newStock")]
    public int NewStock { get; set; }

    [JsonProperty("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class StockAdjustmentRequest
{
    public string Region { get; set; } = string.Empty;
    public int QuantityChange { get; set; }
    public string Reason { get; set; } = string.Empty;
}
