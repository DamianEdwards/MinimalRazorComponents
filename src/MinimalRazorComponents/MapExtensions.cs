using System.Reflection;
using Microsoft.AspNetCore.Components;

public static class MapExtensions
{
    public static IEndpointRouteBuilder MapComponent<T>(this IEndpointRouteBuilder endpointRouteBuilder, string? routePattern = null)
        where T : ComponentBase
    {
        var componentType = typeof(T);

        var mapRoutePattern = routePattern
            ?? componentType.GetCustomAttribute<RouteAttribute>()?.Template
            ?? throw new InvalidOperationException("A route for the component must either be declared on via the @page directive or passed to MapComponent()");

        // Super hack, using the component itself as the object that RDF binds to & then passing it to the renderer as the object
        // to get parameters from.
        endpointRouteBuilder.Map(mapRoutePattern, ([AsParameters]T component) => Results.Extensions.Component<T>(component))
            .WithName(GetEndpointName<T>());

        return endpointRouteBuilder;
    }

    public static string? GetPathByComponent<T>(this LinkGenerator linkGenerator, object? values = null)
    {
        var name = GetEndpointName<T>();
        return linkGenerator.GetPathByName(name, values);
    }

    private static string GetEndpointName<TComponent>() => "ComponentPageRoute__" + typeof(TComponent).Name;
}
