using FluentValidation;
using SupportDesk.Application.Tickets.Dtos;

namespace SupportDesk.Application.Tickets.Validators;

public sealed class AssignTicketRequestValidator : AbstractValidator<AssignTicketRequest>
{
    public AssignTicketRequestValidator()
    {
        RuleFor(x => x.AgentId)
            .GreaterThan(0)
            .When(x => x.AgentId is not null);
    }
}
