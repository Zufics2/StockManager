using Microsoft.EntityFrameworkCore;
using StockManager.Service.Data;
using StockManager.Service.Extensions;
using StockManager.Service.Middlewares;
using ServiceCollectionExtensions = StockManager.Service.Extensions.ServiceCollectionExtensions;

namespace StockManager;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var config = builder.Configuration;
        
        builder.Services
            .AddApiControllers()
            .AddPersistence(config)
            .AddApplicationServices(config)
            .AddCurrencyClient(config)
            .AddJwtAuthentication(config)
            .AddCorsPolicy(config)
            .AddRateLimiting(config)
            .AddSwaggerWithJwt();

        var app = builder.Build();
        
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (db.Database.IsRelational())
            {
                if (config.GetValue("Database:ApplyMigrationsOnStartup", true))
                    await db.Database.MigrateAsync();
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
            }

            await DbSeeder.SeedAsync(scope.ServiceProvider);
        }
        
        app.UseMiddleware<LoggingMiddleware>(); 
        app.UseMiddleware<ExceptionHandlerMiddleware>(); 

        if (app.Environment.IsDevelopment() || config.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "StockManager API v1"));
        }

        if (!app.Environment.IsDevelopment())
            app.UseHttpsRedirection();

        app.UseStaticFiles();                            
        app.UseCors(ServiceCollectionExtensions.CorsPolicy);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        await app.RunAsync();
    }
}
