using Microsoft.AspNetCore.Components;
using MinimalRazorComponents.Infrastructure;

namespace Microsoft.AspNetCore.Http.HttpResults;

public class ComponentResult<TComponent> : IResult
    where TComponent : IComponent, new()
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

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = 200;
        httpContext.Response.ContentType = "text/html";

        var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var renderer = new HtmlRenderer(httpContext.RequestServices, loggerFactory, httpContext.Response.BodyWriter);

        var parameterView = _parameters is null
            ? ParameterView.Empty
            : ParameterView.FromDictionary(_parameters);

        return renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<TComponent>(parameterView));
    }
}
