using Microsoft.AspNetCore.Mvc;
using ShopGlobal.Api.Models;
using ShopGlobal.Api.Services;

namespace ShopGlobal.Api.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomerController : ControllerBase
{
    private readonly CustomerService _customerService;

    public CustomerController(CustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCustomer(string id)
    {
        var customer = await _customerService.GetCustomerAsync(id);
        if (customer == null) return NotFound();
        return Ok(customer);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCustomer(string id, [FromBody] Customer customer)
    {
        var updated = await _customerService.UpdateCustomerAsync(id, customer);
        return Ok(updated);
    }

    [HttpGet("{id}/orders")]
    public async Task<IActionResult> GetOrders(string id)
    {
        var orders = await _customerService.GetCustomerOrdersAsync(id);
        return Ok(orders);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchByEmail([FromQuery] string email)
    {
        var customer = await _customerService.FindCustomerByEmailAsync(email);
        if (customer == null) return NotFound();
        return Ok(customer);
    }
}
