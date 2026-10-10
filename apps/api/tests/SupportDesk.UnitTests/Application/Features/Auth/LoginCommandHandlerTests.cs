using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private const string Email = "alex.turner@example.com";

    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenIssuer> _tokens = new();
    private readonly FixedClock _clock = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _tokens.Setup(t => t.Issue(It.IsAny<Agent>(), It.IsAny<DateTime>()))
            .Returns((Agent _, DateTime now) => new IssuedToken("token-123", now.AddMinutes(60)));

        _hasher.Setup(h => h.Verify("stored-hash", "right-password")).Returns(true);

        _handler = new LoginCommandHandler(
            _agents.Object, _hasher.Object, _tokens.Object, _clock, NullLogger<LoginCommandHandler>.Instance);
    }

    private Agent ExistingAgent(bool active = true)
    {
        var agent = new Agent("Alex Turner", Email, 10, _clock.UtcNow);
        agent.SetPasswordHash("stored-hash");

        if (!active)
        {
            agent.Deactivate();
        }

        _agents.Setup(a => a.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        return agent;
    }

    [Fact]
    public async Task The_right_credentials_return_a_token_its_expiry_and_the_user()
    {
        ExistingAgent();

        var response = await _handler.HandleAsync(new LoginRequest(Email, "right-password"), CancellationToken.None);

        Assert.Equal("token-123", response.AccessToken);
        Assert.Equal(_clock.UtcNow.AddMinutes(60), response.ExpiresAtUtc);
        Assert.Equal("Alex Turner", response.User.FullName);
        Assert.Equal(Email, response.User.Email);
    }

    [Fact]
    public async Task A_wrong_password_is_unauthorized_and_no_token_is_issued()
    {
        ExistingAgent();

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(Email, "wrong-password"), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.InvalidCredentialsMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<Agent>(), It.IsAny<DateTime>()), Times.Never());
    }

    [Fact]
    public async Task An_unknown_email_gives_exactly_the_same_error_as_a_wrong_password()
    {
        ExistingAgent();
        _agents.Setup(a => a.GetByEmailAsync("nobody@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(Email, "wrong-password"), CancellationToken.None));

        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest("nobody@example.com", "whatever"), CancellationToken.None));

        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<Agent>(), It.IsAny<DateTime>()), Times.Never());
    }

    [Fact]
    public async Task An_unknown_email_still_runs_a_password_check_so_the_timing_gives_nothing_away()
    {
        _agents.Setup(a => a.GetByEmailAsync("nobody@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest("nobody@example.com", "whatever"), CancellationToken.None));

        _hasher.Verify(h => h.Verify(null, "whatever"), Times.Once());
    }

    [Fact]
    public async Task An_inactive_agent_cannot_sign_in_even_with_the_right_password()
    {
        ExistingAgent(active: false);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(Email, "right-password"), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.InvalidCredentialsMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<Agent>(), It.IsAny<DateTime>()), Times.Never());
    }

    [Fact]
    public async Task The_email_is_trimmed_before_it_is_looked_up()
    {
        ExistingAgent();

        var response = await _handler.HandleAsync(new LoginRequest($"  {Email}  ", "right-password"), CancellationToken.None);

        Assert.Equal(Email, response.User.Email);
    }
}