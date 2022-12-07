using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.Server;
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
        services.TryAddScoped<NavigationManager, HttpContextNavigationManager>();
        services.TryAddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

        return services;
    }
}
