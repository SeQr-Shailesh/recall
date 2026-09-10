using Microsoft.Extensions.DependencyInjection;
using SeQrRecall.Application.Abstractions.Authentication;
using SeQrRecall.Application.Abstractions.Customers;
using SeQrRecall.Application.Abstractions.Interactions;
using SeQrRecall.Application.Abstractions.Leads;
using SeQrRecall.Application.Abstractions.Notes;
using SeQrRecall.Application.Abstractions.Processing;
using SeQrRecall.Application.Services;

namespace SeQrRecall.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<INotesService, NotesService>();
        services.AddScoped<ILeadsService, LeadsService>();
        services.AddScoped<ICustomersService, CustomersService>();
        services.AddScoped<ICustomerInteractionsService, CustomerInteractionsService>();
        services.AddScoped<INoteProcessingService, NoteProcessingService>();
        return services;
    }
}
