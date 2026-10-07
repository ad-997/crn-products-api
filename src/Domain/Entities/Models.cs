namespace Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string ProductName { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedOn { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public List<Item> Items { get; set; } = [];
}

public class Item
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Reader";
}

public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Hash { get; set; } = "";
    public Guid Family { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public byte[] Version { get; set; } = [];
}
