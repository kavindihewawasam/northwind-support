using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Agents.Services;
using SupportDesk.Application.Categories.Services;
using SupportDesk.Application.Customers.Services;
using SupportDesk.Application.Tickets.Services;

namespace SupportDesk.Application;

/// <summary>
/// Registers the use-case services and their request validators.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TicketService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<AgentService>();
        services.AddScoped<CategoryService>();

        services.AddValidatorsFromAssemblyContaining<TicketService>(ServiceLifetime.Singleton);

        return services;
    }
}
