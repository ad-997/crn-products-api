using Application.DTOs;
using Application.Interfaces;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController, ApiVersion("1.0"), Route("api/v{version:apiVersion}/products"), Authorize]
public class ProductsController(IProductService service) : ControllerBase
{
    /// <summary>Lists products with bounded pagination.</summary>
    [HttpGet]
    public async Task<ActionResult<Page<ProductDto>>> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) => Ok(await service.ListAsync(new(pageNumber, pageSize), ct));

    /// <summary>Returns one product by its identifier.</summary>
    [HttpGet("{id:int:min(1)}")]
    public async Task<ActionResult<ProductDto>> Get(int id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    /// <summary>Creates a product with server-generated audit fields (Admin).</summary>
    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Create(ProductWrite request, CancellationToken ct)
    {
        var p = await service.CreateAsync(request, User.Identity!.Name!, ct);
        return CreatedAtAction(nameof(Get), new { id = p.Id, version = "1" }, p);
    }

    /// <summary>Updates a product name and modified audit fields (Admin).</summary>
    [HttpPut("{id:int:min(1)}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, ProductWrite request, CancellationToken ct)
    {
        await service.UpdateAsync(id, request, User.Identity!.Name!, ct);
        return NoContent();
    }

    /// <summary>Deletes a product and all related items (Admin).</summary>
    [HttpDelete("{id:int:min(1)}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Lists the items belonging to a product.</summary>
    [HttpGet("{id:int:min(1)}/items")]
    public async Task<ActionResult<Page<ItemDto>>> Items(
        int id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    ) => Ok(await service.ItemsAsync(id, new(pageNumber, pageSize), ct));

    /// <summary>Adds a nonnegative-quantity item to a product (Admin).</summary>
    [HttpPost("{id:int:min(1)}/items"), Authorize(Roles = "Admin")]
    public async Task<ActionResult<ItemDto>> AddItem(
        int id,
        ItemWrite request,
        CancellationToken ct
    )
    {
        var i = await service.AddItemAsync(id, request, ct);
        return Created($"/api/v1/products/{id}/items", i);
    }

    /// <summary>Updates an item belonging to the specified product (Admin).</summary>
    [HttpPut("{id:int:min(1)}/items/{itemId:int:min(1)}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateItem(
        int id,
        int itemId,
        ItemWrite request,
        CancellationToken ct
    )
    {
        await service.UpdateItemAsync(id, itemId, request, ct);
        return NoContent();
    }

    /// <summary>Deletes an item belonging to the specified product (Admin).</summary>
    [HttpDelete("{id:int:min(1)}/items/{itemId:int:min(1)}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteItem(int id, int itemId, CancellationToken ct)
    {
        await service.DeleteItemAsync(id, itemId, ct);
        return NoContent();
    }
}
