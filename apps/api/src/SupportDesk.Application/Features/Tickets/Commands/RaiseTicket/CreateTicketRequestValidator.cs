using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

public sealed class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MinimumLength(5)
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(4000);

        RuleFor(x => x.CustomerId).GreaterThan(0);

        RuleFor(x => x.CategoryId).GreaterThan(0);

        RuleFor(x => x.RequestedPriority)
            .IsInEnum()
            .When(x => x.RequestedPriority is not null);
    }
}
