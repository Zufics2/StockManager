using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using StockManager.Models.Constants;
using StockManager.Models.Entities;
using StockManager.Models.Options;

namespace StockManager.Service.Data;

public static class DbSeeder
{
    /// <summary>Creates the roles and (when configured) the initial administrator.</summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var seed = services.GetRequiredService<IOptions<SeedOptions>>().Value;
        if (string.IsNullOrWhiteSpace(seed.AdminEmail) || string.IsNullOrWhiteSpace(seed.AdminPassword))
            return;

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync(seed.AdminEmail);
        if (admin is not null)
            return;

        admin = new ApplicationUser
        {
            UserName = seed.AdminEmail,
            Email = seed.AdminEmail,
            EmailConfirmed = true,
            FullName = seed.AdminFullName,
            CreatedAt = DateTime.UtcNow
        };

        var created = await userManager.CreateAsync(admin, seed.AdminPassword);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                "Could not create the seed administrator: " + string.Join("; ", created.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(admin, Roles.Admin);
    }
}
