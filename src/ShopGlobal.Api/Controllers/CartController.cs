using Microsoft.AspNetCore.Mvc;
using ShopGlobal.Api.Models;
using ShopGlobal.Api.Services;

namespace ShopGlobal.Api.Controllers;

[ApiController]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly CartService _cartService;

    public CartController(CartService cartService)
    {
        _cartService = cartService;
    }

    [HttpGet("{customerId}")]
    public async Task<IActionResult> GetCart(string customerId)
    {
        var cart = await _cartService.GetCartAsync(customerId);
        return Ok(cart);
    }

    [HttpPost("{customerId}/items")]
    public async Task<IActionResult> AddItem(string customerId, [FromBody] CartItem item)
    {
        var cart = await _cartService.AddItemAsync(customerId, item);
        return Ok(cart);
    }

    [HttpDelete("{customerId}/items/{itemId}")]
    public async Task<IActionResult> RemoveItem(string customerId, string itemId)
    {
        var cart = await _cartService.RemoveItemAsync(customerId, itemId);
        return Ok(cart);
    }

    [HttpPost("{customerId}/checkout")]
    public async Task<IActionResult> Checkout(string customerId)
    {
        var order = await _cartService.CheckoutAsync(customerId);
        return Ok(order);
    }
}
