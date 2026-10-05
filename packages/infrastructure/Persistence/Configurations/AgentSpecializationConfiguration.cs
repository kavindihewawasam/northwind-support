using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Entities;

namespace SupportDesk.Infrastructure.Persistence.Configurations;

public sealed class AgentSpecializationConfiguration : IEntityTypeConfiguration<AgentSpecialization>
{
    public void Configure(EntityTypeBuilder<AgentSpecialization> builder)
    {
        builder.ToTable("AgentSpecializations");

        builder.HasKey(s => new { s.AgentId, s.CategoryId });

        builder.HasOne(s => s.Agent)
            .WithMany(a => a.Specializations)
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Category)
            .WithMany(c => c.Specialists)
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.CategoryId)
            .HasDatabaseName("IX_AgentSpecializations_CategoryId")
            .IncludeProperties(s => s.AgentId);
    }
}
