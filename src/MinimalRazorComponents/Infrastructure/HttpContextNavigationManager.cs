using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.Extensions;

namespace MinimalRazorComponents.Infrastructure;

internal class HttpContextNavigationManager : NavigationManager
{
    private HttpContext? _httpContext;
    private bool _initialized;

    public void Initialize(HttpContext httpContext)
    {
        if (!Equals(_httpContext, httpContext))
        {
            _httpContext = httpContext;
            _initialized = false;
        }
    }

    // This is called by the base to enable lazy initialization so we can avoid allocating the URI strings unless someone asks for them
    protected override void EnsureInitialized()
    {
        if (_httpContext is null)
        {
            throw new InvalidOperationException($"{nameof(HttpContextNavigationManager)}.{nameof(Initialize)}({nameof(HttpContext)} httpContext) has not been called.");
        }

        if (!_initialized)
        {
            var request = _httpContext.Request;
            var baseUri = GetBaseUri(UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase));
            var currentUri = UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, request.Path, request.QueryString);

            Initialize(baseUri, currentUri);

            _initialized = true;
        }
    }

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        throw new NavigationException(uri);
    }

    private static string GetBaseUri(string uri)
    {
        // PathBase may be "/" or "/some/thing", but to be a well-formed base URI
        // it has to end with a trailing slash
        return uri.EndsWith('/') ? uri : uri += "/";
    }
}