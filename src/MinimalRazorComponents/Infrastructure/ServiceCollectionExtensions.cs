using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinimalRazorComponents.Infrastructure;

namespace Microsoft.AspNetCore.Builder;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds services required to render Razor Components.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddRazorComponents(this IServiceCollection services)
    {
        services.TryAddScoped<ComponentStatePersistenceManager>();
        services.TryAddScoped<NavigationManager, HttpNavigationManager>();

        return services;
    }
}
