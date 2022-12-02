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
