# Minimal Razor Components

## What is this?

An exploration of allowing returning rendered Razor Components (aka Blazor Components) from ASP.NET Core Minimal API endpoints.


### Requirements

This solution uses the .NET 7 SDK. Get it from https://dotnet.microsoft.com/download

### Example

**Program.cs**

```csharp
using MinimalRazorComponents.Components;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => Results.Extensions.Component<HelloWorld>(new { Message = "Hello from Minimal APIs" }));
app.MapComponent<MyPage>();

app.Run();
```

**HelloWorld.razor**

```razor
<LayoutView Layout="typeof(Layout)">
    <h2>Hello World Component</h2>

    <p>This was rendered from a Razor Component!</p>

    @if (Message is not null)
    {
        <p>@Message</p>
    }
</LayoutView>

@code {
    [Parameter]
    public string Message { get; set; } = "";
}

```

### Notes & Thoughts

- Rendering considerations:
  - Is the default server-based rendering with full page navigations unless client component explicitly involved?
  - Or is the initial page render via server then subsequent server component changes via fetch with client-side tree patching?
  - Client components that are children of server components that accept parameters would need to be client-side refreshed after the parent is re-rendered or even completely removed depending on new state of server component
  - Likely want the renderer to have an optimized path for rendering the render tree to the response output while it's being built, rather than building the full tree (with its allocations) simply to turn around and render it to the response output
  - String literals in render tree could likely be UTF8 string literals now to save allocations and space on the string heap and reduce chances of hitting the assembly string literal limit
- Layout
  - When returning rendered frameworks from minimal APIs it would be useful to be able dynamically wrap them in a `LayoutView` such that a page can specify its layout view `@layout` and be specified as the component to render, and then have the `IResult` nest it in the `LayoutView` before rendering (or similar)
  - Similar for the multi-page scenario implemented by `MapComponents<TAssemblyType>`
- Server vs. client components
  - Are components assumed to be universal (server or client) by default and thus require explicit opting in to server or client behavior?
  - How to declare a component as server or client?
    - Filename suffix, e.g. MyComponent.client.razor vs. MyComponent.server.razor?
    - New directive, e.g. @client vs. @server?
    - Attribute, e.g. @attribute [ClientComponent] vs. @attribute [ServerComponent]?
  - I'd assume design/compile-time errors with regards to components trying to do things their stated hosting type doesn't allow, e.g. can only handle client-interaction events in client or "connected" components, can only access server-domain services in server components, etc.
  - How does DI work in this world? Are services logically declared in different "domains" (client vs. server vs. universal)?
  - Do we expect client components to talk to the server via separate, standard, developer built endpoints/APIs? This would also require a way to allow client components to trigger server component re-rendering.
  - Alternatively, we could support automatic client->server component communication via existing Blazor concepts like `@inject` and component parameters, whereby a transparent object proxy is created by the framework anytime the client->server boundary is crossed in the render tree that takes care of marshalling the calls and triggering server-based rerendering when required. The types that exist across these boundaries would need to have their method calls be serializable which could be enforced at compile/design time (similar to Orleans remote object RPC). This would result in a "seamless" app model whereby e.g. a server component could talk directly to a database via EF and a child client component could call methods on an object provided by the server component as a parameter (maybe even the `DbContext` directly?) that results in a remote call to the server and automatically trigger rerendering if required.
  - The client/server component separation needs some thought from a project and build point of view. The server project references the client project currently which limits what the client components can do (e.g. they can't reference things in the server project). It might be more compelling to have all this in a single project which would require some work on compilation, etc.
- Routing
  - In a universal client/server component model an updated router component would be required as the assumption is server page components result in actual endpoint routes being created so that standard route-based link generation works (unlike Blazor Server today where component pages are transparent to the actual app)
  - The router component would likely need to be server and client aware to ensure the existing Blazor router semantics map well to the server static routing semantics
  - Not sure if the route component would be the thing directly emitting endpoints (e.g. adding an `EndpointDataSource`) or that would happen via the `Map` calls and then details be passed in somehow
- Connected components
  - Do what are currently Blazor Server components become a new component type, aka Connected Components, that require server state & an active SignalR connection?
  - In this model this component type should be extended to support more of the native SignalR concepts like client groups, etc. so that building connected client scenarios is a first-class feature of the component/app model
- Form handling
  - Likely want to keep using existing form components thus they'll need to be updated to handle server-side/POST submit semantics
  - A single component containing a form could be rendered in either the server or client
- App component
  - Where is the `App` component in this world? What does it look like?
