using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductApi.Infrastructure.Persistence;

namespace ProductApi.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var writeConnectionString = configuration.GetConnectionString("WriteDb")
            ?? throw new InvalidOperationException("Connection string 'WriteDb' was not found.");
        var readConnectionString = configuration.GetConnectionString("ReadDb")
            ?? writeConnectionString;

        services.AddDbContext<WriteProductDbContext>(options => options.UseNpgsql(writeConnectionString));
        services.AddDbContext<ReadProductDbContext>(options => options.UseNpgsql(readConnectionString));

        return services;
    }
}
