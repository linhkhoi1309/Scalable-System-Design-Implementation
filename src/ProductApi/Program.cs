using ProductApi.Features.Health;
using ProductApi.Features.Products;
using ProductApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

await app.InitializeDatabasesAsync();

app.MapHealthEndpoints();
app.MapProductEndpoints();

await app.RunAsync();
