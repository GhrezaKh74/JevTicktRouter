using FluentValidation;
using JevTicketRouter.Application.Tickets;
using JevTicketRouter.Application.Tickets.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace JevTicketRouter.Application;

/// <summary>Registration helpers for the application layer.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the triage pipeline and every FluentValidation validator in this assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITicketTriageService, TicketTriageService>();
        services.AddValidatorsFromAssemblyContaining<TriageTicketRequestValidator>(ServiceLifetime.Singleton);

        return services;
    }
}
