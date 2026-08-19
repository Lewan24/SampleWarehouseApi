using Microsoft.AspNetCore.Identity;
using WarehouseApi.Common;
using WarehouseApi.Models;

namespace WarehouseApi.Data;

/// <summary>
/// Ensures the three roles exist and, if Seed:AdminEmail / Seed:AdminPassword are
/// configured (user-secrets or environment variables — never appsettings.json in
/// a real deployment), bootstraps a single Admin account so there's a way into the
/// system on first run. If those settings are absent, seeding is skipped rather
/// than falling back to a hardcoded default — a hardcoded admin credential is
/// exactly the kind of thing that ends up in production by accident.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = configuration["Seed:AdminEmail"];
        var adminPassword = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning(
                "Seed:AdminEmail / Seed:AdminPassword are not configured — skipping admin seeding. " +
                "Set them via 'dotnet user-secrets' or environment variables to bootstrap an admin account.");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await userManager.FindByEmailAsync(adminEmail);
        if (existing is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, Roles.Admin);
            logger.LogInformation("Seeded initial admin user {Email}. Change this password after first login.", adminEmail);
        }
        else
        {
            logger.LogError(
                "Failed to seed admin user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
