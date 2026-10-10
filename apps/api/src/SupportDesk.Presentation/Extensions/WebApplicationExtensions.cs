using Microsoft.EntityFrameworkCore;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Applies pending migrations, seeds the demo data and gives agents their development password.
    /// Development convenience only - a real deployment would run migrations as a separate,
    /// deliberate step and would never seed a known password.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<SupportDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<SupportDbSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<AgentCredentialSeeder>().EnsureAsync();
    }
}