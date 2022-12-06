using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Microsoft.AspNetCore.Builder;

public static class MapComponentExtensions
{
    private readonly static MethodInfo _mapComponentMethod = typeof(MapComponentExtensions)
        .GetMethod(nameof(MapComponent), BindingFlags.Public | BindingFlags.Static, new[] { typeof(IEndpointRouteBuilder), typeof(string) })
            ?? throw new UnreachableException();

    public static IEndpointRouteBuilder MapComponent<T>(this IEndpointRouteBuilder endpointRouteBuilder, string? routePattern = null)
        where T : ComponentBase
    {
        var componentType = typeof(T);

        var mapRoutePattern = routePattern
            ?? componentType.GetCustomAttribute<RouteAttribute>()?.Template
            ?? throw new InvalidOperationException("A route for the component must either be declared via the @page directive or passed to MapComponent()");

        // Super hack, using the component itself as the object that RequestDelegateFactory binds to & then passing it to the renderer as the object
        // to get parameters from.
        endpointRouteBuilder.Map(mapRoutePattern, ([AsParameters]T component) => Results.Extensions.Component<T>(component))
            .WithName(ComponentName<T>.Name);

        return endpointRouteBuilder;
    }

    public static IEndpointRouteBuilder MapComponent(this IEndpointRouteBuilder endpointRouteBuilder, Type componentType, string? routePattern = null)
    {
        var mapComponentGeneric = _mapComponentMethod.MakeGenericMethod(componentType);
        mapComponentGeneric.Invoke(null, new object? [] { endpointRouteBuilder, routePattern });
        return endpointRouteBuilder;
    }

    /// <summary>
    /// Maps all page components in the assembly of the specified type.
    /// </summary>
    /// <param name="endpointRouteBuilder"></param>
    /// <returns></returns>
    public static IEndpointRouteBuilder MapComponents<TAssemblyType>(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var types = typeof(TAssemblyType).Assembly.GetTypes();

        foreach (var type in types)
        {
            if (typeof(ComponentBase).IsAssignableFrom(type) && type.GetCustomAttribute<RouteAttribute>() is { } routeAttribute)
            {
                var route = routeAttribute.Template;
                endpointRouteBuilder.MapComponent(type, route);
            }
        }

        return endpointRouteBuilder;
    }

    public static string? GetPathByComponent<T>(this LinkGenerator linkGenerator, object? values = null)
    {
        var name = ComponentName<T>.Name;
        return linkGenerator.GetPathByName(name, values);
    }

    private static class ComponentName<T>
    {
        public static string Name = "ComponentPageRoute__" + typeof(T).Name;
    }
}
