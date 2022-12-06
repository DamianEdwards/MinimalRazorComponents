using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.Extensions;
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
        httpContext.Response.ContentType = "text/html";

        var loggerFactory = httpContext.RequestServices.GetRequiredService<ILoggerFactory>();
        var renderer = new HtmlRenderer(httpContext.RequestServices, loggerFactory);

        var initialParameters = _parameters is null
            ? ParameterView.Empty
            : ParameterView.FromDictionary(_parameters);

        var bufferWriter = httpContext.Response.BodyWriter;
        var request = httpContext.Request;
        var baseUri = UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase);
        var currentUri = UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, request.Path, request.QueryString);
        var allowNavigation = !httpContext.Response.HasStarted;
        var user = httpContext.User;

        var redirectToUrl = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RenderComponentAsync<TComponent>(initialParameters, bufferWriter, baseUri, currentUri, allowNavigation, user));

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
