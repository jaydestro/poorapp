using Newtonsoft.Json;

namespace ShopGlobal.Api.Models;

public class Customer
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("type")]
    public string Type { get; set; } = "customer";

    [JsonProperty("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("phone")]
    public string Phone { get; set; } = string.Empty;

    [JsonProperty("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [JsonProperty("addresses")]
    public List<Address> Addresses { get; set; } = [];

    [JsonProperty("preferences")]
    public CustomerPreferences Preferences { get; set; } = new();

    [JsonProperty("paymentMethods")]
    public List<PaymentMethod> PaymentMethods { get; set; } = [];

    // Intentionally embedding full order history in the customer document
    [JsonProperty("orderHistory")]
    public List<Order> OrderHistory { get; set; } = [];
}

public class Address
{
    [JsonProperty("label")]
    public string Label { get; set; } = string.Empty;

    [JsonProperty("street")]
    public string Street { get; set; } = string.Empty;

    [JsonProperty("city")]
    public string City { get; set; } = string.Empty;

    [JsonProperty("state")]
    public string State { get; set; } = string.Empty;

    [JsonProperty("zipCode")]
    public string ZipCode { get; set; } = string.Empty;

    [JsonProperty("country")]
    public string Country { get; set; } = string.Empty;
}

public class CustomerPreferences
{
    [JsonProperty("currency")]
    public string Currency { get; set; } = "USD";

    [JsonProperty("language")]
    public string Language { get; set; } = "en";

    [JsonProperty("newsletter")]
    public bool Newsletter { get; set; } = true;

    [JsonProperty("favoriteCategories")]
    public List<string> FavoriteCategories { get; set; } = [];
}

public class PaymentMethod
{
    [JsonProperty("methodId")]
    public string MethodId { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("last4")]
    public string Last4 { get; set; } = string.Empty;

    [JsonProperty("expiryMonth")]
    public int ExpiryMonth { get; set; }

    [JsonProperty("expiryYear")]
    public int ExpiryYear { get; set; }

    [JsonProperty("isDefault")]
    public bool IsDefault { get; set; }
}
