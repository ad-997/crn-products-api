namespace Application.DTOs;

public record ProductWrite(string ProductName);

public record ItemWrite(int Quantity);

public record ProductDto(
    int Id,
    string ProductName,
    string CreatedBy,
    DateTime CreatedOn,
    string? ModifiedBy,
    DateTime? ModifiedOn
);

public record ItemDto(int Id, int ProductId, int Quantity);

public record Page<T>(IReadOnlyList<T> Data, int PageNumber, int PageSize, int TotalCount);

public record Paging(int PageNumber = 1, int PageSize = 20);

public record LoginRequest(string Username, string Password);

public record RefreshRequest(string RefreshToken);

public record TokenPair(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt);
