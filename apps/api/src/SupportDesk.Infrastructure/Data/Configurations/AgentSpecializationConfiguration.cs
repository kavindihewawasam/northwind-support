using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Infrastructure.Data.Configurations;

/// <summary>
/// The categories an agent is qualified for. Part of the agent aggregate (see
/// <see cref="AgentConfiguration"/>); the category is referenced by id.
/// </summary>
public sealed class AgentSpecializationConfiguration : IEntityTypeConfiguration<AgentSpecialization>
{
    public void Configure(EntityTypeBuilder<AgentSpecialization> builder)
    {
        builder.ToTable("AgentSpecializations");

        builder.HasKey(s => new { s.AgentId, s.CategoryId });

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.CategoryId)
            .HasDatabaseName("IX_AgentSpecializations_CategoryId")
            .IncludeProperties(s => s.AgentId);
    }
}
