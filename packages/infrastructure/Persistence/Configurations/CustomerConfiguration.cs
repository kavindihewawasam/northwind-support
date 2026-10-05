using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Entities;

namespace SupportDesk.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", t =>
            t.HasCheckConstraint("CK_Customers_Tier", "[Tier] IN (1, 2)"));

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();

        builder.Property(c => c.Email).HasMaxLength(256).IsRequired();

        builder.Property(c => c.Phone).HasMaxLength(50);

        builder.Property(c => c.Tier).HasConversion<int>();

        builder.Property(c => c.CreatedAtUtc).HasColumnType("datetime2(3)");

        builder.HasIndex(c => c.Email).IsUnique().HasDatabaseName("UQ_Customers_Email");
    }
}
