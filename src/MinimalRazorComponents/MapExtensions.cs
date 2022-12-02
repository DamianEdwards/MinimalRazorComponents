using System.Reflection;
using Microsoft.AspNetCore.Components;

public static class MapExtensions
{
    public static IEndpointRouteBuilder MapComponent<T>(this IEndpointRouteBuilder endpointRouteBuilder)
        where T : ComponentBase
    {
        var componentType = typeof(T);

        var routeAttribute = componentType.GetCustomAttribute<RouteAttribute>();

        if (routeAttribute is null)
        {
            throw new InvalidOperationException("Component must declare a page route.");
        }

        // Super hack, using the component itself as the object that RDF binds to & then passing it to the renderer as the object
        // to get parameters from.
        endpointRouteBuilder.Map(routeAttribute.Template, ([AsParameters]T component) => Results.Extensions.Component<T>(component))
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
