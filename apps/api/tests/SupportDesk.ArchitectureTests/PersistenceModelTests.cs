using Microsoft.EntityFrameworkCore;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.ArchitectureTests;

/// <summary>
/// Catches a change to the EF Core mapping that was made without adding a migration, before
/// it reaches a database.
/// </summary>
public class PersistenceModelTests
{
    [Fact]
    public void TheModel_MatchesTheLatestMigration()
    {
        // The connection string is never opened: comparing the model needs no database.
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=unused;Database=unused")
            .Options;

        using var db = new SupportDbContext(options);

        Assert.False(
            db.Database.HasPendingModelChanges(),
            "The EF model differs from the migrations. Add one with: npm run ef -- migrations add <Name>");
    }
}
