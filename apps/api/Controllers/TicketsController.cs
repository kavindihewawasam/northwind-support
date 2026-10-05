using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Common;
using SupportDesk.Application.Tickets.Dtos;
using SupportDesk.Application.Tickets.Queries;
using SupportDesk.Application.Tickets.Services;

namespace SupportDesk.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
public sealed class TicketsController(TicketService tickets) : ControllerBase
{
    /// <summary>Searches tickets with filters, sorting and paging.</summary>
    [HttpGet]
    public Task<PagedResult<TicketListItemDto>> Search([FromQuery] TicketQuery query, CancellationToken ct) =>
        tickets.SearchAsync(query, ct);

    /// <summary>Returns one ticket.</summary>
    [HttpGet("{id:int}")]
    public Task<TicketDetailDto> Get(int id, CancellationToken ct) => tickets.GetAsync(id, ct);

    /// <summary>Raises a new ticket.</summary>
    [HttpPost]
    public async Task<ActionResult<TicketDetailDto>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var created = await tickets.CreateAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Moves a ticket to another status.</summary>
    [HttpPatch("{id:int}/status")]
    public Task<TicketDetailDto> UpdateStatus(int id, UpdateTicketStatusRequest request, CancellationToken ct) =>
        tickets.ChangeStatusAsync(id, request.Status, ct);

    /// <summary>Assigns the ticket to an agent, or unassigns it when no agent is given.</summary>
    [HttpPatch("{id:int}/assignment")]
    public Task<TicketDetailDto> Assign(int id, AssignTicketRequest request, CancellationToken ct) =>
        tickets.AssignAsync(id, request.AgentId, ct);
}
