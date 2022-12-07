using Microsoft.AspNetCore.Components;
using MinimalRazorComponents.Infrastructure;

namespace Microsoft.AspNetCore.Http.HttpResults;

public class ComponentResult<TComponent> : IResult
    where TComponent : IComponent
{
    private IDictionary<string, object?>? _parameters;

    public IDictionary<string, object?> Parameters
    {
        get
        {
            _parameters ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            return _parameters;
        }
        set => _parameters = value;
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

        var initialParameters = _parameters is null
            ? ParameterView.Empty
            : ParameterView.FromDictionary(_parameters);

        var bufferWriter = httpContext.Response.BodyWriter;
        var allowNavigation = !httpContext.Response.HasStarted;
        var user = httpContext.User;

        var redirectToUrl = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RenderComponentAsync<TComponent>(initialParameters, bufferWriter, allowNavigation, user));

        if (redirectToUrl is not null)
        {
            httpContext.Response.Redirect(redirectToUrl);
        }
        else
        {
            await httpContext.Response.BodyWriter.FlushAsync();
        }
    }
}
