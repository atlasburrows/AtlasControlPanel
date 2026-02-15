using Atlas.Application.Licensing;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddSingleton<LicenseValidator>();
        return services;
    }
}
