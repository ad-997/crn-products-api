using Application.DTOs;
using Domain.Entities;

namespace Application.Interfaces;

public interface IProductRepository
{
    Task<Page<ProductDto>> ListAsync(Paging p, CancellationToken ct);
    Task<Product?> FindAsync(int id, CancellationToken ct, bool tracking = true);
    Task AddAsync(Product product, CancellationToken ct);
    void Remove(Product product);
    Task<Page<ItemDto>> ItemsAsync(int id, Paging p, CancellationToken ct);
    Task<Item?> FindItemAsync(int productId, int itemId, CancellationToken ct);
    Task AddItemAsync(Item item, CancellationToken ct);
    void RemoveItem(Item item);
    Task SaveAsync(CancellationToken ct);
}

public interface IProductService
{
    Task<Page<ProductDto>> ListAsync(Paging p, CancellationToken ct);
    Task<ProductDto> GetAsync(int id, CancellationToken ct);
    Task<ProductDto> CreateAsync(ProductWrite request, string actor, CancellationToken ct);
    Task UpdateAsync(int id, ProductWrite request, string actor, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
    Task<Page<ItemDto>> ItemsAsync(int id, Paging p, CancellationToken ct);
    Task<ItemDto> AddItemAsync(int id, ItemWrite request, CancellationToken ct);
    Task UpdateItemAsync(int id, int itemId, ItemWrite request, CancellationToken ct);
    Task DeleteItemAsync(int id, int itemId, CancellationToken ct);
}

public interface IAuthService
{
    Task<TokenPair> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<TokenPair> RefreshAsync(string token, CancellationToken ct);
    Task RevokeAsync(string token, CancellationToken ct);
}

public class NotFoundException(string message) : Exception(message);

public class AuthenticationException() : Exception("Invalid credentials or refresh token.");
