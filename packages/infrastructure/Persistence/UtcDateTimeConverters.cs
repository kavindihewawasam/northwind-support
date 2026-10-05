using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SupportDesk.Infrastructure.Persistence;

/// <summary>
/// SQL Server's <c>datetime2</c> carries no time zone, so values read back from it arrive as
/// <see cref="DateTimeKind.Unspecified"/> and serialise without the trailing "Z". These
/// converters keep every timestamp explicitly UTC on the way in and on the way out.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            value => value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}

/// <inheritdoc cref="UtcDateTimeConverter"/>
public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            value => value.HasValue ? value.Value.ToUniversalTime() : value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value)
    {
    }
}
