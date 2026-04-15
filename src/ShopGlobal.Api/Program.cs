using ShopGlobal.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Intentionally registering as Transient so a new instance is created every time
builder.Services.AddTransient<CosmosService>();
builder.Services.AddTransient<CartService>();
builder.Services.AddTransient<ProductService>();
builder.Services.AddTransient<CustomerService>();
builder.Services.AddTransient<InventoryService>();
builder.Services.AddTransient<RecommendationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Initialize database and container on startup
using (var scope = app.Services.CreateScope())
{
    var cosmosService = scope.ServiceProvider.GetRequiredService<CosmosService>();
    await cosmosService.InitializeDatabaseAsync();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

app.Run();
