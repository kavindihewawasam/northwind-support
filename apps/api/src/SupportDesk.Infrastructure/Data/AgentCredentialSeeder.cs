using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Data;

/// <summary>
/// Gives every agent that has no password the development password from configuration
/// (<c>Seed:AgentPassword</c>), hashed here and never stored as text. Runs on every start, so a
/// database seeded before sign-in existed gets credentials too. Only called in Development.
/// </summary>
public sealed class AgentCredentialSeeder(
    SupportDbContext db,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    ILogger<AgentCredentialSeeder> logger)
{
    public async Task EnsureAsync(CancellationToken ct = default)
    {
        var password = configuration["Seed:AgentPassword"];

        if (string.IsNullOrEmpty(password))
        {
            logger.LogInformation("No Seed:AgentPassword is configured; agents without a password stay locked out.");
            return;
        }

        var agents = await db.Set<Agent>().Where(a => a.PasswordHash == null).ToListAsync(ct);

        foreach (var agent in agents)
        {
            agent.SetPasswordHash(passwordHasher.Hash(password));
        }

        if (agents.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Set the development password for {Count} agents.", agents.Count);
        }
    }
}