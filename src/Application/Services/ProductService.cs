using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;

namespace Application.Services;

/// <summary>Coordinates validation, auditing and persistence for product operations.</summary>
public class ProductService(
    IProductRepository repository,
    IValidator<ProductWrite> products,
    IValidator<ItemWrite> items,
    IValidator<Paging> paging
) : IProductService
{
    static ProductDto Map(Product p) =>
        new(p.Id, p.ProductName, p.CreatedBy, p.CreatedOn, p.ModifiedBy, p.ModifiedOn);

    async Task<Product> Required(int id, CancellationToken ct) =>
        await repository.FindAsync(id, ct) ?? throw new NotFoundException("Product not found.");

    public async Task<Page<ProductDto>> ListAsync(Paging p, CancellationToken ct)
    {
        await paging.ValidateAndThrowAsync(p, ct);
        return await repository.ListAsync(p, ct);
    }

    public async Task<ProductDto> GetAsync(int id, CancellationToken ct) =>
        Map(
            await repository.FindAsync(id, ct, false)
                ?? throw new NotFoundException("Product not found.")
        );

    public async Task<ProductDto> CreateAsync(
        ProductWrite request,
        string actor,
        CancellationToken ct
    )
    {
        await products.ValidateAndThrowAsync(request, ct);
        var p = new Product
        {
            ProductName = request.ProductName.Trim(),
            CreatedBy = actor,
            CreatedOn = DateTime.UtcNow,
        };
        await repository.AddAsync(p, ct);
        await repository.SaveAsync(ct);
        return Map(p);
    }

    public async Task UpdateAsync(int id, ProductWrite request, string actor, CancellationToken ct)
    {
        await products.ValidateAndThrowAsync(request, ct);
        var p = await Required(id, ct);
        p.ProductName = request.ProductName.Trim();
        p.ModifiedBy = actor;
        p.ModifiedOn = DateTime.UtcNow;
        await repository.SaveAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        repository.Remove(await Required(id, ct));
        await repository.SaveAsync(ct);
    }

    public async Task<Page<ItemDto>> ItemsAsync(int id, Paging p, CancellationToken ct)
    {
        await paging.ValidateAndThrowAsync(p, ct);
        _ =
            await repository.FindAsync(id, ct, false)
            ?? throw new NotFoundException("Product not found.");
        return await repository.ItemsAsync(id, p, ct);
    }

    public async Task<ItemDto> AddItemAsync(int id, ItemWrite request, CancellationToken ct)
    {
        await items.ValidateAndThrowAsync(request, ct);
        await Required(id, ct);
        var i = new Item { ProductId = id, Quantity = request.Quantity };
        await repository.AddItemAsync(i, ct);
        await repository.SaveAsync(ct);
        return new(i.Id, id, i.Quantity);
    }

    async Task<Item> RequiredItem(int id, int itemId, CancellationToken ct) =>
        await repository.FindItemAsync(id, itemId, ct)
        ?? throw new NotFoundException("Item not found for this product.");

    public async Task UpdateItemAsync(int id, int itemId, ItemWrite request, CancellationToken ct)
    {
        await items.ValidateAndThrowAsync(request, ct);
        var i = await RequiredItem(id, itemId, ct);
        i.Quantity = request.Quantity;
        await repository.SaveAsync(ct);
    }

    public async Task DeleteItemAsync(int id, int itemId, CancellationToken ct)
    {
        repository.RemoveItem(await RequiredItem(id, itemId, ct));
        await repository.SaveAsync(ct);
    }
}
