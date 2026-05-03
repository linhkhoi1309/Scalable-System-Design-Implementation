using Microsoft.EntityFrameworkCore;
using ProductApi.Domain.Entities;

namespace ProductApi.Infrastructure.Persistence;

public abstract class ProductDbContextBase : DbContext
{
    protected ProductDbContextBase(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Name).IsRequired().HasMaxLength(200);
            entity.Property(product => product.Price).HasPrecision(18, 2);
            entity.Property(product => product.CreatedAtUtc).IsRequired();
        });
    }
}

public sealed class WriteProductDbContext : ProductDbContextBase
{
    public WriteProductDbContext(DbContextOptions<WriteProductDbContext> options)
        : base(options)
    {
    }
}

public sealed class ReadProductDbContext : ProductDbContextBase
{
    public ReadProductDbContext(DbContextOptions<ReadProductDbContext> options)
        : base(options)
    {
    }
}
