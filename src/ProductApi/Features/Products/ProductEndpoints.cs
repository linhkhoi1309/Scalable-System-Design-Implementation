using Microsoft.EntityFrameworkCore;
using ProductApi.Domain.Entities;
using ProductApi.Infrastructure.Persistence;

namespace ProductApi.Features.Products;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/products", CreateProductAsync);
        endpoints.MapGet("/products", GetProductsAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateProductAsync(
        CreateProductRequest request,
        WriteProductDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = ["Product name is required."]
            });
        }

        if (request.Price <= 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Price)] = ["Product price must be greater than zero."]
            });
        }

        var product = new Product
        {
            Name = request.Name.Trim(),
            Price = request.Price,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created("/products", new
        {
            message = "Product created successfully.",
            processed_by = GetServerId(configuration),
            product = new ProductDto(product.Id, product.Name, product.Price, product.CreatedAtUtc)
        });
    }

    private static async Task<IResult> GetProductsAsync(
        ReadProductDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var products = await db.Products
            .AsNoTracking()
            .OrderByDescending(product => product.CreatedAtUtc)
            .Select(product => new ProductDto(product.Id, product.Name, product.Price, product.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            processed_by = GetServerId(configuration),
            products
        });
    }

    private static string GetServerId(IConfiguration configuration)
    {
        return configuration["ServerId"]
            ?? Environment.GetEnvironmentVariable("SERVER_ID")
            ?? Environment.GetEnvironmentVariable("NODE_ID")
            ?? "Node_A";
    }

    private sealed record ProductDto(int Id, string Name, decimal Price, DateTime CreatedAtUtc);
}
