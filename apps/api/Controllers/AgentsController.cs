using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Agents.Dtos;
using SupportDesk.Application.Agents.Services;

namespace SupportDesk.Api.Controllers;

[ApiController]
[Route("api/agents")]
[Produces("application/json")]
public sealed class AgentsController(AgentService agents) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AgentDto>> GetAll(CancellationToken ct) => agents.GetAllAsync(ct);
}
