namespace ProductApi.Domain.Entities;

public sealed class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
