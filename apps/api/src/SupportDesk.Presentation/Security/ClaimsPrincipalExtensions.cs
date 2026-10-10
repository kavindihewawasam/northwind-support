using System.Security.Claims;
using SupportDesk.Application.Contracts.Auth;

namespace SupportDesk.Presentation.Security;

/// <summary>Reads the signed-in agent back out of the token's claims.</summary>
public static class ClaimsPrincipalExtensions
{
    public static CurrentUserDto? ToCurrentUser(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirst("sub")?.Value;
        var name = principal.FindFirst("name")?.Value;
        var email = principal.FindFirst("email")?.Value;

        return int.TryParse(id, out var agentId) && name is not null && email is not null
            ? new CurrentUserDto(agentId, name, email)
            : null;
    }
}