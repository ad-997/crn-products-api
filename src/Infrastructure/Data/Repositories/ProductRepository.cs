using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public class ProductRepository(ApplicationDbContext db) : IProductRepository
{
    public async Task<Page<ProductDto>> ListAsync(Paging p, CancellationToken ct)
    {
        var q = db.Products.AsNoTracking();
        var count = await q.CountAsync(ct);
        var data = await q.OrderBy(x => x.Id)
            .Skip((p.PageNumber - 1) * p.PageSize)
            .Take(p.PageSize)
            .Select(x => new ProductDto(
                x.Id,
                x.ProductName,
                x.CreatedBy,
                x.CreatedOn,
                x.ModifiedBy,
                x.ModifiedOn
            ))
            .ToListAsync(ct);
        return new(data, p.PageNumber, p.PageSize, count);
    }

    public Task<Product?> FindAsync(int id, CancellationToken ct, bool tracking = true) =>
        (tracking ? db.Products : db.Products.AsNoTracking()).SingleOrDefaultAsync(
            x => x.Id == id,
            ct
        );

    public async Task AddAsync(Product p, CancellationToken ct) =>
        await db.Products.AddAsync(p, ct);

    public void Remove(Product p) => db.Products.Remove(p);

    public async Task<Page<ItemDto>> ItemsAsync(int id, Paging p, CancellationToken ct)
    {
        var q = db.Items.AsNoTracking().Where(x => x.ProductId == id);
        var count = await q.CountAsync(ct);
        return new(
            await q.OrderBy(x => x.Id)
                .Skip((p.PageNumber - 1) * p.PageSize)
                .Take(p.PageSize)
                .Select(x => new ItemDto(x.Id, x.ProductId, x.Quantity))
                .ToListAsync(ct),
            p.PageNumber,
            p.PageSize,
            count
        );
    }

    public Task<Item?> FindItemAsync(int id, int itemId, CancellationToken ct) =>
        db.Items.SingleOrDefaultAsync(x => x.Id == itemId && x.ProductId == id, ct);

    public async Task AddItemAsync(Item i, CancellationToken ct) => await db.Items.AddAsync(i, ct);

    public void RemoveItem(Item i) => db.Items.Remove(i);

    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
