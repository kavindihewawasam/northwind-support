using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Infrastructure.Security;

/// <summary>
/// Password hashing with ASP.NET Identity's <see cref="PasswordHasher{TUser}"/>: PBKDF2 with a
/// random salt per password and a stored iteration count. Nothing here is home-made.
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Subject = new();

    // Checked against when there is no real hash, so "no such account" takes as long as "wrong password".
    private static readonly string DummyHash = Hasher.HashPassword(Subject, Guid.NewGuid().ToString("N"));

    public string Hash(string password) => Hasher.HashPassword(Subject, password);

    public bool Verify(string? passwordHash, string password)
    {
        var result = Hasher.VerifyHashedPassword(Subject, passwordHash ?? DummyHash, password);

        return passwordHash is not null && result != PasswordVerificationResult.Failed;
    }
}