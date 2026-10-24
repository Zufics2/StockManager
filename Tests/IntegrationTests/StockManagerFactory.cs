using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StockManager.Abstract;
using StockManager.Service.Data;

namespace Tests.IntegrationTests;

/// <summary>
/// Boots the real application (controllers, middleware, JWT, validation, MediatR ...) in memory.
/// Only PostgreSQL is swapped for the EF in-memory provider and the external currency API is mocked.
/// </summary>
public class StockManagerFactory : WebApplicationFactory<StockManager.Program>
{
    private readonly string _dbName = $"integration-{Guid.NewGuid()}";

    public Mock<ICurrencyService> Currency { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Development" => appsettings.Development.json seeds admin@stockmanager.local / Admin123!
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // remove every registration that ties ApplicationDbContext to Npgsql
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions>();
            var optionConfigurations = services
                .Where(d => d.ServiceType.IsGenericType
                            && d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")
                            && d.ServiceType.GenericTypeArguments[0] == typeof(ApplicationDbContext))
                .ToList();
            foreach (var descriptor in optionConfigurations)
                services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(_dbName));

            // never call the real exchange-rate provider from tests
            services.RemoveAll<ICurrencyService>();
            services.AddSingleton(Currency.Object);
        });
    }
}
