using Microsoft.AspNetCore.Mvc;
using ShopGlobal.Api.Models;
using ShopGlobal.Api.Services;

namespace ShopGlobal.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly ProductService _productService;
    private readonly RecommendationService _recommendationService;

    public ProductController(ProductService productService, RecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
        _productService = productService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        var results = await _productService.SearchProductsAsync(q);
        return Ok(results);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProduct(string id)
    {
        var product = await _productService.GetProductAsync(id);
        if (product == null) return NotFound();

        // Intentionally rebuilding recommendations on every product view
        try { await _recommendationService.RebuildRecommendationsAsync(); } catch { }

        return Ok(product);
    }

    [HttpGet("category/{category}")]
    public async Task<IActionResult> GetByCategory(string category)
    {
        var results = await _productService.GetProductsByCategoryAsync(category);
        return Ok(results);
    }

    [HttpGet("{id}/recommendations")]
    public async Task<IActionResult> GetRecommendations(string id)
    {
        var recommendations = await _recommendationService.GetRecommendationsAsync(id);
        return Ok(recommendations);
    }
}
