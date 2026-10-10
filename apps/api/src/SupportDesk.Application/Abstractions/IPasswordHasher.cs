namespace SupportDesk.Application.Abstractions;

/// <summary>Hashing and checking passwords. Implemented with a vetted algorithm, never by hand.</summary>
public interface IPasswordHasher
{
    /// <summary>Hashes and salts a password for storage.</summary>
    string Hash(string password);

    /// <summary>
    /// True when the password matches the hash. A missing hash never matches, but still costs the
    /// same time as a real check, so a response cannot reveal whether an account exists.
    /// </summary>
    bool Verify(string? passwordHash, string password);
}