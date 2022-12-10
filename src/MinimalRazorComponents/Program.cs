using MinimalRazorComponents.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

var app = builder.Build();

app.UseStaticFiles();
app.UseBlazorFrameworkFiles();

// Manually mapping routes to components
app.MapGet("/", () => Results.Extensions.Component<HelloWorld>(new { Message = "Hello from Minimal APIs" }));
app.MapGet("/about", () => Results.Extensions.Component<About>());

// Rendering page components as first-class routable endpoints
//app.MapComponent<Form>();
//app.MapComponent<ClientApp>();

// Rendering all page components in an assembly
app.MapComponents<Program>();

app.Run();
