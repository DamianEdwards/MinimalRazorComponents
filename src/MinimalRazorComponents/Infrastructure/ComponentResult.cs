using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Primitives;
using MinimalRazorComponents.Infrastructure;

namespace Microsoft.AspNetCore.Http.HttpResults;

public class ComponentResult<TComponent> : IResult
    where TComponent : IComponent
{
    private readonly bool _useRouteView;
    private IDictionary<string, object?>? _parameters;

    public ComponentResult()
    {
        var pageRoute = typeof(TComponent).GetCustomAttribute<RouteAttribute>()?.Template;
        var layout = typeof(TComponent).GetCustomAttribute<LayoutAttribute>()?.LayoutType;
        _useRouteView = !string.IsNullOrWhiteSpace(pageRoute) || layout is not null;
    }

    public IReadOnlyDictionary<string, object?> Parameters
    {
        get
        {
            _parameters ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            return _parameters.AsReadOnly();
        }
        set => _parameters = new Dictionary<string, object?>(value);
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = 200;
        httpContext.Response.ContentType = "text/html; charset=UTF-8";

        // PERF: ASP.NET Core 8 fixes issue in Renderer base class ctor that results in allocations for ILogger<T> creation
        var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var navigationManager = httpContext.RequestServices.GetRequiredService<NavigationManager>();
        if (navigationManager is HttpContextNavigationManager httpContextNavigationManager)
        {
            httpContextNavigationManager.Initialize(httpContext);
        }

        using var renderer = new HtmlRenderer(httpContext.RequestServices, loggerFactory);

        var bufferWriter = httpContext.Response.BodyWriter;
        var allowNavigation = !httpContext.Response.HasStarted;
        var user = httpContext.User;

        var rootComponentType = typeof(TComponent);
        ParameterView rootComponentParameters;

        var formCascadingValue = new CascadingValue<IEnumerable<KeyValuePair<string, StringValues>>>();

        if (_useRouteView)
        {
            // We're rendering a page so use a RouteView as the root component which will render the page that matches the route
            // according to its definied layout.
            rootComponentType = typeof(RouteView2);
            var routeData = httpContext.GetRouteData();
            var formValues = httpContext.Request.HasFormContentType ? await httpContext.Request.ReadFormAsync() : null;
            rootComponentParameters = GetCombinedParameters(routeData, formValues, _parameters);
        }
        else
        {
            rootComponentParameters = _parameters is null
                ? ParameterView.Empty
                : ParameterView.FromDictionary(_parameters);
        }

        var redirectToUrl = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RenderComponentAsync(rootComponentType, rootComponentParameters, bufferWriter, allowNavigation, user));

        if (redirectToUrl is not null)
        {
            httpContext.Response.Redirect(redirectToUrl);
        }
        else
        {
            await httpContext.Response.BodyWriter.FlushAsync();
        }
    }

    private static ParameterView GetCombinedParameters(Routing.RouteData routeData, IFormCollection? formValues, IDictionary<string, object?>? explicitParameters)
    {
        var routeValues = routeData.Values;

        if (explicitParameters is not null)
        {
            foreach (var parameter in explicitParameters)
            {
                routeValues.Add(parameter.Key, parameter.Value);
            }
        }

        var result = new Dictionary<string, object?>
        {
            { nameof(RouteView2.RouteData), new Components.RouteData(typeof(TComponent), (IReadOnlyDictionary<string, object>)routeValues) }
        };

        if (formValues is not null)
        {
            result.Add(nameof(RouteView2.FormValues), formValues);
        }

        return ParameterView.FromDictionary(result);
    }
}
