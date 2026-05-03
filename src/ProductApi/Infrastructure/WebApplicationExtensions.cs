using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductApi.Infrastructure.Persistence;

namespace ProductApi.Infrastructure;

public static class WebApplicationExtensions
{
    public static async Task InitializeDatabasesAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var writeDb = scope.ServiceProvider.GetRequiredService<WriteProductDbContext>();
        var readDb = scope.ServiceProvider.GetRequiredService<ReadProductDbContext>();

        await writeDb.Database.EnsureCreatedAsync();

        if (!string.Equals(writeDb.Database.GetConnectionString(), readDb.Database.GetConnectionString(), StringComparison.OrdinalIgnoreCase))
        {
            await readDb.Database.EnsureCreatedAsync();
        }
    }
}
