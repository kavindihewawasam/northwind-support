using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>Signs an agent in with email and password and issues a token.</summary>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    IClock clock,
    ILogger<LoginCommandHandler> logger)
{
    /// <summary>The one message for every failure, so a caller cannot tell which part was wrong.</summary>
    public const string InvalidCredentialsMessage = "Invalid email or password.";

    /// <exception cref="UnauthorizedException">Unknown email, wrong password or inactive agent.</exception>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);

        // Always verify, even when there is no such agent, so the time taken does not give it away.
        var passwordMatches = passwordHasher.Verify(agent?.PasswordHash, request.Password);

        if (agent is null || !passwordMatches || !agent.IsActive)
        {
            logger.LogInformation("A sign-in attempt was rejected.");

            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        var token = tokenIssuer.Issue(agent, clock.UtcNow);

        return new LoginResponse(
            token.Token,
            token.ExpiresAtUtc,
            new CurrentUserDto(agent.Id, agent.FullName, agent.Email));
    }
}