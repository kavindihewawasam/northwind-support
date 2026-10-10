namespace SupportDesk.Presentation.Security;

/// <summary>
/// Token settings, bound from the "Jwt" configuration section. The signing key is never in code:
/// Development has a local fixture in appsettings.Development.json, and every other environment
/// must supply Jwt__SigningKey.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    /// <summary>How long a token works. There are no refresh tokens, so this is how long a session lasts.</summary>
    public int LifetimeMinutes { get; set; } = 60;
}