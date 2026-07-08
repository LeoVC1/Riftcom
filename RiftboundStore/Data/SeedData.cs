using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RiftboundStore.Models;

namespace RiftboundStore.Data;

public static class SeedData
{
    public const string AdminRole = "Admin";
    public const string CustomerRole = "Customer";

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

        var db = provider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AdminRole, CustomerRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var adminEmail = configuration["Admin:Email"];
        var adminPassword = configuration["Admin:Password"];
        var adminDisplayName = configuration["Admin:DisplayName"];

        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            logger.LogInformation("Admin:Email não configurado — seed do admin ignorado.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            // Only create the admin if a password was explicitly provided (via appsettings,
            // user-secrets, env var Admin__Password, etc.). Otherwise skip — better than
            // baking a weak default into the seed.
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                logger.LogWarning(
                    "Admin '{Email}' não existe e Admin:Password não está configurado. " +
                    "Defina a variável de ambiente Admin__Password (ou user-secrets) e reinicie " +
                    "para criar o admin inicial.",
                    adminEmail);
                return;
            }

            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                DisplayName = string.IsNullOrWhiteSpace(adminDisplayName) ? adminEmail : adminDisplayName
            };

            // Bypass password validators for the initial seed only; still hashed by the
            // Identity password hasher (PBKDF2). Public registration keeps the strong policy.
            admin.PasswordHash = userManager.PasswordHasher.HashPassword(admin, adminPassword);
            admin.SecurityStamp = Guid.NewGuid().ToString();
            var result = await userManager.CreateAsync(admin);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Falha ao criar admin: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
            logger.LogInformation("Admin inicial '{Email}' criado pelo seed.", adminEmail);
        }

        if (!await userManager.IsInRoleAsync(admin, AdminRole))
        {
            await userManager.AddToRoleAsync(admin, AdminRole);
        }
    }
}
