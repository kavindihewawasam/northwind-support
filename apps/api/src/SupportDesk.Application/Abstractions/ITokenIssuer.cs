using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);

/// <summary>Creates the signed token an agent presents on every request after signing in.</summary>
public interface ITokenIssuer
{
    IssuedToken Issue(Agent agent, DateTime nowUtc);
}